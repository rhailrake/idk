namespace Idk.Bot.Diagnostics;

public interface IPerfStatusService
{
    Task<PerfStatus> GetStatusAsync(ServerDefinition server, CancellationToken cancellationToken);
}
