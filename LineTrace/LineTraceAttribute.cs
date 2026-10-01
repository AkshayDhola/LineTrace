namespace LineTrace;

[AttributeUsage(AttributeTargets.Method)]
public sealed class LineTraceAttribute : Attribute
{
    public int Iterations { get; set; } = 1;
}
