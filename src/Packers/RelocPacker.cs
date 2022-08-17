﻿using System;
using System.Linq;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Builder;
using AsmResolver.DotNet.Cloning;
using AsmResolver.PE.DotNet.Cil;
using Origami.Runtime;
using static Origami.Packers.CustomManagedPEFileBuilder;

namespace Origami.Packers
{
    public sealed class RelocPacker : Packer
    {
        private readonly Mode _mode;
        private readonly ModuleDefinition _stubModule;

        public RelocPacker(Mode mode, byte[] payload, string outputPath) : base(payload, outputPath)
        {
            _mode = mode;
            _stubModule = CreateStub(ModuleDefinition.FromBytes(payload));
        }

        public override void Execute()
        {
            InjectLoader(_stubModule, typeof(RelocLoader), out var oldToken);
            _stubModule.IsILOnly = false;

            Patches patches = GetOffsets();

            var imageBuilder = new ManagedPEImageBuilder();

            var imageResult = imageBuilder.CreateImage(_stubModule);

            imageResult.TokenMapping.TryGetNewToken(oldToken, out var newToken);

            var payload = new DataSegment(Payload.Compress(Name));

            var fileBuilder = new CustomManagedPEFileBuilder(_mode, payload, newToken, patches);
            var peImage = imageResult.ConstructedImage;
            var peFile = fileBuilder.CreateFile(imageResult.ConstructedImage);

            peFile.Write(OutputPath);
        }

        private static void InjectLoader(ModuleDefinition targetModule, Type loaderClass, out IMetadataMember offset)
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
            entryPoint.Name = ".origami";
            entryPoint.DeclaringType!.Name = "<Origami>";

            targetModule.ManagedEntrypoint = entryPoint;
        }

        private Patches GetOffsets()
        {
            var patches = new Patches();

            var entryPoint = _stubModule.ManagedEntrypointMethod;

            var instructions = entryPoint!.CilMethodBody?.Instructions;

            foreach (var instruction in instructions!)
            {
                if (instruction.OpCode == CilOpCodes.Ldc_I8)
                {
                    //if ((ulong)instruction.Operand! == 6969696969L)
                        patches.OffsetVA = instruction.Offset + instruction.OpCode.Size;
                }

                if (!instruction.IsLdcI4())
                    continue;

                // Offset for payload length
                if (instruction.GetLdcI4Constant() == 0x1337c0de)
                    patches.OffsetSize = instruction.Offset + instruction.OpCode.Size;

                // // Offset for RVA
                // if (instruction.GetLdcI4Constant() == 0x420c0de)
                //     patches.OffsetRva = instruction.Offset + instruction.OpCode.Size;
            }

            return patches;
        }

        public struct Patches
        {
            public int OffsetVA;
            public int OffsetSize;
            public int OffsetRva;
        }


    }
}