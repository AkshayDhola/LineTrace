namespace LineTrace;

[AttributeUsage(AttributeTargets.Method)]
public sealed class LineTraceAttribute : Attribute
{
    public int Warmup { get; set; } = 1;
    public int Iterations { get; set; } = 1;
}
