namespace LineTrace.Report.Models;

internal sealed class MethodLines(string name, string? file)
{
    public readonly string Name = name;
    public readonly string? File = file;
    public readonly Dictionary<int, LineTotals> Rows = [];
    public long Calls;
    public double Time;
    public double Self;
    public long Alloc;
}
