using System;
using System.Collections.Generic;
using System.Linq;
using AsmResolver;
using AsmResolver.PE;
using AsmResolver.PE.Debug;
using AsmResolver.PE.Debug.CodeView;
using AsmResolver.PE.DotNet;
using AsmResolver.PE.DotNet.Builder;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using AsmResolver.PE.File;
using AsmResolver.PE.File.Headers;
using AsmResolver.PE.Imports;
using AsmResolver.PE.Relocations;

namespace Origami.Packers;

public sealed class CustomManagedPEFileBuilder : ManagedPEFileBuilder
{
    private readonly Mode _mode;
    private readonly DataSegment _payload;
    private readonly MetadataToken _token;
    private readonly RelocPacker.Patches _patches;

    public CustomManagedPEFileBuilder(Mode mode, DataSegment payload, MetadataToken token, RelocPacker.Patches patches)
    {
        _mode = mode;
        _payload = payload;
        _token = token;
        _patches = patches;
    }


    // Modified to add Base Relocations and patch RVAs in the CIL method body of the loader
    public override PEFile CreateFile(IPEImage image)
    {
        // Get raw method body of the loader entrypoint
        var rawBody = (CilRawMethodBody) image.DotNetDirectory.Metadata
            .GetStream<TablesStream>()
            .GetTable<MethodDefinitionRow>()
            .GetByRid(_token.Rid)
            .Body
            .GetSegment();

        var oldBody = rawBody.Code;

        var newBody = new DataSegment(oldBody.CreateReader().ReadToEnd());
        rawBody.Code = newBody;

        // Add BaseRelocation to use VirtualAddress
        var reloc = image.PEKind == OptionalHeaderMagic.Pe32
            ? new BaseRelocation(RelocationType.HighLow, newBody.ToReference(_patches.OffsetVA))
            : new BaseRelocation(RelocationType.Dir64, newBody.ToReference(_patches.OffsetVA));
        image.Relocations.Add(reloc);

        var file = base.CreateFile(image);
        file.AlignSections();

        byte[] body = newBody.Data;

        // Patch placeholder for VirtualAddress of payload
        BitConverter.GetBytes(_payload.Rva + image.ImageBase).CopyTo(body, _patches.OffsetVA);

        // Patch placeholder for size of buffer
        BitConverter.GetBytes(_payload.GetPhysicalSize()).CopyTo(body, _patches.OffsetSize);

        return file;
    }

    private PESection CreatePackerSection(IPEImage image, ManagedPEBuilderContext context)
    {
        ProcessRvasInMetadataTables(context);
        var contents = new SegmentBuilder();

        contents.Add(_payload);

        contents.Add(context.DotNetSegment);

        return new PESection(".origami",
            SectionFlags.ContentInitializedData | SectionFlags.MemoryRead)
        {
            Contents = contents
        };
    }

