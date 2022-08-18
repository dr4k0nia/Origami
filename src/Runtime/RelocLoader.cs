using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace Origami.Runtime
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal unsafe class RelocLoader
    {
        private static void Main(string[] args)
        {
            // Placeholder VirtualAddress to the payload
            byte* basePtr = (byte*) 6969696969L;

            // Placeholder size of the payload
            byte[] buffer = new byte[0x1337c0de];

            byte* key = (basePtr + buffer.Length - 64);

            fixed (byte* rawData = &buffer[0])
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    *(long*) (rawData + i) = *(long*) (basePtr + i) ^ (key[i % 64] * 0x0101010101010101);
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
