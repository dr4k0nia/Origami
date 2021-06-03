using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using AsmResolver.DotNet;
using AsmResolver.PE.File;
using Origami.Packers;
using Origami.Runtime;

namespace Origami
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Origami by drakonia - https://github.com/dr4k0nia/Origami \r\n");
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: Origami.exe <file> <mode> or Origami.exe <file>");
                Console.WriteLine(
                    "Available modes:\n-pes: Uses additional PE section for the payload data\n-dbg: Uses PE Debug Directory for the payload data\n-mds: Uses additional metadata stream for the payload data");
                Console.WriteLine("Default mode: -pes");
                Console.ReadKey();
                return;
            }

            string file = args[0];

            if (!File.Exists(file))
                throw new FileNotFoundException($"Could not find file: {file}");

            if (!file.Contains(".exe"))
                throw new InvalidDataException("Origami only supports .net executable files");

            if (args.Length > 1)
            {
                IPacker packer;
                switch (args[1])
                {
                    case "-dbg":
                        packer = new DebugDirPacker(File.ReadAllBytes(file), file.Insert(file.Length - 4, "_origami"));
                        packer.Execute();
                        break;
                    case "-pes":
                        packer = new SectionPacker(File.ReadAllBytes(file), file.Insert(file.Length - 4, "_origami"));
                        packer.Execute();
                        break;
                    default:
                        throw new InvalidDataException(
                            "Invalid mode argument: Available modes:\n-pes: Uses additional PE section for the payload data\n-dbg: Uses PE Debug Directory for the payload data");
                }
            }
            else
            {
                var packer = new DebugDirPacker(File.ReadAllBytes(file), file.Insert(file.Length - 4, "_origami"));
                packer.Execute();
            }


            Console.WriteLine("Saving module...");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Finished");

            Console.ReadKey();
        }
    }
}