namespace Idk.Bot.Diagnostics;

public interface IPerfTraceService
{
    Task<PerfTraceResult> CollectTraceAsync(ServerDefinition server, TimeSpan duration, CancellationToken cancellationToken);
}
