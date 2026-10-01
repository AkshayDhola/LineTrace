using System.ComponentModel;

namespace LineTrace.Trace;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class TraceHooks
{
    public static int Enter(string name, string? sourceFile)
    {
        var session = Tracer.Current;
        return session is null ? -1 : session.Push(name, external: false, session.HookResidual.Enter, sourceFile: sourceFile);
    }

    public static void Exit(int frame)
    {
        if (frame >= 0)
        {
            var session = Tracer.Current;
            session?.PopTo(frame, session.HookResidual.Exit);
        }
    }

    public static void Unwind(int frame)
    {
        if (frame >= 0)
        {
            var session = Tracer.Current;
            session?.PopTo(frame + 1, session.HookResidual.Exit);
        }
    }

    public static void Line(int frame, int line)
    {
        if (frame >= 0 && Tracer.Current is { } session)
        {
            session.PopTo(frame + 1, session.LineResidual.Exit);
            session.Push("", external: false, session.LineResidual.Enter, line);
        }
    }

    public static void EnterExternal(string name)
    {
        var session = Tracer.Current;
        session?.Push(name, external: true, session.HookResidual.Enter);
    }

    public static void ExitExternal()
    {
        Tracer.Current?.PopExternal();
    }

    public static TraceSession StartRoot(int iterations) => Tracer.Start(iterations, measured: true);

    public static void StopRoot(TraceSession session)
    {
        session.Dispose();
        if (session.Measured)
        {
            Tracer.LastSession = session;
        }
    }
}
