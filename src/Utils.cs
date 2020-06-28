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
    }
}