using System.IO;
using System.IO.Compression;
using System.Linq;
using AsmResolver.DotNet;

namespace Origami
{
    public static class Utils
    {
        private static byte[] Xor(this byte[] data, byte[] key)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= key[i % key.Length];
            }

            return data;
        }

        public static byte[] PreparePayload(this byte[] data, byte[] key)
        {
            using var mStream = new MemoryStream();
            using (var dStream = new DeflateStream(mStream, CompressionLevel.Optimal))
                dStream.Write(data, 0, data.Length);

            byte[] result = new byte[mStream.Length + key.Length];
            mStream.ToArray().Xor(key).CopyTo(result, 0);
            key.CopyTo(result, result.Length - key.Length);

            return result;
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
