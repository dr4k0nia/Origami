using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Origami.Runtime
{
    internal unsafe class DebugDirLoader
    {
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static void Main(string[] args)
        {
            // Call GetHINSTANCE() to obtain a handle to our module
            byte* basePtr = (byte*) Marshal.GetHINSTANCE(Assembly.GetCallingAssembly().ManifestModule);

            // Parse PE header using the before obtained module handle
            // Reading e_lfanew from the dos header
            byte* ptr = basePtr + *(uint*) (basePtr + 0x3C);

            //index += 20; // index NumberOfSections + 3 x uint + 3x ushort + 2 from above

            // Check the optional header magic to determine 32-bit vs 64-bit
            short optMagic = *(short*) (ptr + 0x18);

            // 0x20b = IMAGE_NT_OPTIONAL_HDR64_MAGIC 
            uint DebugVirtualAddress = optMagic != 0x20b
                ? *(uint*) (ptr + 0xA8)
                : *(uint*) (ptr + 0xB8);

            basePtr += DebugVirtualAddress;
            uint SizeOfData = *(uint*) (basePtr + 0x10);
            uint AddressOfRawData = *(uint*) (basePtr + 0x14);
            basePtr -= DebugVirtualAddress;

            // Get name of EntryPoint
            string name = Assembly.GetCallingAssembly().EntryPoint.Name;

            // Initialize buffer using SizeOfData
            // Copy data from debug directory into buffer and simultaneously (un)xor it
            byte[] buffer = new byte[SizeOfData];
            basePtr += AddressOfRawData;
            fixed (byte* rawData = &buffer[0])
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    *(rawData + i) = (byte) (*(basePtr + i) ^ name[i % name.Length]);
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
            ;
            object[] parameters = new object[entryPoint.GetParameters().Length];
            if (parameters.Length != 0)
                parameters[0] = args;
            entryPoint.Invoke(null, parameters);
        }
    }
}