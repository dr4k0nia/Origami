using System;
using System.IO;
using Origami.Packers;

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

            // Prepare initialization parameters payloadData that will get packed, and output path of packed file.
            byte[] payloadData = File.ReadAllBytes(file);
            string outputPath = file.Insert(file.Length - 4, "_origami");

            IPacker packer;
            if (args.Length > 1)
            {
                packer = args[1] switch
                {
                    "-dbg" => new RelocPacker(Mode.DebugDataEntry, payloadData, outputPath),
                    "-pes" => new RelocPacker(Mode.PESection, payloadData, outputPath),
                    _ => throw new InvalidDataException(
                        "Invalid mode argument: Available modes:\n-pes: Uses additional PE section for the payload data\n-dbg: Uses PE Debug Directory for the payload data")
                };
            }
            else
            {
                packer = new RelocPacker(Mode.PESection, payloadData, outputPath);
            }

            // Run packer
            packer.Execute();


            Console.WriteLine("Saving packed module: {0}", outputPath);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Finished");

            Console.ReadKey();
        }
    }
}
