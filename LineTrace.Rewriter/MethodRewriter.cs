using dnlib.DotNet;
using dnlib.DotNet.Emit;
using LineTrace.Rewriter.Emit;
using LineTrace.Rewriter.Helpers;

namespace LineTrace.Rewriter;

internal sealed class MethodRewriter(MethodDef method, string displayName, Hooks hooks, bool lines, HashSet<string> userAssemblies)
{
    private const int HiddenLine = 0xFEEFEE;

    private readonly CilBody body = method.Body;
    private readonly IList<Instruction> il = method.Body.Instructions;
    private readonly Local frame = new(method.Module.CorLibTypes.Int32, "$frame");

    private string? sourceFile;

    public void Run()
    {
        body.SimplifyMacros(method.Parameters);
        body.SimplifyBranches();
        body.Variables.Add(frame);

        foreach (var instruction in il)
        {
            if (instruction.OpCode.Code == Code.Tailcall)
            {
                instruction.OpCode = OpCodes.Nop;
            }
        }

        if (lines)
        {
            AddLineHooks();
        }

        WrapExternalCalls();
        UnwindInCatch();
        WrapMethodBody();

        body.OptimizeBranches();
        body.OptimizeMacros();
    }

    private void AddLineHooks()
    {
        foreach (var anchor in new List<Instruction>(il))
        {
            if (anchor.SequencePoint is not { } sp || sp.StartLine == HiddenLine)
            {
                continue;
            }

            var index = il.IndexOf(anchor);
            if (index > 0 && il[index - 1].OpCode.OpCodeType == OpCodeType.Prefix)
            {
                continue;
            }

            sourceFile ??= sp.Document.Url;
            Il.InsertBefore(il, anchor,
                Instruction.Create(OpCodes.Ldloc, frame),
                Instruction.Create(OpCodes.Ldc_I4, sp.StartLine),
                Instruction.Create(OpCodes.Call, hooks.Line));
        }
    }

    private void WrapExternalCalls()
    {
        foreach (var call in new List<Instruction>(il))
        {
            if (call.OpCode.Code is not (Code.Call or Code.Callvirt or Code.Newobj)
                || call.Operand is not IMethod target
                || !IsExternal(target, call.OpCode.Code))
            {
                continue;
            }

            var index = il.IndexOf(call);
            var anchor = index > 0 && il[index - 1].OpCode.OpCodeType == OpCodeType.Prefix ? il[index - 1] : call;
            var moved = Il.InsertBefore(il, anchor,
                Instruction.Create(OpCodes.Ldstr, NodeNameHelper.External(target, call.OpCode.Code)),
                Instruction.Create(OpCodes.Call, hooks.EnterExternal));
            var actual = anchor == call ? moved : call;
            il.Insert(il.IndexOf(actual) + 1, Instruction.Create(OpCodes.Call, hooks.ExitExternal));
        }
    }

    private bool IsExternal(IMethod target, Code code)
    {
        var name = target.Name.String;
        if ((code == Code.Call && name == ".ctor")
            || name.StartsWith("get_") || name.StartsWith("set_") || name.StartsWith("op_")
            || name.StartsWith("add_") || name.StartsWith("remove_"))
        {
            return false;
        }

        var type = target.DeclaringType;
        var assembly = type?.DefinitionAssembly?.Name.String;
        // Calls into LineTrace itself are our own hooks (inserted before this pass), not user code to measure.
        if (assembly is null or "LineTrace" || userAssemblies.Contains(assembly))
        {
            return false;
        }

        // JIT intrinsics: wrapping them breaks folding (typeof(T) == typeof(int), Unsafe.As) and records calls
        // that don't exist in the real code.
        var scope = type!.ScopeType?.FullName;
        return scope is not ("System.Runtime.CompilerServices.Unsafe"
                or "System.Runtime.CompilerServices.RuntimeHelpers"
                or "System.Runtime.InteropServices.MemoryMarshal")
            && !(scope == "System.Type" && name == "GetTypeFromHandle");
    }

    private void UnwindInCatch()
    {
        foreach (var handler in body.ExceptionHandlers)
        {
            if (handler.HandlerType is not (ExceptionHandlerType.Catch or ExceptionHandlerType.Filter))
            {
                continue;
            }

            Il.InsertBefore(il, handler.HandlerStart,
                Instruction.Create(OpCodes.Ldloc, frame),
                Instruction.Create(OpCodes.Call, hooks.Unwind));
        }
    }

    private void WrapMethodBody()
    {
        var isVoid = method.MethodSig.RetType.ElementType == ElementType.Void;
        var result = isVoid ? null : new Local(method.MethodSig.RetType, "$result");
        if (result is not null)
        {
            body.Variables.Add(result);
        }

        var end = isVoid ? Instruction.Create(OpCodes.Ret) : Instruction.Create(OpCodes.Ldloc, result);
        foreach (var ret in new List<Instruction>(il))
        {
            if (ret.OpCode.Code != Code.Ret)
            {
                continue;
            }

            if (isVoid)
            {
                ret.OpCode = OpCodes.Leave;
                ret.Operand = end;
            }
            else
            {
                ret.OpCode = OpCodes.Stloc;
                ret.Operand = result;
                il.Insert(il.IndexOf(ret) + 1, Instruction.Create(OpCodes.Leave, end));
            }
        }

        var tryStart = il[0];
        var finallyStart = Instruction.Create(OpCodes.Ldloc, frame);
        il.Add(finallyStart);
        il.Add(Instruction.Create(OpCodes.Call, hooks.Exit));
        il.Add(Instruction.Create(OpCodes.Endfinally));
        il.Add(end);
        if (!isVoid)
        {
            il.Add(Instruction.Create(OpCodes.Ret));
        }

        // Handlers that ran to the end of the method now end where our finally starts.
        foreach (var handler in body.ExceptionHandlers)
        {
            handler.TryEnd ??= finallyStart;
            handler.HandlerEnd ??= finallyStart;
        }

        body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally)
        {
            TryStart = tryStart,
            TryEnd = finallyStart,
            HandlerStart = finallyStart,
            HandlerEnd = end,
        });

        // Plain inserts, not the anchor trick: a loop back to the first instruction must not re-run Enter.
        il.Insert(0, Instruction.Create(OpCodes.Ldstr, displayName));
        il.Insert(1, sourceFile is null ? Instruction.Create(OpCodes.Ldnull) : Instruction.Create(OpCodes.Ldstr, sourceFile));
        il.Insert(2, Instruction.Create(OpCodes.Call, hooks.Enter));
        il.Insert(3, Instruction.Create(OpCodes.Stloc, frame));
    }
}
