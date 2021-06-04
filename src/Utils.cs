using System.IO;
using System.IO.Compression;
using System.Linq;
using AsmResolver.DotNet;

namespace Origami
{
    public static class Utils
    {
        private static byte[] Xor(this byte[] data, string name)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= (byte) name[i % name.Length];
            }

            return data;
        }

        public static byte[] Compress(this byte[] data, string key)
        {
            using var mStream = new MemoryStream();
            using (var dStream = new DeflateStream(mStream, CompressionLevel.Optimal))
                dStream.Write(data, 0, data.Length);
            return mStream.ToArray().Xor(key);
        }
        
        public static void ImportAssemblyTypeReferences(this ModuleDefinition target, ModuleDefinition origin)
        {
            var assembly = origin.Assembly;
            var importer = new ReferenceImporter(target);
            foreach (var ca in assembly.CustomAttributes.Where(ca => ca.Constructor.Module == origin))
                ca.Constructor = (ICustomAttributeType) importer.ImportMethod(ca.Constructor);
        }
    }
}