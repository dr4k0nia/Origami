
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.PE.Debug;
using AsmResolver.PE.DotNet.Builder;
using Origami.Runtime;

namespace Origami.Packers
{
    public class DebugDirPacker : Packer, IPacker
    {

        private readonly ModuleDefinition _stubModule;

        private readonly byte[] _payload;
        
        private readonly string _outputPath;

        public DebugDirPacker(byte[] originBinary, string outputPath)
        {
            _payload = originBinary;
            _outputPath = outputPath;
            _stubModule = CreateStub(ModuleDefinition.FromBytes(originBinary));
        }

        public void Execute()
        {
            InjectLoader(_stubModule, typeof(DebugDirLoader));
            var peImage = _stubModule.ToPEImage();
            peImage.DebugData.Clear();
            var segment = new DebugDataEntry(new CustomDebugDataSegment(DebugDataType.Unknown,
                new DataSegment(_payload.Compress(".origami"))));
            peImage.DebugData.Add(segment);

            var fileBuilder = new ManagedPEFileBuilder();
            var file = fileBuilder.CreateFile(peImage);
            file.Write(_outputPath);
        }
        
    }
}