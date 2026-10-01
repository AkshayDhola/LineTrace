using dnlib.DotNet.Emit;

namespace LineTrace.Rewriter.Emit;

internal static class Il
{
    public static Instruction InsertBefore(IList<Instruction> il, Instruction anchor, params Instruction[] hook)
    {
        var moved = new Instruction(anchor.OpCode, anchor.Operand);
        anchor.OpCode = hook[0].OpCode;
        anchor.Operand = hook[0].Operand;
        var index = il.IndexOf(anchor);
        for (var i = 1; i < hook.Length; i++)
        {
            il.Insert(++index, hook[i]);
        }

        il.Insert(++index, moved);

        return moved;
    }
}
