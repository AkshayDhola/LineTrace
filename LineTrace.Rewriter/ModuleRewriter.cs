using dnlib.DotNet;
using dnlib.DotNet.Emit;
using LineTrace.Rewriter.Emit;
using LineTrace.Rewriter.Helpers;

namespace LineTrace.Rewriter;

internal sealed class ModuleRewriter(ModuleDef module, Hooks hooks, bool lines, HashSet<string> userAssemblies)
{
    public const string Marker = "<LineTracePatched>";

    public void Run()
    {
        foreach (var type in new List<TypeDef>(module.GetTypes()))
        {
            if (IsAsyncStateMachine(type))
            {
                continue;
            }

            foreach (var method in new List<MethodDef>(type.Methods))
            {
                if (!method.HasBody || Has(method, Code.Jmp) || IsTrivialAccessor(method))
                {
                    continue;
                }

                if (method.CustomAttributes.Find("LineTrace.LineTraceAttribute") is { } attribute)
                {
                    RootWrapper.Wrap(method, attribute, this, hooks);
                }
                else
                {
                    Instrument(method, NodeNameHelper.Of(method));
                }
            }
        }

        module.Types.Add(new TypeDefUser("", Marker, module.CorLibTypes.Object.TypeDefOrRef)
        {
            Attributes = TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed,
        });
    }

    public void Instrument(MethodDef method, string displayName) =>
        new MethodRewriter(method, displayName, hooks, lines, userAssemblies).Run();

    private static bool IsAsyncStateMachine(TypeDef type)
    {
        foreach (var implemented in type.Interfaces)
        {
            if (implemented.Interface.FullName == "System.Runtime.CompilerServices.IAsyncStateMachine")
            {
                return true;
            }
        }

        return false;
    }

    private static bool Has(MethodDef method, Code code)
    {
        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.OpCode.Code == code)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTrivialAccessor(MethodDef method)
    {
        if (!method.IsGetter && !method.IsSetter)
        {
            return false;
        }

        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.OpCode.Code is Code.Call or Code.Callvirt or Code.Newobj or Code.Newarr or Code.Box)
            {
                return false;
            }
        }

        return true;
    }
}
