namespace Idk.Bot.Diagnostics;

public sealed record TraceArchive(
    ServerDefinition Server,
    string Path,
    long? SizeBytes,
    DateTimeOffset? LastWriteTime);
