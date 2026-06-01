namespace Idk.Bot.Diagnostics;

public interface ITraceAnalysisService
{
    Task<TraceAnalysisResult> AnalyzeAsync(TraceArchive archive, CancellationToken cancellationToken);
}
