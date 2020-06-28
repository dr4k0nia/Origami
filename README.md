# Origami
**Packer compressing .net assemblies, storing their contents inside of PE sections and invoking them on runtime**

## Usage

       Origami.exe <file>

 The input file will be "cloned", the cloned stub contains the Origami Loader and an additional PE section called ".origami". The Origami Loader will extract, decompress and invoke the original assembly on runtime.

 ### Compatibility
- Origami supports .net framework executables

### Example


Structure of the Origami stub in dnSpy
![Example](https://i.imgur.com/t8McDSg.png)

#### Known issues:
- **Incompatible with Fody.Costura** and everything else that relies on methods called in the global constructor

## Dependencies
- [dnlib](https://github.com/0xd4d/dnlib) by 0xd4d (nuget package)
