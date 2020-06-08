# Origami
**POC Packer compressing .net assemblies, storing their contents inside of PE sections and invoking them on runtime**

## Usage

**Default:** The input file will be "cloned", the cloned stub contains the Origami Loader and an additional PE section called .origami, and a compressed version of dnlib which is required (in this application) to exract data the PE section. The Origami Loader will extract, decompress and invoke the original assembly on runtime.

       Origami.exe <file>
**Injection:** This mode needs two input files, the host file and the payload file. The Origami Loader will be injected into the host assembly's global type. The payload file is the assembly that the Origami Loader will invoke on runtime. The payload will be written into the .origami PE section. And a compressed version of dnlib will be added.

    Origami.exe -inject <host> <payload>

Origami supports .net framework executables

Structure of the Origami stub in dnSpy
![example output](https://i.imgur.com/9t2yO9e.png)

## Dependencies
- [dnlib](https://github.com/0xd4d/dnlib) by 0xd4d (nuget package)
