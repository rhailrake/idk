namespace Idk.Bot.Diagnostics;

public sealed class PhysicsDiagnosticsService : IPhysicsDiagnosticsService
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public Task<string> GetDiagnosticsAsync(ServerDefinition server, int limit, CancellationToken cancellationToken)
    {
        var endpoint = new UriBuilder(server.PhysicsDiagnosticsEndpoint)
        {
            Query = $"limit={limit}",
        };

        return _httpClient.GetStringAsync(endpoint.Uri, cancellationToken);
    }
}
