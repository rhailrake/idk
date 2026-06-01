namespace Idk.Bot.Diagnostics;

public interface ITraceArchiveStore
{
    Task SaveLatestAsync(ServerDefinition server, string archivePath, CancellationToken cancellationToken);

    Task<TraceArchive?> GetLatestAsync(ServerDefinition server, CancellationToken cancellationToken);

    Task<TraceArchive?> ResolveAsync(ServerDefinition server, string? trace, CancellationToken cancellationToken);

    TraceArchive GetArchive(ServerDefinition server, string archivePath);
}
