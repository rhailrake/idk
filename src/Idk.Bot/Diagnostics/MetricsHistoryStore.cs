namespace Idk.Bot.Diagnostics;

public sealed class MetricsHistoryStore
{
    private static readonly TimeSpan MaxHistory = TimeSpan.FromHours(2);

    private readonly object _lock = new();
    private readonly Dictionary<string, List<MetricsSnapshot>> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public void Add(MetricsSnapshot snapshot)
    {
        lock (_lock)
        {
            if (!_snapshots.TryGetValue(snapshot.Server.Id, out var list))
            {
                list = [];
                _snapshots[snapshot.Server.Id] = list;
            }

            list.Add(snapshot);
            var cutoff = snapshot.CapturedAt - MaxHistory;
            list.RemoveAll(existing => existing.CapturedAt < cutoff);
        }
    }

    public IReadOnlyList<MetricsSnapshot> GetSnapshots(ServerDefinition server, TimeSpan range)
    {
        lock (_lock)
        {
            if (!_snapshots.TryGetValue(server.Id, out var list) || list.Count == 0)
                return [];

            var cutoff = DateTimeOffset.UtcNow - range;
            return list
                .Where(snapshot => snapshot.CapturedAt >= cutoff)
                .OrderBy(snapshot => snapshot.CapturedAt)
                .ToArray();
        }
    }

    public MetricsSnapshot? GetLatest(ServerDefinition server)
    {
        lock (_lock)
        {
            if (!_snapshots.TryGetValue(server.Id, out var list) || list.Count == 0)
                return null;

            return list[^1];
        }
    }
}
