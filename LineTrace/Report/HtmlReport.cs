using System.Runtime.InteropServices;
using LineTrace.Report.Models;
using LineTrace.Report.Helpers;
using LineTrace.Trace;

namespace LineTrace.Report;

internal static class HtmlReport
{
    public static void Write(string path, string title, List<TraceSession> sessions)
    {
        List<List<MethodLines>> methods = [];
        bool lineHooks = false;

        foreach (var session in sessions)
        {
            var list = Methods(session.Root);
            methods.Add(list);
            lineHooks |= list.Count > 0;
        }

        using var w = new StreamWriter(path);
        w.Write(HtmlTemplate.Head);
        HtmlHelper.Text(w, title);
        w.Write(HtmlTemplate.Side);
        HtmlHelper.Name(w, title);
        w.Write(HtmlTemplate.Run);
        w.Write("<dt>Run</dt><dd>");
        HtmlHelper.Value(w, DateTime.Now, "yyyy-MM-dd HH:mm");
        w.Write("</dd><dt>Runtime</dt><dd>");
        HtmlHelper.Text(w, RuntimeInformation.FrameworkDescription);
        w.Write("</dd><dt>OS</dt><dd>");
        HtmlHelper.Text(w, RuntimeInformation.OSDescription);
        w.Write(' ');
        HtmlHelper.Value(w, RuntimeInformation.ProcessArchitecture);
        w.Write("</dd><dt>CPUs</dt><dd>");
        HtmlHelper.Value(w, Environment.ProcessorCount);
        w.Write("</dd><dt>Mode</dt><dd>");
        w.Write(lineHooks ? "Line hooks on" : "Method level");
        w.Write("</dd>");

        w.Write(HtmlTemplate.Nav);
        for (var i = 0; i < sessions.Count; i++)
        {
            w.Write("<a class=\"case\" href=\"#case-");
            HtmlHelper.Value(w, i);
            w.Write("\">");
            HtmlHelper.Name(w, CaseName(sessions[i]));
            w.Write("</a><a class=\"section\" href=\"#case-");
            HtmlHelper.Value(w, i);
            w.Write("-tree\">Call tree</a>");
            
            if (methods[i].Count > 0)
            {
                w.Write("<a class=\"section\" href=\"#case-");
                HtmlHelper.Value(w, i);
                w.Write("-lines\">Line report</a>");
            }
        }

        w.Write(HtmlTemplate.Page);
        HtmlHelper.Name(w, title);
        w.Write(HtmlTemplate.Notes);
        var sources = new Dictionary<string, string[]>();
        for (var i = 0; i < sessions.Count; i++)
        {
            Case(w, i, sessions[i], methods[i], sources);
        }

        w.Write(HtmlTemplate.End);
    }

    private static string CaseName(TraceSession session) =>
        session.Root.Nodes.Count > 0 ? session.Root.Nodes[0].Name : "trace";

    private static void Case(TextWriter w, int index, TraceSession session, List<MethodLines> methods, Dictionary<string, string[]> sources)
    {
        var root = session.Root;
        double n = session.Iterations;
        w.Write("<section class=\"case\" id=\"case-");
        HtmlHelper.Value(w, index);
        w.Write("\"><header class=\"case-head\"><h2>");
        HtmlHelper.Name(w, CaseName(session));
        w.Write("</h2><div class=\"tiles\"><div class=\"tile\"><small>Time per iteration</small><b>");
        HtmlHelper.Time(w, root.InclusiveNanoseconds / n);
        w.Write("</b></div><div class=\"tile\"><small>Allocated per iteration</small><b>");
        HtmlHelper.Bytes(w, root.InclusiveBytes / n);
        w.Write("</b></div><div class=\"tile\"><small>Iterations</small><b>");
        HtmlHelper.Count(w, n);
        w.Write("</b></div></div></header>");

        CardHead(w, index, "tree", "Call tree", "Time includes children · Self excludes them · Share = part of the total time");
        w.Write(HtmlTemplate.TreeHead);
        Calls(w, root, 0, n, root.InclusiveNanoseconds);
        w.Write("</div></details>");

        if (methods.Count > 0)
        {
            CardHead(w, index, "lines", "Line report", "One block per method, hottest first · shading = share of the method's largest Self / Self alloc");
            foreach (var method in methods)
            {
                Lines(w, method, n, sources);
            }

            w.Write("</details>");
        }

        w.Write("</section>");
    }

