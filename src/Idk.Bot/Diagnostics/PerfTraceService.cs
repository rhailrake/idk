using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Idk.Bot.Configuration;
using Idk.Bot.Execution;

namespace Idk.Bot.Diagnostics;

public sealed partial class PerfTraceService(
    DiagnosticsOptions options,
    IProcessExecutor processExecutor,
    ITraceArchiveStore archiveStore) : IPerfTraceService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _serverLocks = new(StringComparer.OrdinalIgnoreCase);

    public async Task<PerfTraceResult> CollectTraceAsync(ServerDefinition server, TimeSpan duration, CancellationToken cancellationToken)
    {
        var serverLock = _serverLocks.GetOrAdd(server.Id, _ => new SemaphoreSlim(1, 1));
        if (!await serverLock.WaitAsync(0, cancellationToken))
            return new PerfTraceResult(server, false, true, null, string.Empty, string.Empty, TimeSpan.Zero);

        try
        {
            var result = await processExecutor.ExecuteAsync(CreateTraceCommand(server, duration), cancellationToken);
            var archivePath = result.Success ? TryFindArchivePath(result.StandardOutput) : null;

            if (archivePath != null)
                await archiveStore.SaveLatestAsync(server, archivePath, cancellationToken);

            return new PerfTraceResult(
                server,
                result.Success && archivePath != null,
                false,
                archivePath,
                result.StandardOutput,
                result.StandardError,
                result.Duration);
        }
        finally
        {
            serverLock.Release();
        }
    }

    private ProcessCommand CreateTraceCommand(ServerDefinition server, TimeSpan duration)
    {
        const string script = """
            TRACE_SECONDS="$IDK_TRACE_SECONDS" bash "$IDK_LAGTRACE_PATH" "$IDK_TARGET"
            """;

        return new ProcessCommand(
            "bash",
            ["-lc", script],
            Environment: new Dictionary<string, string>
            {
                ["IDK_LAGTRACE_PATH"] = options.LagtracePath,
                ["IDK_TARGET"] = server.LagtraceTarget,
                ["IDK_TRACE_SECONDS"] = ((int)duration.TotalSeconds).ToString(),
            },
            Timeout: duration + TimeSpan.FromMinutes(10));
    }

    private static string? TryFindArchivePath(string output)
    {
        var matches = ArchivePathRegex().Matches(output);
        return matches.Count == 0
            ? null
            : matches[^1].Value;
    }

    [GeneratedRegex(@"\/\S+\.tar\.gz", RegexOptions.Compiled)]
    private static partial Regex ArchivePathRegex();
}
