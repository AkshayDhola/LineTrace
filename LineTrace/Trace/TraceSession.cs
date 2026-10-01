using System.Diagnostics;

namespace LineTrace.Trace;

public sealed class TraceSession : IDisposable
{
    internal readonly Residual HookResidual;
    internal readonly Residual LineResidual;

    private TraceNode _current;
    private int _depth;
    private long _overheadTicks;
    private long _overheadBytes;

    internal TraceSession(int iterations, bool measured, (Residual Hook, Residual Line) residual)
    {
        (HookResidual, LineResidual) = residual;
        Iterations = iterations;
        Measured = measured;
        Root = _current = new TraceNode("<root>", isExternal: false, parent: null);
    }

    public TraceNode Root { get; }
    public int Iterations { get; }
    internal bool Measured { get; }

    public void Dispose()
    {
        if (Tracer.Current == this)
        {
            Tracer.Current = null;
        }

        Root.Ticks = 0;
        Root.Bytes = 0;
        foreach (var node in Root.Nodes)
        {
            Root.Ticks += node.Ticks;
            Root.Bytes += node.Bytes;
        }
    }

    internal int Push(string name, bool external, long residual, int line = 0, string? sourceFile = null)
    {
        var t0 = Stopwatch.GetTimestamp();
        var b0 = GC.GetAllocatedBytesForCurrentThread();

        var node = _current.GetOrAddChild(name, external, line, sourceFile);
        node.Calls++;
        _current = node;
        var frame = _depth++;

        var b1 = GC.GetAllocatedBytesForCurrentThread();
        var t1 = Stopwatch.GetTimestamp();
        _overheadBytes += b1 - b0;
        _overheadTicks += t1 - t0 + residual;

        node.StartBytes = b1 - _overheadBytes;
        node.StartTicks = t1 - _overheadTicks;
        return frame;
    }

    internal void PopTo(int depth, long residual)
    {
        var t = Stopwatch.GetTimestamp();
        var b = GC.GetAllocatedBytesForCurrentThread();

        // Charged once: every node popped here (and every ancestor) contains the same hook call.
        _overheadTicks += residual;
        while (_depth > depth)
        {
            var node = _current;
            node.Ticks += t - _overheadTicks - node.StartTicks;
            node.Bytes += b - _overheadBytes - node.StartBytes;
            _current = node.Parent!;
            _depth--;
        }

        _overheadTicks += Stopwatch.GetTimestamp() - t;
    }

    internal void PopExternal()
    {
        if (_current.IsExternal)
        {
            PopTo(_depth - 1, HookResidual.Exit);
        }
    }

    internal static (Residual Hook, Residual Line) Calibrate()
    {
        var previous = Tracer.Current;
        try
        {
            return (Measure(lines: false), Measure(lines: true));
        }
        finally
        {
            Tracer.Current = previous;
        }
    }

    private static Residual Measure(bool lines)
    {
        const int Count = 1000;
        var session = Tracer.Current = new TraceSession(1, measured: false, residual: default);
        var frame = lines ? TraceHooks.Enter("calibrate", null) : 0;
        var parentNode = session._current;
        long bestParent = long.MaxValue, bestSelf = long.MaxValue;
        for (var batch = 0; batch < 20; batch++)
        {
            var self0 = parentNode.Nodes.Count > 0 ? parentNode.Nodes[0].Ticks : 0;
            var overhead0 = session._overheadTicks;
            var t0 = Stopwatch.GetTimestamp();
            for (var i = 0; i < Count; i++)
            {
                if (lines)
                {
                    TraceHooks.Line(frame, 1);
                }
                else
                {
                    TraceHooks.Exit(TraceHooks.Enter("calibrate", null));
                }
            }

            var parent = Stopwatch.GetTimestamp() - t0 - (session._overheadTicks - overhead0);
            session.PopTo(frame + 1, 0);
            bestParent = Math.Min(bestParent, parent);
            bestSelf = Math.Min(bestSelf, parentNode.Nodes[0].Ticks - self0);
        }

        var exit = Math.Max(0, bestSelf / Count);
        return new Residual(Math.Max(0, bestParent / Count - exit), exit);
    }
}
