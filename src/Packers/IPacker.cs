using System;
using System.Linq;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;
using AsmResolver.PE.File.Headers;

namespace Origami.Packers
{
    public interface IPacker
    {
        void Execute();
    }

    public abstract class Packer : IPacker
    {
        protected Packer(byte[] payload, string outputPath)
        {
            Payload = payload;
            OutputPath = outputPath;
        }

        protected byte[] Payload
        {
            get;
        }

        protected string OutputPath
        {
            get;
        }

        protected static ModuleDefinition CreateStub(ModuleDefinition originModule)
        {
            var stubModule =
                new ModuleDefinition(originModule.Name,
                    originModule.CorLibTypeFactory.CorLibScope.GetAssembly() as AssemblyReference);

            bool isCoreApp = originModule.OriginalTargetRuntime.Name == ".NETCoreApp";

            originModule.Assembly.Modules.Insert(0, stubModule);

            stubModule.FileCharacteristics = originModule.FileCharacteristics;
            stubModule.DllCharacteristics = originModule.DllCharacteristics;
            stubModule.EncBaseId = originModule.EncBaseId;
            stubModule.EncId = originModule.EncId;
            stubModule.Generation = originModule.Generation;

            // For .NETCoreApp consider installed runtime bitness, reasonable to assume 64bit installation of .NET (Core)
            stubModule.PEKind = isCoreApp ? OptionalHeaderMagic.Pe32Plus :  originModule.PEKind;
            stubModule.MachineType = isCoreApp ? MachineType.Amd64 : originModule.MachineType;
            stubModule.IsBit32Required = !isCoreApp && originModule.IsBit32Required;
            stubModule.IsBit32Preferred = !isCoreApp && originModule.IsBit32Preferred;

            stubModule.RuntimeVersion = originModule.RuntimeVersion;
            stubModule.SubSystem = originModule.SubSystem;

            // Copy NativeResourceDirectory preserves icons and manifest
            if (originModule.NativeResourceDirectory != null)
            {
                stubModule.NativeResourceDirectory = originModule.NativeResourceDirectory;
            }

            stubModule.ImportAssemblyTypeReferences(originModule);

            return stubModule;
        }

        protected static void InjectLoader(ModuleDefinition targetModule, Type loaderClass)
        {
            var sourceModule = ModuleDefinition.FromFile(typeof(Packer).Assembly.Location);
            var cloner = new MemberCloner(targetModule);
            var loader = (TypeDefinition) sourceModule.LookupMember(loaderClass.MetadataToken);
            cloner.Include(loader, true);
            var result = cloner.Clone();

            foreach (var clonedType in result.ClonedTopLevelTypes)
                targetModule.TopLevelTypes.Add(clonedType);

            result.GetClonedMember(loader).Namespace = "";

            var entryPoint = (MethodDefinition) result.ClonedMembers.First(m => m.Name == "Main");
            entryPoint.Name = ".origami";
            entryPoint.DeclaringType.Name = "<Origami>";

            targetModule.ManagedEntrypoint = entryPoint;
        }

        public abstract void Execute();
    }

    public enum Mode
    {
        PESection = 0x0,
        DebugDataEntry = 0x1
    }
}
