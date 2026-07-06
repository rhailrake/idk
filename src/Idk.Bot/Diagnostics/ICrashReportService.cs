namespace Idk.Bot.Diagnostics;

public interface ICrashReportService
{
    Task<CrashReportLookupResult> GetLatestAsync(ServerDefinition server, CancellationToken cancellationToken);
}
