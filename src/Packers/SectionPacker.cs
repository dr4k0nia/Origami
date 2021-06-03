using System.IO;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.PE.File;
using AsmResolver.PE.File.Headers;
using Origami.Runtime;

namespace Origami.Packers
{
    public sealed class SectionPacker : Packer, IPacker
    {
        private readonly ModuleDefinition _stubModule;

        private readonly byte[] _payload;

        private readonly string _outputPath;

        public SectionPacker(byte[] originBinary, string outputPath)
        {
            _payload = originBinary;
            _outputPath = outputPath;
            _stubModule = CreateStub(ModuleDefinition.FromBytes(originBinary));
        }

        public void Execute()
        {
            InjectLoader(_stubModule, typeof(PeSectionLoader));
            
            var output = new MemoryStream();
            _stubModule.Write(output);
            var peFile = PEFile.FromBytes(output.ToArray());
            var section = new PESection(".origami",
                SectionFlags.MemoryRead | SectionFlags.MemoryWrite | SectionFlags.ContentUninitializedData, new DataSegment(_payload.Compress(".origami")));
            peFile.Sections.Add(section);
            peFile.Write(_outputPath);
        }
    }
}