namespace Idk.Bot.Diagnostics;

public sealed record PerfStatus(
    ServerDefinition Server,
    bool Found,
    int? ProcessId,
    string? User,
    double? CpuPercent,
    double? MemoryPercent,
    string? Elapsed,
    string? Details);
