using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Origami
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static unsafe class Loader
    {
        #region File Header Structures

        // Grabbed the following definition from http://www.pinvoke.net/default.aspx/Structures/IMAGE_SECTION_HEADER.html

        [StructLayout(LayoutKind.Explicit)]
        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        private struct IMAGE_SECTION_HEADER
        {
            [FieldOffset(0)] public fixed byte Name[8];
            [FieldOffset(12)] public uint VirtualAddress;
            [FieldOffset(16)] public uint SizeOfRawData;
            [FieldOffset(36)] private uint Characteristics;
        }

        #endregion File Header Structures

        #region Parsing

        /// <summary>
        /// Reading from unmanaged memory pointer address.
        /// </summary>
        /// <param name="ptr"></param>
        private static void Initialize(byte* ptr)
        {
            long index = 0;
            // Reading e_lfanew from the dos header
            //uint e_lfanew = *(uint*) (ptr + 0x3C);
            ptr += *(uint*) (ptr + 0x3C);

            // Reading NumberOfSections the file header
          
            ushort NumberOfSections = *(ushort*) (ptr + 0x6);

            // Check optional header size and add it to index
            ushort optHeaderSize = *(ushort*)(ptr + 0x14);
            
            index += 0x18 + optHeaderSize;

            // Read section headers
            ImageSectionHeaders = new IMAGE_SECTION_HEADER[NumberOfSections];
            for (int headerNo = 0; headerNo < ImageSectionHeaders.Length; headerNo++)
            {
                ImageSectionHeaders[headerNo] = *(IMAGE_SECTION_HEADER*) (ptr + index);
                index += sizeof(IMAGE_SECTION_HEADER);
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Image Section headers. Number of sections is in the file header.
        /// </summary>
        private static IMAGE_SECTION_HEADER[] ImageSectionHeaders { get; set; }
        
        private static readonly delegate*<byte[], Assembly> LoadAssembly = &Assembly.Load;
        private static readonly delegate*<Assembly> GetCaller = &Assembly.GetCallingAssembly;
        private static readonly delegate*<Module, IntPtr> GetHandle = &Marshal.GetHINSTANCE;

        #endregion Properties


        private static void Main(string[] args)
        {
            // Call GetHINSTANCE() to obtain a handle to our module
            var ptr = (byte*)GetHandle(GetCaller().ManifestModule);
            // Parse PE header using the before obtained module handle
            Initialize(ptr);
            // Get name of EntryPoint
            string name = GetCaller().EntryPoint.Name;
            
            
            // Iterate trough all PE sections
            foreach (var section in ImageSectionHeaders)
            {
                // Check if pe section name matches first 8 bytes of stub EntryPoint
                bool flag = true;
                for (int h = 0; h < 8; h++)
                    if (name[h] != *(section.Name + h))
                        flag = false;

                if (flag)
                {
                    // Initialize buffer using size of raw data
                    // Copy data from pe section into buffer and simultaneously (un)xor it
                    byte[] buffer = new byte[section.SizeOfRawData];
                    fixed (byte* p = &buffer[0])
                    {
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            *(p + i) = (byte) (*(ptr + section.VirtualAddress + i) ^ name[i % name.Length]);
                        }
                    }
                    
                    // Decompress data from the buffer
                    using var origin = new MemoryStream(buffer);
                    using var destination = new MemoryStream();
                    using var deflateStream = new DeflateStream(origin, CompressionMode.Decompress);
                    deflateStream.CopyTo(destination);
                    
                    // Load assembly using the previously decompressed data
                    var asm = LoadAssembly(destination.GetBuffer());

                    if (asm.EntryPoint != null)
                    {
                        MethodBase entryPoint = asm.EntryPoint;
                        object[] parameters = new object[entryPoint.GetParameters().Length];
                        if (parameters.Length != 0)
                            parameters[0] = args;
                        entryPoint.Invoke(null, parameters);
                    }
                    else
                        throw new EntryPointNotFoundException("Origami could not find a valid EntryPoint to invoke");
                }
            }
        }
    }
}
