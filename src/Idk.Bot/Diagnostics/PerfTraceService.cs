using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Idk.Bot.Configuration;
using Idk.Bot.Execution;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Diagnostics;

public sealed partial class PerfTraceService(
    DiagnosticsOptions options,
    IProcessExecutor processExecutor,
    ITraceArchiveStore archiveStore,
    ILogger<PerfTraceService> logger) : IPerfTraceService
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
            var traceDirectory = result.Success ? TryFindTraceDirectory(result.StandardOutput) : null;

            if (archivePath != null)
                await archiveStore.SaveLatestAsync(server, archivePath, cancellationToken);

            if (archivePath != null && traceDirectory != null)
                await CleanupTraceDirectoryAsync(traceDirectory, cancellationToken);

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

    private async Task CleanupTraceDirectoryAsync(string traceDirectory, CancellationToken cancellationToken)
    {
        if (!IsSafeTraceDirectory(traceDirectory))
        {
            logger.LogWarning("Refusing to cleanup unsafe trace directory: {TraceDirectory}", traceDirectory);
            return;
        }

        const string script = """
            set -euo pipefail
            case "$TRACE_DIR" in
                "$HOME"/ss14-traces/lagtrace_*) rm -rf -- "$TRACE_DIR" ;;
                *) exit 2 ;;
            esac
            """;

        var result = await processExecutor.ExecuteAsync(
            new ProcessCommand(
                "sudo",
                ["-n", "-H", "-u", "ss14", "env", $"TRACE_DIR={traceDirectory}", "bash", "-lc", script],
                Timeout: TimeSpan.FromMinutes(2)),
            cancellationToken);

        if (!result.Success)
        {
            logger.LogWarning(
                "Failed to cleanup trace directory {TraceDirectory}. Exit={ExitCode} Error={Error}",
                traceDirectory,
                result.ExitCode,
                result.StandardError.Trim());
        }
    }

    private static bool IsSafeTraceDirectory(string traceDirectory)
    {
        return traceDirectory.StartsWith("/home/ss14/ss14-traces/lagtrace_", StringComparison.Ordinal) &&
               !traceDirectory.Contains("..", StringComparison.Ordinal) &&
               !traceDirectory.Any(char.IsControl);
    }

    [GeneratedRegex(@"\/\S+\.tar\.gz", RegexOptions.Compiled)]
    private static partial Regex ArchivePathRegex();

    private static string? TryFindTraceDirectory(string output)
    {
        var match = TraceDirectoryRegex().Match(output);
        return match.Success ? match.Groups["path"].Value : null;
    }

    [GeneratedRegex(@"(?m)^Trace dir:\s*(?<path>\/\S+)\s*$", RegexOptions.Compiled)]
    private static partial Regex TraceDirectoryRegex();
}
