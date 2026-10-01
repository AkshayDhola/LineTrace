using System.Globalization;

namespace LineTrace.Report.Helpers;

internal static class HtmlHelper
{
    public static void Text(TextWriter w, ReadOnlySpan<char> text) => Encode(w, text, wrap: false);

    public static void Name(TextWriter w, ReadOnlySpan<char> name) => Encode(w, name, wrap: true);

    public static void Time(TextWriter w, double ns)
    {
        ns = Math.Max(0, ns);
        if (ns < 1_000)
        {
            Value(w, ns, "0");
            w.Write(" ns");
        }
        else if (ns < 1_000_000)
        {
            Value(w, ns / 1_000, "0.000");
            w.Write(" μs");
        }
        else
        {
            Value(w, ns / 1_000_000, "0.000");
            w.Write(" ms");
        }
    }

    public static void Bytes(TextWriter w, double bytes)
    {
        if (Math.Abs(bytes) < 1024)
        {
            Value(w, bytes, "0");
            w.Write(" B");
        }
        else if (Math.Abs(bytes) < 1024 * 1024)
        {
            Value(w, bytes / 1024, "0.0");
            w.Write(" KB");
        }
        else
        {
            Value(w, bytes / (1024 * 1024), "0.0");
            w.Write(" MB");
        }
    }

    public static void Count(TextWriter w, double value) => Value(w, value, value == Math.Floor(value) ? "0" : "0.##");

    public static void Value<T>(TextWriter w, T value, ReadOnlySpan<char> format = default)
        where T : ISpanFormattable
    {
        Span<char> buffer = stackalloc char[64];
        value.TryFormat(buffer, out var length, format, CultureInfo.InvariantCulture);
        w.Write(buffer[..length]);
    }

    private static void Encode(TextWriter w, ReadOnlySpan<char> text, bool wrap)
    {
        foreach (char c in text)
        {
            switch (c)
            {
                case '<': w.Write("&lt;"); break;
                case '>': w.Write("&gt;"); break;
                case '&': w.Write("&amp;"); break;
                case '"': w.Write("&quot;"); break;
                case '.' or ',' when wrap: w.Write(c); w.Write("<wbr>"); break;
                default: w.Write(c); break;
            }
        }
    }
}
