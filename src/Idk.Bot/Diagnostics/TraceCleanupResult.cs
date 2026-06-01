namespace Idk.Bot.Diagnostics;

public sealed record TraceCleanupResult(
    int DeletedFiles,
    int DeletedDirectories,
    long DeletedBytes,
    IReadOnlyList<string> Errors);