    protected override PESection CreateTextSection(IPEImage image, ManagedPEBuilderContext context)
    {
        CreateImportDirectory(image, context);
        CreateDebugDirectory(image, context);

        var contents = new SegmentBuilder();
        if (context.ImportDirectory.Count > 0)
            contents.Add(context.ImportDirectory.ImportAddressDirectory);

        // Add the DotNetSegment normally when using the DebugDataEntry payload
        if (_mode == Mode.DebugDataEntry)
            contents.Add(context.DotNetSegment);

        if (context.ImportDirectory.Count > 0)
            contents.Add(context.ImportDirectory);

        if (!context.ExportDirectory.IsEmpty)
            contents.Add(context.ExportDirectory);

        if (!context.DebugDirectory.IsEmpty)
        {
            contents.Add(context.DebugDirectory);
            contents.Add(context.DebugDirectory.ContentsTable);
        }

        if (context.Bootstrapper.HasValue)
            contents.Add(context.Bootstrapper.Value.Segment);

        if (image.Exports is {Entries: {Count: > 0} entries})
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var export = entries[i];
                if (export.Address.IsBounded && export.Address.GetSegment() is { } segment)
                    contents.Add(segment, 4);
            }
        }

        return new PESection(".text",
            SectionFlags.ContentCode | SectionFlags.MemoryExecute | SectionFlags.MemoryRead)
        {
            Contents = contents
        };
    }


    // Add packer section injection
    protected override IEnumerable<PESection> CreateSections(IPEImage image, ManagedPEBuilderContext context)
    {
        // Always create .text section.
        var sections = new List<PESection>
        {
            CreateTextSection(image, context),
        };

        // Add .sdata section when necessary.
        if (image.Exports is not null || image.DotNetDirectory?.VTableFixups is not null)
            sections.Add(CreateSDataSection(image, context));

        // Add .rsrc section when necessary.
        if (image.Resources is not null && image.Resources.Entries.Count > 0)
            sections.Add(CreateRsrcSection(image, context));

        // Inject packer section if PESection payload was chosen
        if (_mode == Mode.PESection)
            sections.Add(CreatePackerSection(image, context));

        // Collect all base relocations.
        // Since the PE is rebuild in its entirety, all relocations that were originally in the PE are invalidated.
        // Therefore, we filter out all relocations that were added by the reader.
        var relocations = image.Relocations
            .Where(r => r.Location is not PESegmentReference)
            .ToList();

        // Add relocations of the bootstrapper stub if necessary.
        if (context.Bootstrapper.HasValue)
            relocations.AddRange(context.Bootstrapper.Value.Relocations);

        // Add .reloc section when necessary.
        if (relocations.Count > 0)
            sections.Add(CreateRelocSection(context, relocations));

        return sections;
    }

    private static void CreateImportDirectory(IPEImage image, ManagedPEBuilderContext context)
    {
        bool importEntrypointRequired = context.Platform.IsClrBootstrapperRequired
                                        || (image.DotNetDirectory!.Flags & DotNetDirectoryFlags.ILOnly) == 0;
        string entrypointName = (image.Characteristics & Characteristics.Dll) != 0
            ? "_CorDllMain"
            : "_CorExeMain";

        var modules = CollectImportedModules(image, importEntrypointRequired, entrypointName, out var entrypointSymbol);

        foreach (var module in modules)
            context.ImportDirectory.AddModule(module);

        if (importEntrypointRequired)
        {
            if (entrypointSymbol is null)
                throw new InvalidOperationException("Entrypoint symbol was required but not imported.");

            context.Bootstrapper = context.Platform.CreateThunkStub(image.ImageBase, entrypointSymbol);
        }
    }

    private static List<IImportedModule> CollectImportedModules(
        IPEImage image,
        bool entryRequired,
        string mscoreeEntryName,
        out ImportedSymbol? entrypointSymbol)
    {
        var modules = new List<IImportedModule>();

        IImportedModule? mscoreeModule = null;
        entrypointSymbol = null;

        foreach (var module in image.Imports)
        {
            // Check if the CLR entrypoint is already imported.
            if (module.Name == "mscoree.dll")
            {
                mscoreeModule = module;

                // Find entrypoint in this imported module.
                if (entryRequired)
                    entrypointSymbol = mscoreeModule.Symbols.FirstOrDefault(s => s.Name == mscoreeEntryName);

                // Only include mscoree.dll if necessary.
                if (entryRequired || module.Symbols.Count > 1)
                    modules.Add(module);
            }
            else
            {
                // Imported module is some other module. Just add in its entirety.
                modules.Add(module);
            }
        }

        if (entryRequired)
        {
            // Add mscoree.dll if it wasn't imported yet.
            if (mscoreeModule is null)
            {
                mscoreeModule = new ImportedModule("mscoree.dll");
                modules.Add(mscoreeModule);
            }

            // Add entrypoint sumbol if it wasn't imported yet.
            if (entrypointSymbol is null)
            {
                entrypointSymbol = new ImportedSymbol(0, mscoreeEntryName);
                mscoreeModule.Symbols.Add(entrypointSymbol);
            }
        }

        return modules;
    }


    // Modified to inject fake RSDS DebugDataEntry and payload as custom DebugDataEntry
    private void CreateDebugDirectory(IPEImage image, ManagedPEBuilderContext context)
    {
        for (int i = 0; i < image.DebugData.Count; i++)
            context.DebugDirectory.AddEntry(image.DebugData[i]);

        // Dont inject DebugData when PESection is used
        if (_mode == Mode.PESection)
            return;

        // Add fake debug entry to hide the payload
        var data = new RsdsDataSegment
        {
            // TODO randomization
            Path = "C:/Users/GLaDOS/The/Cake/Is/A/Lie.pdb",
            Guid = Guid.NewGuid(),
            Age = 1
        };
        var entry = new DebugDataEntry(data)
        {
            TimeDateStamp = Utils.GetRandomTimestamp()
        };
        context.DebugDirectory.AddEntry(entry);

        // Process RVAs and inject payload as custom DebugDataEntry
        ProcessRvasInMetadataTables(context);
        var segment = new CustomDebugDataSegment(DebugDataType.Unknown, _payload);
        var payload = new DebugDataEntry(segment);

        context.DebugDirectory.AddEntry(payload);
    }

    /// <summary>
    /// Creates the .sdata section containing the exports and vtables directory of the new .NET PE file.
    /// </summary>
    /// <param name="image">The image to build.</param>
    /// <param name="context">The working space of the builder.</param>
    /// <returns>The section.</returns>
    private PESection CreateSDataSection(IPEImage image, ManagedPEBuilderContext context)
    {
        var contents = new SegmentBuilder();

        if (image.DotNetDirectory?.VTableFixups is { } fixups)
        {
            for (int i = 0; i < fixups.Count; i++)
                contents.Add(fixups[i].Tokens);
        }

        if (image.Exports is {Entries: {Count: > 0}} exports)
        {
            context.ExportDirectory.AddDirectory(exports);
            contents.Add(context.ExportDirectory, 4);
        }

        return new PESection(
            ".sdata",
            SectionFlags.MemoryRead | SectionFlags.MemoryWrite | SectionFlags.ContentInitializedData,
            contents);
    }

    private static void ProcessRvasInMetadataTables(ManagedPEBuilderContext context)
    {
        var dotNetSegment = context.DotNetSegment;
        var tablesStream = dotNetSegment.DotNetDirectory.Metadata?.GetStream<TablesStream>();
        if (tablesStream is null)
            throw new ArgumentException("Image does not have a .NET metadata tables stream.");

        AddMethodBodiesToTable(dotNetSegment.MethodBodyTable, tablesStream);
        AddFieldRvasToTable(context);
    }

    private static void AddMethodBodiesToTable(MethodBodyTableBuffer table, TablesStream tablesStream)
    {
        var methodTable = tablesStream.GetTable<MethodDefinitionRow>();
        for (int i = 0; i < methodTable.Count; i++)
        {
            var methodRow = methodTable[i];

            var bodySegment = GetMethodBodySegment(methodRow);
            if (bodySegment is CilRawMethodBody cilBody)
                table.AddCilBody(cilBody);
            else if (bodySegment is not null)
                table.AddNativeBody(bodySegment, 4); // TODO: maybe make customizable?
            else
                continue;

            methodTable[i] = new MethodDefinitionRow(
                bodySegment.ToReference(),
                methodRow.ImplAttributes,
                methodRow.Attributes,
                methodRow.Name,
                methodRow.Signature,
                methodRow.ParameterList);
        }
    }

    private static ISegment? GetMethodBodySegment(MethodDefinitionRow methodRow)
    {
        if (methodRow.Body.IsBounded)
            return methodRow.Body.GetSegment();

        if (methodRow.Body.CanRead)
        {
            if ((methodRow.ImplAttributes & MethodImplAttributes.CodeTypeMask) == MethodImplAttributes.IL)
            {
                var reader = methodRow.Body.CreateReader();
                return CilRawMethodBody.FromReader(ThrowErrorListener.Instance, ref reader);
            }

            throw new NotImplementedException("Native unbounded method bodies cannot be reassembled yet.");
        }

        return null;
    }

    private static void AddFieldRvasToTable(ManagedPEBuilderContext context)
    {
        var metadata = context.DotNetSegment.DotNetDirectory.Metadata;
        var fieldRvaTable = metadata
            !.GetStream<TablesStream>()
            !.GetTable<FieldRvaRow>(TableIndex.FieldRva);

        if (fieldRvaTable.Count == 0)
            return;

        var table = context.DotNetSegment.FieldRvaTable;
        var reader = context.FieldRvaDataReader;

        for (int i = 0; i < fieldRvaTable.Count; i++)
        {
            var data = reader.ResolveFieldData(ThrowErrorListener.Instance, metadata, fieldRvaTable[i]);
            if (data is null)
                continue;

            table.Add(data);
        }
    }
}
