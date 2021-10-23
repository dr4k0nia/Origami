# <img width="64" height="64" valign="bottom" src="https://maxcdn.icons8.com/Color/PNG/512/Cultures/origami-512.png">Origami 
**Packer compressing .net assemblies, (ab)using the PE format for data storage**

## Usage

       Origami.exe <file>
       Origami.exe <file> <mode>
#### Available modes:
> **-dbg**
Use PE headers debug directory for data storage

> **-pes** Use additional PE Section (.origami) for data storage

## How it works

The assembly supplied to origami will be compressed and encrypted with a simple xor operation, the encrypted and compressed data (payload) will be inserted into a stub executable which will invoke its payload on runtime. Depending on the mode chosen the payload will either be stored in an additional pe section called .origami or in the debug directory of the stub.

For a detailed explanation of the stub code check out [my blog post](https://dr4k0nia.github.io/dotnet/coding/2021/06/24/Writing-a-Packer.html)

## Known issues
- **Incompatible with Fody.Costura** and everything else that relies on methods called in the global constructor
- No .NET Core support *(working on the issue)*

## Dependencies
- [AsmResolver](https://github.com/Washi1337/AsmResolver) by Washi

*Logo by [icons8](https://icons8.com)*
