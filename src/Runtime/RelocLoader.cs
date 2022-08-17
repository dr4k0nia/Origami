using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Origami.Runtime
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal unsafe class RelocLoader
    {
        private static void Main(string[] args)
        {
            // Call GetHINSTANCE() to obtain a handle to our module
            byte* basePtr = (byte*) 6969696969L;

            byte[] buffer = new byte[0x1337c0de];
                    //basePtr += 0x420c0de;
                    fixed (byte* p = &buffer[0])
                    {
                        // for (int i = 0; i < buffer.Length; i++)
                        // {
                        //     *(p + i) = (byte)(*(basePtr + i) ^ name[i % name.Length]);
                        // }
                        byte[] key =
                            Encoding.UTF8.GetBytes(@"f(!^nE34tN^$C[8t-f2CSP=M9f2Lg;pqL](UVC]8yv~4J{Xm6c\9Jf+3mQ-P:;x!");

                        Decrypt(basePtr, p, buffer.Length, key);
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

        private static void Decrypt(byte* section, byte* buffer, int length, byte[] key)
        {
            int* S = stackalloc int[256];

            int* T = stackalloc int[256];

            for (int _ = 0; _ < 256; _++)
                S[_] = _;

            fixed (byte* ptr = &key[0])
            {
                if (key.Length == 256)
                {
                    Buffer.MemoryCopy(ptr, T, 256, 256);
                }
                else
                {
                    for (int _ = 0; _ < 256; _++)
                        T[_] = ptr[_ % key.Length];
                }
            }

            int i;
            int j = 0;
            for (i = 0; i < 256; i++)
            {
                j = (j + S[i] + T[i]) % 256;

                // Deconstruction => swapping the values of S[i] and S[j]
                (S[i], S[j]) = (S[j], S[i]);
            }

            i = j = 0;
            for (int round = 0; round < length; round++)
            {
                i = (i + 1) % 256;

                j = (j + S[i]) % 256;

                // Deconstruction => swapping the values of S[i] and S[j]
                (S[i], S[j]) = (S[j], S[i]);

                int K = S[(S[i] + S[j]) % 256];

                buffer[round] = (byte) (*(section + round) ^ K);
            }
        }
    }
}
