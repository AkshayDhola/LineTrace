using System.Reflection;
using LineTrace.Report;
using LineTrace.Trace;

namespace LineTrace;

public static class LineTraceRunner
{
    public static int Run(Assembly assembly, string[] args)
    {
        var cases = new List<MethodInfo>();
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.IsDefined(typeof(LineTraceAttribute)) && Matches($"{type.Name}.{method.Name}", args))
                {
                    cases.Add(method);
                }
            }
        }

        if (cases.Count == 0)
        {
            Console.Error.WriteLine($"No [LineTrace] method in {assembly.GetName().Name} matches '{string.Join(" ", args)}'.");
            return 1;
        }

        var sessions = new List<TraceSession>();
        var failed = false;
        foreach (var method in cases)
        {
            try
            {
                if (method.GetParameters().Length > 0)
                {
                    throw new NotSupportedException("methods with parameters are not supported.");
                }

                Tracer.LastSession = null;
                method.Invoke(method.IsStatic ? null : Activator.CreateInstance(method.DeclaringType!), null);
                sessions.Add(Tracer.LastSession
                    ?? throw new InvalidOperationException("nothing was recorded. Is the assembly patched (LineTrace package referenced, LineTraceEnabled not false)?"));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"{method.DeclaringType!.Name}.{method.Name}: {(e as TargetInvocationException)?.InnerException ?? e}");
                failed = true;
            }
        }

        if (sessions.Count > 0)
        {
            var title = assembly.GetName().Name!;
            var path = Path.GetFullPath(Path.Combine("LineTrace.Artifacts", $"{title}-report.html"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            HtmlReport.Write(path, title, sessions);
            Console.WriteLine($"Report: {path}");
        }

        return failed ? 1 : 0;
    }

    private static bool Matches(string name, string[] filters)
    {
        if (filters.Length == 0)
        {
            return true;
        }

        foreach (var filter in filters)
        {
            if (name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
