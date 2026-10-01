using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace LineTrace.Rewriter.Helpers;

internal static class NodeNameHelper
{
    public static string Of(MethodDef method)
    {
        var type = TypeName(method.DeclaringType);
        if (method.IsConstructor)
        {
            return method.IsStatic ? $"{type}.cctor" : $"new {type}";
        }

        var generics = method.HasGenericParameters ? Join(method.GenericParameters, 0) : "";
        return $"{type}.{MethodName(method.Name)}{generics}";
    }

    public static string External(IMethod target, Code code)
    {
        var type = target.DeclaringType.ScopeType?.Name.String ?? target.DeclaringType.Name.String;
        var tick = type.IndexOf('`');
        type = tick < 0 ? type : type[..tick];
        return code == Code.Newobj ? $"new {type}" : $"{type}.{target.Name}";
    }

    private static string MethodName(string name)
    {
        var depth = 0;
        for (var i = name.LastIndexOf('.') - 1; i >= 0; i--)
        {
            switch (name[i])
            {
                case '>': depth++; break;
                case '<': depth--; break;
                case '.' when depth == 0: return name[(i + 1)..];
            }
        }

        return name;
    }

    private static string TypeName(TypeDef type)
    {
        var outer = type.DeclaringType is { } declaring ? TypeName(declaring) + "." : "";
        var tick = type.Name.String.IndexOf('`');
        if (tick < 0)
        {
            return outer + type.Name;
        }

        var arity = int.Parse(type.Name.String[(tick + 1)..]);
        return outer + type.Name.String[..tick] + Join(type.GenericParameters, type.GenericParameters.Count - arity);
    }

    private static string Join(IList<GenericParam> parameters, int start)
    {
        var text = new StringBuilder("<");
        for (var i = start; i < parameters.Count; i++)
        {
            text.Append(i > start ? "," : "").Append(parameters[i].Name);
        }

        return text.Append('>').ToString();
    }
}