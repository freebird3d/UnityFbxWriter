# FBX manipulation for .NET (for Unity)

Uses [hamish-milne/FbxWriter](https://github.com/hamish-milne/FbxWriter) under the hood, for writing a Unity GameObject mesh into a binary FBX file. To use this, copy the [FbxTemplate.bytes](https://github.com/cmdr2/UnityFbxWriter/blob/master/FbxTemplate.bytes) file into the `Resources` folder in your Unity project, then call:
```csharp
UnityFbxWriter.ExportToBinary(gameObjectToExport, "/path/to/output/file");
```

- Read FBX binary files (**Done**)
- Read FBX ASCII files (**Done**)
- Write **fully compliant** FBX binary files (**Done**)
- Write FBX ASCII files (**Done**)
- Format detection (TODO)
- Store and manipulate raw FBX object data (**Done**)
- Higher level processing of FBX nodes (TODO)
- Optional integration with DotNetZip for more efficient compression (TODO)

```csharp
using Fbx;

class FbxExample
{
	static void Main(string[] args)
	{
		// Read a file
		var documentNode = FbxIO.ReadBinary("MyModel.fbx");
		
		// Update a property
		documentNode["Creator"].Value = "My Application";
		
		// Preview the file in the console
		var writer = new FbxAsciiWriter(Console.OpenStandardOutput());
		writer.Write(documentNode);
		
		// Write the updated binary
		FbxIO.WriteBinary(documentNode, "MyModel_patched.fbx");
	}
}
```
