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

        // Grabbed the following definition from http://www.pinvoke.net/default.aspx/Structures/IMAGE_SECTION_HEADER.html

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

        #endregion File Header Structures

        #region Parsing

        /// <summary>
        /// Reading from unmanaged memory pointer address.
        /// </summary>
        /// <param name="memPtr"></param>
        /// <param name="index"></param>
        private static void Load( IntPtr memPtr)
        {
            long index = 0;
            // Reading e_lfanew from the dos header
            uint e_lfanew = (uint) Marshal.ReadInt16( new IntPtr( memPtr.ToInt64() + 0x3C ) );
            index += e_lfanew + 4;

            // Reading NumberOfSections the file header
            ushort NumberOfSections = (ushort) Marshal.ReadInt16( new IntPtr( memPtr.ToInt64() + index + 2 ) );
            index += NumberOfSections + 16;

            // See the optional header magic to determine 32-bit vs 64-bit
            short optMagic = Marshal.ReadInt16( new IntPtr( memPtr.ToInt64() + index ) );
            
            if ( optMagic != 0x20b ) // IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20b
                index += 0xE0; // size of IMAGE_OPTIONAL_HEADER32
            else
                index += 0xF0; // size of IMAGE_OPTIONAL_HEADER64

            // Read section headers
            ImageSectionHeaders = new IMAGE_SECTION_HEADER[NumberOfSections];
            for ( int headerNo = 0; headerNo < ImageSectionHeaders.Length; headerNo++ )
            {
                ImageSectionHeaders[headerNo] = (IMAGE_SECTION_HEADER) Marshal.PtrToStructure( new IntPtr( memPtr.ToInt64() + index ),
                    typeof(IMAGE_SECTION_HEADER) );
                index += Marshal.SizeOf( typeof(IMAGE_SECTION_HEADER) );
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Image Section headers. Number of sections is in the file header.
        /// </summary>
        private static IMAGE_SECTION_HEADER[] ImageSectionHeaders { get; set; }

        #endregion Properties


        [STAThread]
        private static void Main( string[] args )
        {
            var ptr = Marshal.GetHINSTANCE( typeof(Loader).Assembly.ManifestModule );
            Load( ptr);

            foreach ( var section in ImageSectionHeaders )
            {
                if ( section.Section == ".origami" )
                {
                    //Initialize destination array with size of raw data
                    byte[] destination = new byte[section.SizeOfRawData];

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