namespace Idk.Bot.Diagnostics;

public interface IPhysicsDiagnosticsService
{
    Task<string> GetDiagnosticsAsync(ServerDefinition server, int limit, CancellationToken cancellationToken);
}
