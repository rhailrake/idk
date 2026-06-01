namespace Idk.Bot.Diagnostics;

public interface ITraceCleanupService
{
    Task<TraceCleanupResult> CleanupAsync(TimeSpan olderThan, CancellationToken cancellationToken);
}
