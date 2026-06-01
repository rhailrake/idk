namespace Idk.Bot.Diagnostics;

public sealed class StaticServerRegistry : IServerRegistry
{
    private readonly Dictionary<string, ServerDefinition> _servers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["titan"] = new ServerDefinition("titan", "Titan", "Watchdog.titan"),
        ["fobos"] = new ServerDefinition("fobos", "Fobos", "Watchdog.fobos"),
    };

    public IReadOnlyCollection<ServerDefinition> Servers => _servers.Values;

    public bool TryGetServer(string id, out ServerDefinition server)
    {
        return _servers.TryGetValue(id, out server!);
    }
}
