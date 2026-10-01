using dnlib.DotNet;
using dnlib.DotNet.Emit;
using LineTrace.Rewriter.Emit;
using LineTrace.Rewriter.Helpers;

namespace LineTrace.Rewriter;

internal static class RootWrapper
{
    public static void Wrap(MethodDef method, CustomAttribute attribute, ModuleRewriter rewriter, Hooks hooks)
    {
        var name = NodeNameHelper.Of(method);
        if (method.MethodSig.RetType.ElementType != ElementType.Void
            || method.HasGenericParameters
            || method.DeclaringType.HasGenericParameters
            || method.CustomAttributes.Find("System.Runtime.CompilerServices.AsyncStateMachineAttribute") is not null)
        {
            throw new RewriteException($"{name}: [LineTrace] supports only synchronous, non-generic, void methods.");
        }

        int Arg(string property) => attribute.GetProperty(property)?.Argument.Value is int value ? value : 1;

        var traced = new MethodDefUser(
            method.Name + "$traced",
            method.MethodSig.Clone(),
            method.ImplAttributes,
            MethodAttributes.Private | MethodAttributes.HideBySig | (method.IsStatic ? MethodAttributes.Static : 0));
        method.DeclaringType.Methods.Add(traced);
        traced.Body = method.Body;
        rewriter.Instrument(traced, name);

        var body = method.Body = new CilBody();
        var session = new Local(hooks.StartRoot.MethodSig.RetType, "$session");
        var i = new Local(method.Module.CorLibTypes.Int32, "$i");
        body.Variables.Add(session);
        body.Variables.Add(i);

        Loop(Arg("Warmup"), measured: false);
        Loop(Arg("Iterations"), measured: true);
        body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        body.OptimizeBranches();
        body.OptimizeMacros();

        void Loop(int count, bool measured)
        {
            var il = body.Instructions;
            var tryStart = Instruction.Create(OpCodes.Ldc_I4_0);
            var loop = Instruction.Create(OpCodes.Nop);
            var condition = Instruction.Create(OpCodes.Ldloc, i);
            var finallyStart = Instruction.Create(OpCodes.Ldloc, session);
            var after = Instruction.Create(OpCodes.Nop);

            il.Add(Instruction.Create(OpCodes.Ldc_I4, count));
            il.Add(Instruction.Create(measured ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
            il.Add(Instruction.Create(OpCodes.Call, hooks.StartRoot));
            il.Add(Instruction.Create(OpCodes.Stloc, session));
            il.Add(tryStart);
            il.Add(Instruction.Create(OpCodes.Stloc, i));
            il.Add(Instruction.Create(OpCodes.Br, condition));
            il.Add(loop);
            foreach (var parameter in method.Parameters)
            {
                il.Add(Instruction.Create(OpCodes.Ldarg, parameter));
            }

            il.Add(Instruction.Create(OpCodes.Call, traced));
            il.Add(Instruction.Create(OpCodes.Ldloc, i));
            il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
            il.Add(Instruction.Create(OpCodes.Add));
            il.Add(Instruction.Create(OpCodes.Stloc, i));
            il.Add(condition);
            il.Add(Instruction.Create(OpCodes.Ldc_I4, count));
            il.Add(Instruction.Create(OpCodes.Blt, loop));
            il.Add(Instruction.Create(OpCodes.Leave, after));
            il.Add(finallyStart);
            il.Add(Instruction.Create(OpCodes.Call, hooks.StopRoot));
            il.Add(Instruction.Create(OpCodes.Endfinally));
            il.Add(after);
            body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally)
            {
                TryStart = tryStart,
                TryEnd = finallyStart,
                HandlerStart = finallyStart,
                HandlerEnd = after,
            });
        }
    }
}
