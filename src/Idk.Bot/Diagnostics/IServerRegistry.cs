namespace Idk.Bot.Diagnostics;

public interface IServerRegistry
{
    IReadOnlyCollection<ServerDefinition> Servers { get; }

    bool TryGetServer(string id, out ServerDefinition server);
}
