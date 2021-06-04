
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.PE.Debug;
using AsmResolver.PE.DotNet.Builder;
using Origami.Runtime;

namespace Origami.Packers
{
    public class DebugDirPacker : Packer
    {

        private readonly ModuleDefinition _stubModule;

        public DebugDirPacker(byte[] payload, string outputPath) : base(payload, outputPath)
        {
            _stubModule = CreateStub(ModuleDefinition.FromBytes(payload));
        }

        public override void Execute()
        {
            InjectLoader(_stubModule, typeof(DebugDirLoader));
            var peImage = _stubModule.ToPEImage();
            peImage.DebugData.Clear();
            var segment = new DebugDataEntry(new CustomDebugDataSegment(DebugDataType.Unknown,
                new DataSegment(Payload.Compress(Name))));
            peImage.DebugData.Add(segment);

            var fileBuilder = new ManagedPEFileBuilder();
            var file = fileBuilder.CreateFile(peImage);
            file.Write(OutputPath);
        }
        
    }
}