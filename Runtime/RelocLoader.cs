using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Reflection;

namespace Runtime
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static unsafe class RelocLoader
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Placeholder for relocated VirtualAddress
            byte* basePtr = (byte*) 6969696969L;

            // Placeholder for payload size
            byte[] buffer = new byte[0x1337c0de];

            string key = typeof(RelocLoader).Assembly.EntryPoint.Name;

            fixed (byte* rawData = &buffer[0])
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    *(rawData + i) = (byte) (*(basePtr + i) ^ key[i % key.Length]);
                }
            }

            // Decompress data from the buffer
            using var origin = new MemoryStream(buffer);
            using var destination = new MemoryStream();
            using var deflateStream = new DeflateStream(origin, CompressionMode.Decompress);
            deflateStream.CopyTo(destination);

            // Load assembly using the previously decompressed data
            var asm = Assembly.Load(destination.GetBuffer());

            MethodBase entryPoint = asm.EntryPoint ??
                                    throw new EntryPointNotFoundException(
                                        "Origami could not find a valid EntryPoint to invoke");

            object[] parameters = new object[entryPoint.GetParameters().Length];
            if (parameters.Length != 0)
                parameters[0] = args;
            entryPoint.Invoke(null, parameters);
        }
    }
}
