using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Builder;
using AsmResolver.PE.File;
using AsmResolver.PE.File.Headers;
using Origami.Runtime;

namespace Origami.Packers
{
    public sealed class SectionPacker : Packer
    {
        private readonly ModuleDefinition _stubModule;

        public SectionPacker(byte[] payload, string outputPath) : base(payload, outputPath)
        {
            _stubModule = CreateStub(ModuleDefinition.FromBytes(payload));
        }

        public override void Execute()
        {
            InjectLoader(_stubModule, typeof(PeSectionLoader));

            var peImage = _stubModule.ToPEImage();
            var fileBuilder = new ManagedPEFileBuilder();
            var peFile = fileBuilder.CreateFile(peImage);
            var section = new PESection(Name,
                SectionFlags.MemoryRead | SectionFlags.MemoryWrite | SectionFlags.ContentUninitializedData, new DataSegment(Payload.Compress(Name)));
            peFile.Sections.Add(section);
            peFile.Write(OutputPath);
        }
    }
}