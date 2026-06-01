using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Diagnostics;

public sealed class MetricsService(
    IServerRegistry serverRegistry,
    MetricsHistoryStore historyStore,
    PrometheusTextParser parser,
    ILogger<MetricsService> logger) : BackgroundService, IMetricsService
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    public IReadOnlyList<MetricsSnapshot> GetSnapshots(ServerDefinition server, TimeSpan range)
    {
        return historyStore.GetSnapshots(server, range);
    }

    public MetricsSnapshot? GetLatest(ServerDefinition server)
    {
        return historyStore.GetLatest(server);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await SampleAllAsync(stoppingToken);
            await Task.Delay(SampleInterval, stoppingToken);
        }
    }

    private async Task SampleAllAsync(CancellationToken cancellationToken)
    {
        foreach (var server in serverRegistry.Servers)
        {
            try
            {
                var text = await _httpClient.GetStringAsync(server.MetricsEndpoint, cancellationToken);
                var snapshot = parser.Parse(server, DateTimeOffset.UtcNow, text);
                historyStore.Add(snapshot);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to scrape metrics for {Server} from {Endpoint}.", server.Id, server.MetricsEndpoint);
            }
        }
    }
}
