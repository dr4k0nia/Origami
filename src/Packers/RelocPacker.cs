using System;
using System.Linq;
using System.Security.Cryptography;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Builder;
using AsmResolver.DotNet.Cloning;
using AsmResolver.PE.DotNet.Cil;
using Origami.Runtime;

namespace Origami.Packers
{
    public sealed class RelocPacker : Packer
    {
        private readonly Mode _mode;
        private readonly ModuleDefinition _stubModule;

        private string _key;

        public RelocPacker(Mode mode, byte[] payload, string outputPath)
            : base(payload, outputPath)
        {
            _mode = mode;
            _stubModule = CreateStub(ModuleDefinition.FromBytes(payload));
        }

        public override void Execute()
        {
            _key = RandomString();
            InjectLoader(_stubModule, typeof(RelocLoader), out var oldToken);
            _stubModule.IsILOnly = false;

            var patches = GetOffsets();

            var imageBuilder = new ManagedPEImageBuilder();

            var imageResult = imageBuilder.CreateImage(_stubModule);

            imageResult.TokenMapping.TryGetNewToken(oldToken, out var newToken);

            var payload = new DataSegment(Payload.Compress(_key));

            var fileBuilder = new CustomManagedPEFileBuilder(_mode, payload, newToken, patches);
            var peImage = imageResult.ConstructedImage;
            var peFile = fileBuilder.CreateFile(imageResult.ConstructedImage);

            peFile.Write(OutputPath);
        }

        private void InjectLoader(ModuleDefinition targetModule, Type loaderClass, out IMetadataMember offset)
        {
            var sourceModule = ModuleDefinition.FromFile(typeof(Packer).Assembly.Location);
            var cloner = new MemberCloner(targetModule);
            var loader = (TypeDefinition) sourceModule.LookupMember(loaderClass.MetadataToken);
            cloner.Include(loader, true);
            var result = cloner.Clone();

            foreach (var clonedType in result.ClonedTopLevelTypes)
                targetModule.TopLevelTypes.Add(clonedType);

            var member = result.GetClonedMember(loader);

            member.Namespace = "";

            offset = member.Methods.First(m => m.Name == "Main");

            var entryPoint = (MethodDefinition) result.ClonedMembers.First(m => m.Name == "Main");
            entryPoint.Name = _key;
            entryPoint.DeclaringType!.Name = "<Origami>";

            targetModule.ManagedEntrypoint = entryPoint;
        }

        private Patches GetOffsets()
        {
            var patches = new Patches();

            var entryPoint = _stubModule.ManagedEntrypointMethod;

            var instructions = entryPoint!.CilMethodBody?.Instructions;


            var target = instructions.First(i => i.OpCode == CilOpCodes.Ldc_I8);
            patches.OffsetVA = target.Offset + target.OpCode.Size;

            target = instructions.First(i => i.IsLdcI4() && i.GetLdcI4Constant() == 0x1337c0de);
            patches.OffsetSize = target.Offset + target.OpCode.Size;

            return patches;
        }

        private string RandomString()
        {
            using var cryptoProvider = new RNGCryptoServiceProvider();
            byte[] bytes = new byte[64];
            cryptoProvider.GetBytes(bytes);

            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        public struct Patches
        {
            public int OffsetVA;
            public int OffsetSize;
        }
    }
}
