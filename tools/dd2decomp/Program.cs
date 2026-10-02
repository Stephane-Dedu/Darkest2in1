// Per-type decompiler: one bad type can't take down the run.
// usage: dd2decomp <assembly> <refDir> <outDir> [startIndex]
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

var asmPath = args[0];
var refDir = args[1];
var outDir = args[2];
int start = args.Length > 3 ? int.Parse(args[3]) : 0;

int exitCode = 0;
var worker = new Thread(() => exitCode = Run(), 1024 * 1024 * 1024);
worker.Start();
worker.Join();
return exitCode;

int Run()
{
    var file = new PEFile(asmPath);
    var resolver = new UniversalAssemblyResolver(asmPath, false, file.Metadata.DetectTargetFrameworkId());
    resolver.AddSearchDirectory(refDir);
    var settings = new DecompilerSettings(LanguageVersion.Latest) { ThrowOnAssemblyResolveErrors = false };
    var decompiler = new CSharpDecompiler(file, resolver, settings);

    var types = decompiler.TypeSystem.MainModule.TypeDefinitions
        .Where(t => t.DeclaringTypeDefinition == null && !t.Name.StartsWith("<"))
        .OrderBy(t => t.FullName)
        .ToList();

    var progress = Path.Combine(outDir, "_progress.txt");
    var failures = Path.Combine(outDir, "_failures.txt");
    Directory.CreateDirectory(outDir);
    Console.WriteLine($"{types.Count} top-level types, starting at {start}");

    for (int i = start; i < types.Count; i++)
    {
        var t = types[i];
        File.WriteAllText(progress, $"{i}\t{t.FullName}");
        var ns = string.IsNullOrEmpty(t.Namespace) ? "_global" : t.Namespace;
        var dir = Path.Combine(outDir, ns);
        Directory.CreateDirectory(dir);
        var name = string.Concat(t.Name.Split(Path.GetInvalidFileNameChars()));
        if (t.TypeParameterCount > 0) name += "`" + t.TypeParameterCount;
        try
        {
            var code = decompiler.DecompileTypeAsString(t.FullTypeName);
            File.WriteAllText(Path.Combine(dir, name + ".cs"), code);
        }
        catch (Exception e)
        {
            File.AppendAllText(failures, $"{i}\t{t.FullName}\t{e.GetType().Name}: {e.Message}\n");
        }
        if (i % 500 == 0) Console.WriteLine($"{i}/{types.Count}");
    }
    File.WriteAllText(progress, "done");
    Console.WriteLine("done");
    return 0;
}
