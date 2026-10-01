using dnlib.DotNet;
using dnlib.DotNet.Writer;
using LineTrace.Rewriter;
using LineTrace.Rewriter.Emit;

// Usage: LineTrace.Rewriter <LineTrace.dll> <assembly.dll>[=lines] ...
// Patches each assembly in place. Calls between the listed assemblies are traced as methods, not as external calls.
if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: LineTrace.Rewriter <LineTrace.dll> <assembly.dll>[=lines] ...");
    return 2;
}

var targets = new List<(string Path, bool Lines)>();
foreach (var arg in args.Skip(1))
{
    var lines = arg.EndsWith("=lines", StringComparison.Ordinal);
    targets.Add((Path.GetFullPath(lines ? arg[..^"=lines".Length] : arg), lines));
}

var userAssemblies = new HashSet<string>(targets.Select(t => Path.GetFileNameWithoutExtension(t.Path)));
using var lineTrace = ModuleDefMD.Load(args[0]);
var hooksType = lineTrace.Find("LineTrace.Trace.TraceHooks", isReflectionName: false)
    ?? throw new RewriteException($"{args[0]} has no LineTrace.Trace.TraceHooks.");

try
{
    foreach (var (path, lines) in targets)
    {
        // Load from memory so the file can be overwritten; the PDB is read from beside it for line numbers.
        var module = ModuleDefMD.Load(File.ReadAllBytes(path), new ModuleCreationOptions { TryToLoadPdbFromDisk = false });
        var pdb = Path.ChangeExtension(path, ".pdb");
        if (File.Exists(pdb))
        {
            module.LoadPdb(File.ReadAllBytes(pdb));
        }

        if (module.Find(ModuleRewriter.Marker, isReflectionName: false) is not null)
        {
            Console.WriteLine($"LineTrace: {Path.GetFileName(path)} already patched.");
            continue;
        }

        new ModuleRewriter(module, new Hooks(module, hooksType), lines, userAssemblies).Run();

        // Both files were read into memory, so they can be overwritten in place.
        module.Write(path, new ModuleWriterOptions(module) { WritePdb = module.PdbState is not null, PdbFileName = pdb });

        Console.WriteLine($"LineTrace: patched {Path.GetFileName(path)}{(lines ? " (lines)" : "")}.");
    }
}
catch (RewriteException e)
{
    Console.Error.WriteLine($"error LT0001: {e.Message}");
    return 1;
}

return 0;
