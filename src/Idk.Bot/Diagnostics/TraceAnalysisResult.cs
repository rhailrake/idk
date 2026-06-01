namespace Idk.Bot.Diagnostics;

public sealed record TraceAnalysisResult(
    TraceArchive Archive,
    bool Success,
    string? Error,
    TraceProcessSummary? Process,
    TraceHardwareSummary? Hardware,
    IReadOnlyList<TraceThreadSample> Threads,
    IReadOnlyList<TraceFunctionSample> HotFunctions,
    IReadOnlyList<TraceCategorySample> Categories);

public sealed record TraceProcessSummary(
    int? ProcessId,
    string? User,
    double? SnapshotCpuPercent,
    double? SnapshotMemoryPercent,
    string? Elapsed,
    double? AverageCpuPercent,
    double? MaxCpuPercent,
    double? AverageUserCpuPercent,
    double? AverageSystemCpuPercent,
    double? AverageWaitPercent);

public sealed record TraceHardwareSummary(
    string? Uptime,
    double? AverageCpuIdlePercent,
    double? AverageCpuWaitPercent,
    double? AverageCpuUserPercent,
    double? AverageCpuSystemPercent,
    double? AverageRunQueue,
    int? MaxRunQueue);

public sealed record TraceThreadSample(
    string Name,
    double AverageCpuPercent,
    double MaxCpuPercent,
    int Samples);

public sealed record TraceFunctionSample(
    int Rank,
    string Name,
    string Category,
    double InclusivePercent,
    double ExclusivePercent);

public sealed record TraceCategorySample(
    string Category,
    double ExclusivePercent);
