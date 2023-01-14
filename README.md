# <img width="64" height="64" valign="bottom" src="https://img.icons8.com/color/96/000000/origami.png">Origami 
**Packer compressing .net assemblies, (ab)using the PE format for data storage**

## Usage

       Origami.exe <file>
       Origami.exe <file> <mode>
#### Available modes:
> **-dbg**
Use PE headers debug directory for data storage

> **-pes** Use additional PE Section (.origami) for data storage

## How it works

Origami takes an input module (payload) which gets compressed and encrypted. The payload is then inserted into a, newly created, stub module along with a runtime loader for payload extraction. Depending on the chosen mode the payload is either placed in a new section along side the stubs metadata or hidden in the debug data entries of the stub. The new loader uses a direct pointer (VirtualAddress) to the payloads location, instead of traversing the PE header at runtime. To make the direct access possible I utilize Base Relocations and a customized module building routine in AsmResolver.


Some improvements made in version 2:
- NET Core support
- Costura support
- Simplified loader


*This blog post is based on an [older release of origami](https://github.com/dr4k0nia/Origami/tree/parsing-runtime) which uses a different runtime and packing process. I will write an updated blog post when I find the time*
<br>
~For a detailed explanation of the stub code check out [my blog post](https://dr4k0nia.github.io/posts/Writing-a-Packer/)~


## Dependencies
- [AsmResolver](https://github.com/Washi1337/AsmResolver) by Washi

*Logo by [icons8](https://icons8.com)*