    private static void CardHead(TextWriter w, int index, string id, string title, string hint)
    {
        w.Write("<details class=\"card\" id=\"case-");
        HtmlHelper.Value(w, index);
        w.Write('-');
        w.Write(id);
        w.Write("\" open><summary class=\"card-head\"><span class=\"title\">");
        w.Write(title);
        w.Write("</span><span class=\"hint\">");
        HtmlHelper.Text(w, hint);
        w.Write("</span>");
        w.Write(HtmlTemplate.Tools);
    }

    private static void Calls(TextWriter w, TraceNode node, int depth, double n, double total)
    {
        foreach (var child in node.Nodes)
        {
            if (child.Line == 0)
            {
                Call(w, child, 0, depth, n, total);
                continue;
            }

            foreach (var call in child.Nodes)
            {
                Call(w, call, child.Line, depth, n, total);
            }
        }
    }

    private static void Call(TextWriter w, TraceNode node, int line, int depth, double n, double total)
    {
        var share = total > 0 ? Math.Clamp(node.InclusiveNanoseconds / total * 100, 0, 100) : 0;
        var leaf = !HasCalls(node);
        w.Write(leaf ? "<div class=\"row\">" : share >= 10 ? "<details open><summary class=\"row\">" : "<details><summary class=\"row\">");

        w.Write("<span class=\"name\" style=\"--d:");
        HtmlHelper.Value(w, depth);
        w.Write("\" title=\"");
        HtmlHelper.Text(w, node.Name);
        w.Write("\">");
        HtmlHelper.Name(w, node.Name);
        if (line > 0)
        {
            w.Write("<span class=\"at\"> :");
            HtmlHelper.Value(w, line);
            w.Write("</span>");
        }

        if (node.IsExternal)
        {
            w.Write("<span class=\"tag\">external</span>");
        }

        w.Write("</span><span class=\"share\"><span class=\"track\"><span class=\"bar\" style=\"--w:");
        HtmlHelper.Value(w, share, "0.###");
        w.Write("%\"></span></span>");
        HtmlHelper.Value(w, share, "0.0");
        w.Write("%</span><span>");
        HtmlHelper.Count(w, node.Calls / n);
        w.Write("</span><span>");
        HtmlHelper.Time(w, node.InclusiveNanoseconds / n);
        w.Write("</span><span>");
        HtmlHelper.Time(w, node.ExclusiveNanoseconds / n);
        w.Write("</span><span>");
        HtmlHelper.Bytes(w, node.InclusiveBytes / n);
        w.Write("</span><span>");
        HtmlHelper.Bytes(w, node.ExclusiveBytes / n);
        w.Write("</span>");

        if (leaf)
        {
            w.Write("</div>");
            return;
        }

        w.Write("</summary>");
        Calls(w, node, depth + 1, n, total);
        w.Write("</details>");
    }

