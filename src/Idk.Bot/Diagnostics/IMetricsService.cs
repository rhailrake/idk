namespace Idk.Bot.Diagnostics;

public interface IMetricsService
{
    IReadOnlyList<MetricsSnapshot> GetSnapshots(ServerDefinition server, TimeSpan range);

    MetricsSnapshot? GetLatest(ServerDefinition server);
}
