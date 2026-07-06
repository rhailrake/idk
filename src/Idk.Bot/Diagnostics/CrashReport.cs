namespace Idk.Bot.Diagnostics;

public sealed record CrashReport(
    ServerDefinition Server,
    string Path,
    DateTimeOffset? LastModified,
    long? SizeBytes,
    string Content);
