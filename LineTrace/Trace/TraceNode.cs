using System.Diagnostics;

namespace LineTrace.Trace;

public sealed class TraceNode
{
    private static readonly double NanosecondsPerTick = 1_000_000_000.0 / Stopwatch.Frequency;

    internal readonly List<TraceNode> Nodes = [];

    internal long Ticks;
    internal long Bytes;
    internal long StartTicks;
    internal long StartBytes;

    internal TraceNode(string name, bool isExternal, TraceNode? parent, int line = 0, string? sourceFile = null)
    {
        Name = name;
        IsExternal = isExternal;
        Parent = parent;
        Line = line;
        SourceFile = sourceFile;
    }

    public string Name { get; }
    public int Line { get; }
    public string? SourceFile { get; }
    public bool IsExternal { get; }
    public TraceNode? Parent { get; }
    public long Calls { get; internal set; }

    public List<TraceNode> Children
    {
        get
        {
            var children = new List<TraceNode>();
            foreach (var node in Nodes)
            {
                if (node.Line > 0)
                {
                    children.AddRange(node.Nodes);
                }
                else
                {
                    children.Add(node);
                }
            }

            return children;
        }
    }

    public List<TraceNode> Lines
    {
        get
        {
            var lines = new List<TraceNode>();
            foreach (var node in Nodes)
            {
                if (node.Line > 0)
                {
                    lines.Add(node);
                }
            }

            return lines;
        }
    }

    public double InclusiveNanoseconds => Ticks * NanosecondsPerTick;
    public double ExclusiveNanoseconds => (Ticks - ChildTotals().Ticks) * NanosecondsPerTick;
    public long InclusiveBytes => Bytes;
    public long ExclusiveBytes => Bytes - ChildTotals().Bytes;

    public List<TraceNode> Descendants()
    {
        var all = new List<TraceNode>();
        AddDescendants(all);
        return all;
    }

    private void AddDescendants(List<TraceNode> all)
    {
        foreach (var child in Children)
        {
            all.Add(child);
            child.AddDescendants(all);
        }
    }

    private (long Ticks, long Bytes) ChildTotals()
    {
        long ticks = 0, bytes = 0;
        foreach (var node in Nodes)
        {
            if (node.Line == 0)
            {
                ticks += node.Ticks;
                bytes += node.Bytes;
                continue;
            }

            foreach (var call in node.Nodes)
            {
                ticks += call.Ticks;
                bytes += call.Bytes;
            }
        }

        return (ticks, bytes);
    }

    internal TraceNode GetOrAddChild(string name, bool external, int line, string? sourceFile)
    {
        foreach (var child in Nodes)
        {
            if (child.Line == line && child.IsExternal == external && child.Name == name)
            {
                return child;
            }
        }

        var node = new TraceNode(name, external, this, line, sourceFile);
        Nodes.Add(node);
        return node;
    }
}
