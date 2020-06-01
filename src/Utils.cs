using System;
using dnlib.DotNet;

namespace Origami
{
    public static class Utils
    {
        public static bool IsExe( ModuleDefMD module )
        {
            if ( module.Kind == ModuleKind.Windows || module.Kind == ModuleKind.Console )
                return true;
            return false;
        }
    }
}