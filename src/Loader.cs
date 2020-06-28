using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Origami
{
    internal static class Loader
    {
        #region File Header Structures

        [StructLayout( LayoutKind.Sequential )]
        private readonly struct IMAGE_DOS_HEADER
        {
            // DOS .EXE header
            private readonly ushort e_magic; // Magic number
            private readonly ushort e_cblp; // Bytes on last page of file
            private readonly ushort e_cp; // Pages in file
            private readonly ushort e_crlc; // Relocations
            private readonly ushort e_cparhdr; // Size of header in paragraphs
            private readonly ushort e_minalloc; // Minimum extra paragraphs needed
            private readonly ushort e_maxalloc; // Maximum extra paragraphs needed
            private readonly ushort e_ss; // Initial (relative) SS value
            private readonly ushort e_sp; // Initial SP value
            private readonly ushort e_csum; // Checksum
            private readonly ushort e_ip; // Initial IP value
            private readonly ushort e_cs; // Initial (relative) CS value
            private readonly ushort e_lfarlc; // File address of relocation table
            private readonly ushort e_ovno; // Overlay number
            private readonly ushort e_res_0; // Reserved words
            private readonly ushort e_res_1; // Reserved words
            private readonly ushort e_res_2; // Reserved words
            private readonly ushort e_res_3; // Reserved words
            private readonly ushort e_oemid; // OEM identifier (for e_oeminfo)
            private readonly ushort e_oeminfo; // OEM information; e_oemid specific
            private readonly ushort e_res2_0; // Reserved words
            private readonly ushort e_res2_1; // Reserved words
            private readonly ushort e_res2_2; // Reserved words
            private readonly ushort e_res2_3; // Reserved words
            private readonly ushort e_res2_4; // Reserved words
            private readonly ushort e_res2_5; // Reserved words
            private readonly ushort e_res2_6; // Reserved words
            private readonly ushort e_res2_7; // Reserved words
            private readonly ushort e_res2_8; // Reserved words
            private readonly ushort e_res2_9; // Reserved words
            public readonly uint e_lfanew; // File address of new exe header
        }

        [StructLayout( LayoutKind.Sequential )]
        private readonly struct IMAGE_DATA_DIRECTORY
        {
            private readonly uint VirtualAddress;
            private readonly uint Size;
        }

        [StructLayout( LayoutKind.Sequential, Pack = 1 )]
        private readonly struct IMAGE_OPTIONAL_HEADER32
        {
            private readonly ushort Magic;
            private readonly byte MajorLinkerVersion;
            private readonly byte MinorLinkerVersion;
            private readonly uint SizeOfCode;
            private readonly uint SizeOfInitializedData;
            private readonly uint SizeOfUninitializedData;
            private readonly uint AddressOfEntryPoint;
            private readonly uint BaseOfCode;
            private readonly uint BaseOfData;
            private readonly uint ImageBase;
            private readonly uint SectionAlignment;
            private readonly uint FileAlignment;
            private readonly ushort MajorOperatingSystemVersion;
            private readonly ushort MinorOperatingSystemVersion;
            private readonly ushort MajorImageVersion;
            private readonly ushort MinorImageVersion;
            private readonly ushort MajorSubsystemVersion;
            private readonly ushort MinorSubsystemVersion;
            private readonly uint Win32VersionValue;
            private readonly uint SizeOfImage;
            private readonly uint SizeOfHeaders;
            private readonly uint CheckSum;
            private readonly ushort Subsystem;
            private readonly ushort DllCharacteristics;
            private readonly uint SizeOfStackReserve;
            private readonly uint SizeOfStackCommit;
            private readonly uint SizeOfHeapReserve;
            private readonly uint SizeOfHeapCommit;
            private readonly uint LoaderFlags;
            private readonly uint NumberOfRvaAndSizes;

            private readonly IMAGE_DATA_DIRECTORY ExportTable;
            private readonly IMAGE_DATA_DIRECTORY ImportTable;
            private readonly IMAGE_DATA_DIRECTORY ResourceTable;
            private readonly IMAGE_DATA_DIRECTORY ExceptionTable;
            private readonly IMAGE_DATA_DIRECTORY CertificateTable;
            private readonly IMAGE_DATA_DIRECTORY BaseRelocationTable;
            private readonly IMAGE_DATA_DIRECTORY Debug;
            private readonly IMAGE_DATA_DIRECTORY Architecture;
            private readonly IMAGE_DATA_DIRECTORY GlobalPtr;
            private readonly IMAGE_DATA_DIRECTORY TLSTable;
            private readonly IMAGE_DATA_DIRECTORY LoadConfigTable;
            private readonly IMAGE_DATA_DIRECTORY BoundImport;
            private readonly IMAGE_DATA_DIRECTORY IAT;
            private readonly IMAGE_DATA_DIRECTORY DelayImportDescriptor;
            private readonly IMAGE_DATA_DIRECTORY CLRRuntimeHeader;
            private readonly IMAGE_DATA_DIRECTORY Reserved;
        }

        [StructLayout( LayoutKind.Sequential, Pack = 1 )]
        private readonly struct IMAGE_OPTIONAL_HEADER64
        {
            private readonly ushort Magic;
            private readonly byte MajorLinkerVersion;
            private readonly byte MinorLinkerVersion;
            private readonly uint SizeOfCode;
            private readonly uint SizeOfInitializedData;
            private readonly uint SizeOfUninitializedData;
            private readonly uint AddressOfEntryPoint;
            private readonly uint BaseOfCode;
            private readonly ulong ImageBase;
            private readonly uint SectionAlignment;
            private readonly uint FileAlignment;
            private readonly ushort MajorOperatingSystemVersion;
            private readonly ushort MinorOperatingSystemVersion;
            private readonly ushort MajorImageVersion;
            private readonly ushort MinorImageVersion;
            private readonly ushort MajorSubsystemVersion;
            private readonly ushort MinorSubsystemVersion;
            private readonly uint Win32VersionValue;
            private readonly uint SizeOfImage;
            private readonly uint SizeOfHeaders;
            private readonly uint CheckSum;
            private readonly ushort Subsystem;
            private readonly ushort DllCharacteristics;
            private readonly ulong SizeOfStackReserve;
            private readonly ulong SizeOfStackCommit;
            private readonly ulong SizeOfHeapReserve;
            private readonly ulong SizeOfHeapCommit;
            private readonly uint LoaderFlags;
            private readonly uint NumberOfRvaAndSizes;

            private readonly IMAGE_DATA_DIRECTORY ExportTable;
            private readonly IMAGE_DATA_DIRECTORY ImportTable;
            private readonly IMAGE_DATA_DIRECTORY ResourceTable;
            private readonly IMAGE_DATA_DIRECTORY ExceptionTable;
            private readonly IMAGE_DATA_DIRECTORY CertificateTable;
            private readonly IMAGE_DATA_DIRECTORY BaseRelocationTable;
            private readonly IMAGE_DATA_DIRECTORY Debug;
            private readonly IMAGE_DATA_DIRECTORY Architecture;
            private readonly IMAGE_DATA_DIRECTORY GlobalPtr;
            private readonly IMAGE_DATA_DIRECTORY TLSTable;
            private readonly IMAGE_DATA_DIRECTORY LoadConfigTable;
            private readonly IMAGE_DATA_DIRECTORY BoundImport;
            private readonly IMAGE_DATA_DIRECTORY IAT;
            private readonly IMAGE_DATA_DIRECTORY DelayImportDescriptor;
            private readonly IMAGE_DATA_DIRECTORY CLRRuntimeHeader;
            private readonly IMAGE_DATA_DIRECTORY Reserved;
        }

        [StructLayout( LayoutKind.Sequential, Pack = 1 )]
        private readonly struct IMAGE_FILE_HEADER
        {
            private readonly ushort Machine;
            public readonly ushort NumberOfSections;
            private readonly uint TimeDateStamp;
            private readonly uint PointerToSymbolTable;
            private readonly uint NumberOfSymbols;
            private readonly ushort SizeOfOptionalHeader;
            private readonly ushort Characteristics;
        }

        // Grabbed the following 2 definitions from http://www.pinvoke.net/default.aspx/Structures/IMAGE_SECTION_HEADER.html

        [StructLayout( LayoutKind.Explicit )]
        private readonly struct IMAGE_SECTION_HEADER
        {
            [FieldOffset( 0 )] [MarshalAs( UnmanagedType.ByValArray, SizeConst = 8 )]
            private readonly char[] Name;

            [FieldOffset( 8 )] private readonly uint VirtualSize;
            [FieldOffset( 12 )] public readonly uint VirtualAddress;
            [FieldOffset( 16 )] public readonly uint SizeOfRawData;
            [FieldOffset( 20 )] private readonly uint PointerToRawData;
            [FieldOffset( 24 )] private readonly uint PointerToRelocations;
            [FieldOffset( 28 )] private readonly uint PointerToLinenumbers;
            [FieldOffset( 32 )] private readonly ushort NumberOfRelocations;
            [FieldOffset( 34 )] private readonly ushort NumberOfLinenumbers;
            [FieldOffset( 36 )] private readonly uint Characteristics;

            public string Section => new string( Name );
        }

        private const ushort IMAGE_NT_OPTIONAL_HDR32_MAGIC = 0x10b;
        private const ushort IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20b;

        #endregion File Header Structures

        #region Private Fields

        private static bool _is32bit;

        /// <summary>
        /// The DOS header
        /// </summary>
        private static IMAGE_DOS_HEADER dosHeader;

        /// <summary>
        /// The file header
        /// </summary>
        private static IMAGE_FILE_HEADER fileHeader;

        #endregion Private Fields

        #region Parsing

        /// <summary>
        /// Reading from unmanaged memory pointer address.
        /// </summary>
        /// <param name="memPtr"></param>
        /// <param name="index"></param>
        private static void Load( IntPtr memPtr, long index )
        {
            var startIndex = index;
            // Reading the dos header
            dosHeader = FromMemoryPtr<IMAGE_DOS_HEADER>( memPtr, ref index );
            index = startIndex + dosHeader.e_lfanew + 4;

            // Reading the file header
            fileHeader = FromMemoryPtr<IMAGE_FILE_HEADER>( memPtr, ref index );

            // See the optional header magic to determine 32-bit vs 64-bit
            var optMagic = Marshal.ReadInt16( new IntPtr( memPtr.ToInt64() + index ) );
            _is32bit = ( optMagic != IMAGE_NT_OPTIONAL_HDR64_MAGIC );

            if ( _is32bit )
                OptionalHeader32 = FromMemoryPtr<IMAGE_OPTIONAL_HEADER32>( memPtr, ref index );
            else
                OptionalHeader64 = FromMemoryPtr<IMAGE_OPTIONAL_HEADER64>( memPtr, ref index );

            // Read section headers
            ImageSectionHeaders = new IMAGE_SECTION_HEADER[fileHeader.NumberOfSections];
            for ( int headerNo = 0; headerNo < ImageSectionHeaders.Length; headerNo++ )
            {
                ImageSectionHeaders[headerNo] = FromMemoryPtr<IMAGE_SECTION_HEADER>( memPtr, ref index );
            }
        }

        /// <summary>
        /// Reading T from unmanaged memory pointer address.
        /// </summary>
        /// <param name="memPtr"></param>
        /// <param name="index"></param>
        private static T FromMemoryPtr<T>( IntPtr memPtr, ref long index )
        {
            var obj = (T) Marshal.PtrToStructure( new IntPtr( memPtr.ToInt64() + index ), typeof(T) );
            index += Marshal.SizeOf( typeof(T) );
            return obj;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the optional header
        /// </summary>
        private static IMAGE_OPTIONAL_HEADER32 OptionalHeader32 { get; set; }

        /// <summary>
        /// Gets the optional header
        /// </summary>
        private static IMAGE_OPTIONAL_HEADER64 OptionalHeader64 { get; set; }

        /// <summary>
        /// Image Section headers. Number of sections is in the file header.
        /// </summary>
        private static IMAGE_SECTION_HEADER[] ImageSectionHeaders { get; set; }

        #endregion Properties


        [STAThread]
        private static void Main( string[] args )
        {
            var ptr = Marshal.GetHINSTANCE( typeof(Loader).Assembly.ManifestModule );
            Load( ptr, 0 );

            foreach ( var section in ImageSectionHeaders )
            {
                if ( section.Section == ".origami" )
                {
                    //Initialize destination array with size of raw data
                    var destination = new byte[section.SizeOfRawData];

                    //Copy managed array from unmanaged heap
                    Marshal.Copy( ptr + (int) section.VirtualAddress, destination, 0, (int) section.SizeOfRawData );

                    var asm = Assembly.Load( Decompress( destination ) );

                    if ( asm.EntryPoint != null )
                    {
                        MethodBase entryPoint = asm.EntryPoint;
                        var parameters = new object[entryPoint.GetParameters().Length];
                        if ( parameters.Length != 0 )
                            parameters[0] = args;
                        entryPoint.Invoke( null, parameters );
                    }
                    else
                        throw new EntryPointNotFoundException( "Origami could not find a valid EntryPoint to invoke" );
                }
            }
        }

        private static byte[] Decompress( byte[] data )
        {
            using ( var origin = new MemoryStream( data ) )
            using ( var destination = new MemoryStream() )
            using ( var deflateStream = new DeflateStream( origin, CompressionMode.Decompress ) )
            {
                deflateStream.CopyTo( destination );
                return destination.ToArray();
            }
        }
    }
}