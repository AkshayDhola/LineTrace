using LineTrace.Trace;

namespace LineTrace;

public static class Tracer
{
    [ThreadStatic] internal static TraceSession? Current;
    [ThreadStatic] internal static TraceSession? LastSession;

    private static (Residual Hook, Residual Line)? _residual;

    public static TraceSession Start() => Start(iterations: 1, measured: false);
    public static TraceNode? GetTrace() => Current?.Root;

    internal static TraceSession Start(int iterations, bool measured)
    {
        if (Current is not null)
        {
            throw new InvalidOperationException("A trace session is already running on this thread.");
        }

        return Current = new TraceSession(iterations, measured, _residual ??= TraceSession.Calibrate());
    }
}
