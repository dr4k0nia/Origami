using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;

namespace Origami
{
    public static class Utils
    {
        public static bool IsExecutable( this ModuleDefMD module )
        {
            return module.Kind == ModuleKind.Windows || module.Kind == ModuleKind.Console;
        }

        public static byte[] Compress( this byte[] data )
        {
            using ( var mStream = new MemoryStream() )
            {
                using ( var dStream = new DeflateStream( mStream, CompressionLevel.Optimal ) )
                    dStream.Write( data, 0, data.Length );
                return mStream.ToArray();
            }
        }

        public static ModuleDefUser GetStub( this ModuleDef originModule )
        {
            var stubModule =
                new ModuleDefUser( originModule.Name, originModule.Mvid, originModule.CorLibTypes.AssemblyRef );

            originModule.Assembly.Modules.Insert( 0, stubModule );
            ImportAssemblyTypeReferences( originModule, stubModule );

            stubModule.Characteristics = originModule.Characteristics;
            stubModule.Cor20HeaderFlags = originModule.Cor20HeaderFlags;
            stubModule.Cor20HeaderRuntimeVersion = originModule.Cor20HeaderRuntimeVersion;
            stubModule.DllCharacteristics = originModule.DllCharacteristics;
            stubModule.EncBaseId = originModule.EncBaseId;
            stubModule.EncId = originModule.EncId;
            stubModule.Generation = originModule.Generation;
            stubModule.Kind = originModule.Kind;
            stubModule.Machine = originModule.Machine;
            stubModule.TablesHeaderVersion = originModule.TablesHeaderVersion;
            stubModule.Win32Resources = originModule.Win32Resources;
            stubModule.RuntimeVersion = originModule.RuntimeVersion;
            stubModule.Is32BitRequired = originModule.Is32BitRequired;
            stubModule.Is32BitPreferred = originModule.Is32BitPreferred;

            return stubModule;
        }

        //Taken from ConfuserEx (Compressor)
        private static void ImportAssemblyTypeReferences( ModuleDef originModule, ModuleDef stubModule )
        {
            var assembly = stubModule.Assembly;
            foreach ( var ca in assembly.CustomAttributes.Where( ca => ca.AttributeType.Scope == originModule ) )
                ca.Constructor = (ICustomAttributeType) stubModule.Import( ca.Constructor );
            foreach ( var ca in assembly.DeclSecurities.SelectMany( declSec => declSec.CustomAttributes ) )
                if ( ca.AttributeType.Scope == originModule )
                    ca.Constructor = (ICustomAttributeType) stubModule.Import( ca.Constructor );
        }
    }
}