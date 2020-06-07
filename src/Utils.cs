using System;
using System.IO;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;

namespace Origami
{
    public static class Utils
    {
        public static bool IsExe( ModuleDefMD module )
        {
            return module.Kind == ModuleKind.Windows || module.Kind == ModuleKind.Console;
        }

        public static string GetDnlibPath()
        {
            return ( from asm in AppDomain.CurrentDomain.GetAssemblies() from m in asm.Modules where m.Name == "dnlib.dll" select m.Assembly.Location ).FirstOrDefault();
        }
    }
}