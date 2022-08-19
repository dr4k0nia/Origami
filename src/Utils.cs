using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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

        public static uint GetRandomTimestamp()
        {
            var rnd = new Random();

            var from = new DateTime(DateTime.Today.Year - 5, 1, 1);
            var to = DateTime.Today;
            var range = to - from;

            var randTimeSpan = new TimeSpan((long) (rnd.NextDouble() * range.Ticks));

            return (uint) (from + randTimeSpan).Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }
    }
}
