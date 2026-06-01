namespace Idk.Bot.Configuration;

public sealed class DiagnosticsOptions
{
    public const string LagtracePathEnvironmentVariable = "IDK_LAGTRACE_PATH";
    public const string TraceOutputDirectoryEnvironmentVariable = "IDK_TRACE_OUTPUT_DIR";
    public const string DefaultTraceSecondsEnvironmentVariable = "IDK_DEFAULT_TRACE_SECONDS";

    public required string LagtracePath { get; init; }

    public required string TraceOutputDirectory { get; init; }

    public TimeSpan DefaultTraceDuration { get; init; } = TimeSpan.FromSeconds(120);

    public static DiagnosticsOptions FromEnvironment()
    {
        return new DiagnosticsOptions
        {
            LagtracePath = ReadString(LagtracePathEnvironmentVariable, "/home/helper/lagtrace.sh"),
            TraceOutputDirectory = ReadString(TraceOutputDirectoryEnvironmentVariable, "/home/helper/ss14-traces"),
            DefaultTraceDuration = TimeSpan.FromSeconds(ReadTraceSeconds()),
        };
    }

    private static string ReadString(string environmentVariable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int ReadTraceSeconds()
    {
        var raw = Environment.GetEnvironmentVariable(DefaultTraceSecondsEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(raw))
            return 120;

        if (!int.TryParse(raw, out var seconds) || seconds < 1 || seconds > 600)
            throw new InvalidOperationException($"{DefaultTraceSecondsEnvironmentVariable} must be between 1 and 600.");

        return seconds;
    }
}
