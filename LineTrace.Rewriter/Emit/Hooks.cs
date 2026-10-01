using dnlib.DotNet;

namespace LineTrace.Rewriter.Emit;

internal sealed class Hooks
{
    public IMethod Enter { get; }
    public IMethod Exit { get; }
    public IMethod Unwind { get; }
    public IMethod Line { get; }
    public IMethod EnterExternal { get; }
    public IMethod ExitExternal { get; }
    public IMethod StartRoot { get; }
    public IMethod StopRoot { get; }
    
    public Hooks(ModuleDef module, TypeDef hooks)
    {
        IMethod Import(string name) => module.Import(hooks.FindMethod(name) ?? throw new RewriteException($"TraceHooks.{name} not found."));
        Enter = Import("Enter");
        Exit = Import("Exit");
        Unwind = Import("Unwind");
        Line = Import("Line");
        EnterExternal = Import("EnterExternal");
        ExitExternal = Import("ExitExternal");
        StartRoot = Import("StartRoot");
        StopRoot = Import("StopRoot");
    }
}