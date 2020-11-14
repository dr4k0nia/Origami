using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Origami
{
    [SuppressMessage( "ReSharper", "InconsistentNaming" )]
    internal static class Loader
    {
        #region File Header Structures

        [StructLayout( LayoutKind.Explicit )]
        private readonly struct IMAGE_DOS_HEADER
        {
            [FieldOffset( 60 )] public readonly uint e_lfanew;
        }

        [StructLayout( LayoutKind.Sequential )]
        private readonly struct IMAGE_DATA_DIRECTORY
        {
            private readonly uint VirtualAddress;
            private readonly uint Size;
        }

        [StructLayout( LayoutKind.Explicit )]
        private readonly struct IMAGE_OPTIONAL_HEADER32
        {
            [FieldOffset( 0 )] private readonly ushort Magic;
            [FieldOffset( 216 )] private readonly IMAGE_DATA_DIRECTORY Reserved;
        }

        [StructLayout( LayoutKind.Explicit )]
        private readonly struct IMAGE_OPTIONAL_HEADER64
        {
            [FieldOffset( 0 )] private readonly ushort Magic;
            [FieldOffset( 216 )] private readonly IMAGE_DATA_DIRECTORY Reserved;
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

            [FieldOffset( 12 )] public readonly uint VirtualAddress;
            [FieldOffset( 16 )] public readonly uint SizeOfRawData;
            [FieldOffset( 20 )] private readonly uint PointerToRawData;
            [FieldOffset( 36 )] private readonly uint Characteristics;

            public string Section => new string( Name );
        }

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
            long startIndex = index;
            // Reading the dos header
            dosHeader = FromMemoryPtr<IMAGE_DOS_HEADER>( memPtr, ref index );
            index = startIndex + dosHeader.e_lfanew + 4;

            // Reading the file header
            fileHeader = FromMemoryPtr<IMAGE_FILE_HEADER>( memPtr, ref index );

            // See the optional header magic to determine 32-bit vs 64-bit
            short optMagic = Marshal.ReadInt16( new IntPtr( memPtr.ToInt64() + index ) );
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