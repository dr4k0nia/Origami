using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using dnlib.DotNet;

namespace Origami
{
    internal static class Loader
    {
        private static void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }


        [STAThread]
        private static void Main( string[] args )
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;

            var stream = new StreamReader( assemblyLocation ).BaseStream;
            var binaryReader = new BinaryReader( stream );
            var file = binaryReader.ReadBytes( File.ReadAllBytes( assemblyLocation ).Length );

            var mod = ModuleDefMD.Load( file );
            var peImage = mod.Metadata.PEImage;

            for ( var i = 0; i < peImage.ImageSectionHeaders.Count; i++ )
            {
                var section = peImage.ImageSectionHeaders[i];

                if ( section.DisplayName == ".origami" )
                {
                    var reader = peImage.CreateReader( section.VirtualAddress, section.SizeOfRawData );

                    var data = Decompress( reader.ToArray() );

                    var asm = Assembly.Load( data );

                    if ( asm.EntryPoint != null )
                    {
                        MethodBase entryPoint = asm.EntryPoint;
                        var parameters = new object[entryPoint.GetParameters().Length];
                        if ( parameters.Length != 0 )
                            parameters[0] = args;
                        entryPoint.Invoke( null, parameters );
                    }
                    else
                        throw new Exception( "Origami could not find a valid EntryPoint to invoke" );
                }
            }
        }

        private static Assembly Resolve( object sender, ResolveEventArgs e )
        {
            //System.Reflection.AssemblyName requestedAssemblyName = new System.Reflection.AssemblyName( e.Name );
            var text = new AssemblyName( e.Name ).Name + ".dll.compressed";

            using ( var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream( text ) )
            {
                var assemblyData = new byte[stream.Length];

                stream.Read( assemblyData, 0, assemblyData.Length );

                return Assembly.Load( Decompress( assemblyData ) );
            }
        }

        private static byte[] Decompress( byte[] data )
        {
            var destination = new MemoryStream();
            using ( var deflateStream = new DeflateStream( new MemoryStream( data ), CompressionMode.Decompress ) )
            {
                deflateStream.CopyTo( destination );
            }

            return destination.ToArray();
        }
    }


    internal static class CallLoader
    {
        [STAThread]
        private static void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;

            Load();
        }

        private static void Load()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;

            var stream = new StreamReader( assemblyLocation ).BaseStream;
            var binaryReader = new BinaryReader( stream );
            var file = binaryReader.ReadBytes( File.ReadAllBytes( assemblyLocation ).Length );

            var mod = ModuleDefMD.Load( file );
            var peImage = mod.Metadata.PEImage;

            for ( var i = 0; i < peImage.ImageSectionHeaders.Count; i++ )
            {
                var section = peImage.ImageSectionHeaders[i];

                if ( section.DisplayName == ".origami" )
                {
                    var reader = peImage.CreateReader( section.VirtualAddress, section.SizeOfRawData );

                    var data = Decompress( reader.ToArray() );

                    var asm = Assembly.Load( data );

                    if ( asm.EntryPoint != null )
                    {
                        MethodBase entryPoint = asm.EntryPoint;
                        var parameters = new object[entryPoint.GetParameters().Length];
                        if ( parameters.Length != 0 )
                            parameters[0] = new string[0];
                        entryPoint.Invoke( null, parameters );
                    }
                    else
                        throw new Exception( "DependencyResolver could not invoke dependency" );
                }
            }
        }

        private static Assembly Resolve( object sender, ResolveEventArgs e )
        {
            var requestedAssemblyName = new AssemblyName( e.Name );
            var text = requestedAssemblyName.Name + ".dll.compressed";

            using ( var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream( text ) )
            {
                var assemblyData = new byte[stream.Length];

                stream.Read( assemblyData, 0, assemblyData.Length );

                return Assembly.Load( Decompress( assemblyData ) );
            }
        }

        private static byte[] Decompress( byte[] data )
        {
            var destination = new MemoryStream();
            using ( var deflateStream = new DeflateStream( new MemoryStream( data ), CompressionMode.Decompress ) )
            {
                deflateStream.CopyTo( destination );
            }

            return destination.ToArray();
        }
    }
}