    private static bool HasCalls(TraceNode node)
    {
        foreach (var child in node.Nodes)
        {
            if (child.Line == 0 || child.Nodes.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static List<MethodLines> Methods(TraceNode root)
    {
        var byName = new Dictionary<string, MethodLines>();
        Collect(root, byName);
        var methods = new List<MethodLines>(byName.Values);
        methods.Sort((a, b) => b.Time.CompareTo(a.Time));
        return methods;
    }

    private static void Collect(TraceNode node, Dictionary<string, MethodLines> byName)
    {
        MethodLines? method = null;
        foreach (var child in node.Nodes)
        {
            if (child.Line > 0)
            {
                if (method is null)
                {
                    if (!byName.TryGetValue(node.Name, out method))
                    {
                        byName[node.Name] = method = new MethodLines(node.Name, node.SourceFile);
                    }

                    method.Calls += node.Calls;
                    method.Time += node.InclusiveNanoseconds;
                    method.Self += node.ExclusiveNanoseconds;
                    method.Alloc += node.InclusiveBytes;
                }

                ref var row = ref CollectionsMarshal.GetValueRefOrAddDefault(method.Rows, child.Line, out _);
                row.Hits += child.Calls;
                row.Time += child.InclusiveNanoseconds;
                row.Self += child.ExclusiveNanoseconds;
                row.Alloc += child.InclusiveBytes;
                row.SelfAlloc += child.ExclusiveBytes;
            }

            Collect(child, byName);
        }
    }

    private static void Lines(TextWriter w, MethodLines method, double n, Dictionary<string, string[]> sources)
    {
        int first = int.MaxValue, last = 0;
        double maxSelf = 0, maxAlloc = 0;
        foreach (var (line, row) in method.Rows)
        {
            first = Math.Min(first, line);
            last = Math.Max(last, line);
            maxSelf = Math.Max(maxSelf, row.Self);
            maxAlloc = Math.Max(maxAlloc, row.SelfAlloc);
        }

        var source = Source(method.File, sources);
        var dedent = int.MaxValue;
        for (var line = first; line <= last && line <= source.Length; line++)
        {
            var text = source[line - 1];
            var indent = text.Length - text.AsSpan().TrimStart().Length;
            if (indent < text.Length)
            {
                dedent = Math.Min(dedent, indent);
            }
        }

        w.Write("<details class=\"method\"><summary><b>");
        HtmlHelper.Name(w, method.Name);
        w.Write("</b><span class=\"hint\">calls ");
        HtmlHelper.Count(w, method.Calls / n);
        w.Write(" · time ");
        HtmlHelper.Time(w, method.Time / n);
        w.Write(" · self ");
        HtmlHelper.Time(w, method.Self / n);
        w.Write(" · alloc ");
        HtmlHelper.Bytes(w, method.Alloc / n);
        w.Write(" · <span title=\"");
        HtmlHelper.Text(w, method.File);
        w.Write("\">");
        HtmlHelper.Text(w, Path.GetFileName(method.File.AsSpan()));
        w.Write("</span>");
        w.Write(HtmlTemplate.LineHead);

        for (var line = first; line <= last; line++)
        {
            w.Write("<tr><td class=\"num at\">");
            HtmlHelper.Value(w, line);
            w.Write("</td>");
            if (method.Rows.TryGetValue(line, out var row))
            {
                w.Write("<td class=\"num\">");
                HtmlHelper.Count(w, row.Hits / n);
                w.Write("</td><td class=\"num\">");
                HtmlHelper.Time(w, row.Time / n);
                w.Write("</td><td class=\"num heat time\" style=\"--h:");
                HtmlHelper.Value(w, Heat(row.Self, maxSelf), "0.###");
                w.Write("\">");
                HtmlHelper.Time(w, row.Self / n);
                w.Write("</td><td class=\"num\">");
                HtmlHelper.Bytes(w, row.Alloc / n);
                w.Write("</td><td class=\"num heat alloc\" style=\"--h:");
                HtmlHelper.Value(w, Heat(row.SelfAlloc, maxAlloc), "0.###");
                w.Write("\">");
                HtmlHelper.Bytes(w, row.SelfAlloc / n);
                w.Write("</td>");
            }
            else
            {
                // Never ran: a blank line or a brace between executed lines.
                w.Write("<td class=\"num\"></td><td class=\"num\"></td><td class=\"num\"></td><td class=\"num\"></td><td class=\"num\"></td>");
            }

            w.Write("<td class=\"src\"><code>");
            if (line <= source.Length)
            {
                var text = source[line - 1].AsSpan();
                HtmlHelper.Text(w, (text.Length > dedent ? text[dedent..] : text).TrimEnd());
            }

            w.Write("</code></td></tr>");
        }

        w.Write("</tbody></table></details>");
    }

    private static string[] Source(string? file, Dictionary<string, string[]> sources)
    {
        if (file is null)
        {
            return [];
        }

        if (!sources.TryGetValue(file, out var lines))
        {
            lines = File.Exists(file) ? File.ReadAllLines(file) : [];
            for (var i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].Replace("\t", "    "); // same string back when there is no tab
            }

            sources[file] = lines;
        }

        return lines;
    }

    private static double Heat(double value, double max) => max > 0 ? Math.Clamp(value / max, 0, 1) : 0;
}
