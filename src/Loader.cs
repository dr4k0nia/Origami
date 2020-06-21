using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using dnlib.DotNet;
using dnlib.PE;

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
            var assemblyData = File.ReadAllBytes( assemblyLocation );

            var mod = ModuleDefMD.Load( assemblyData );
            var peImage = mod.Metadata.PEImage;

            foreach ( var section in peImage.ImageSectionHeaders )
            {
                if ( section.DisplayName != ".origami" ) continue;

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
                    throw new EntryPointNotFoundException( "Origami could not find a valid EntryPoint to invoke" );
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
            using ( var origin = new MemoryStream(data) )
            using ( var destination = new MemoryStream() )
            using ( var deflateStream = new DeflateStream( origin, CompressionMode.Decompress ) )
            {
                deflateStream.CopyTo( destination );
                return destination.ToArray();
            }
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

            var assemblyData = File.ReadAllBytes( assemblyLocation );

            var mod = ModuleDefMD.Load( assemblyData );
            var peImage = mod.Metadata.PEImage;

            foreach ( var section in peImage.ImageSectionHeaders )
            {
                if ( section.DisplayName != ".origami" ) continue;

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
                    throw new EntryPointNotFoundException( "Origami could not find a valid EntryPoint to invoke" );
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
            using ( var origin = new MemoryStream(data) )
            using ( var destination = new MemoryStream() )
            using ( var deflateStream = new DeflateStream( origin, CompressionMode.Decompress ) )
            {
                deflateStream.CopyTo( destination );
                return destination.ToArray();
            }
        }
    }
}