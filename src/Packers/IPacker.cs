using System;
using System.Linq;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;

namespace Origami.Packers
{
    public interface IPacker
    {
        void Execute();
    }

    public class Packer
    {
        protected static ModuleDefinition CreateStub(ModuleDefinition originModule)
        {
            var stubModule =
                new ModuleDefinition( originModule.Name, originModule.CorLibTypeFactory.CorLibScope.GetAssembly() as AssemblyReference);
            
            originModule.Assembly.Modules.Insert( 0, stubModule );
            
            stubModule.FileCharacteristics = originModule.FileCharacteristics;
            stubModule.DllCharacteristics = originModule.DllCharacteristics;
            stubModule.EncBaseId = originModule.EncBaseId;
            stubModule.EncId = originModule.EncId;
            stubModule.Generation = originModule.Generation;
            stubModule.PEKind = originModule.PEKind;
            stubModule.MachineType = originModule.MachineType;
            stubModule.RuntimeVersion = originModule.RuntimeVersion;
            stubModule.IsBit32Required = originModule.IsBit32Required;
            stubModule.IsBit32Preferred = originModule.IsBit32Preferred;
            
            ImportAssemblyTypeReferences(originModule, stubModule);
            
            return stubModule;
        }

        private static void ImportAssemblyTypeReferences(ModuleDefinition origin, ModuleDefinition target)
        {
            var assembly = origin.Assembly;
            var importer = new ReferenceImporter(target);
            foreach (var ca in assembly.CustomAttributes.Where(ca => ca.Constructor.Module == origin))
                ca.Constructor = (ICustomAttributeType) importer.ImportMethod(ca.Constructor);
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

            targetModule.ManagedEntrypoint = entryPoint;
        }
    }
}