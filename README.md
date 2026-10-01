# LineTrace

Line-level profiler for .NET, like Callgrind. Mark a method, run it, and get an HTML report with the
time and allocations of every call and every source line.

## Quick start

```sh
dotnet new console -n LineDebugging
cd LineDebugging
dotnet add package LineTrace
```

Replace `Program.cs`:

```csharp
using LineTrace;

return LineTraceRunner.Run(typeof(Cases).Assembly, args);

public class Cases
{
    [LineTrace(Iterations = 1)]
    public void Sum()
    {
        long total = 0;
        for (int i = 0; i < 1_000_000; i++)
            total += i % 7;
    }
}
```

Run:

```sh
dotnet run -c Release
```

Open `LineTrace.Artifacts/LineDebugging-report.html`.

## Notes

- `[LineTrace]` methods must be `void`, non-async, non-generic, and take no parameters.
- There is no warm-up: all `Iterations` are averaged, so the first (cold) run with JIT and empty caches is included. Raise `Iterations` to see the warm cost dominate.
- `dotnet run -c Release -- Sum` runs only the cases whose name contains `Sum`.
- Calls into other libraries are timed as a whole and shown as `[external]`.
- Async methods are traced only up to the first `await`, and only the calling thread is traced.

## License

[MIT](LICENSE)
