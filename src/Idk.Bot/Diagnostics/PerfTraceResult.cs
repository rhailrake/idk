namespace Idk.Bot.Diagnostics;

public sealed record PerfTraceResult(
    ServerDefinition Server,
    bool Success,
    bool AlreadyRunning,
    string? ArchivePath,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration);
