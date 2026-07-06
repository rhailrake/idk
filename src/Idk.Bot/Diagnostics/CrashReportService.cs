using System.Globalization;
using Idk.Bot.Execution;

namespace Idk.Bot.Diagnostics;

public sealed class CrashReportService(IProcessExecutor processExecutor) : ICrashReportService
{
    private const string ContentMarker = "IDK_CRASH_REPORT_CONTENT_BEGIN";

    public async Task<CrashReportLookupResult> GetLatestAsync(ServerDefinition server, CancellationToken cancellationToken)
    {
        var result = await processExecutor.ExecuteAsync(CreateCommand(server), cancellationToken);
        if (!result.Success)
            return new CrashReportLookupResult(server, null, FormatFailure(result));

        return ParseResult(server, result.StandardOutput);
    }

    private static ProcessCommand CreateCommand(ServerDefinition server)
    {
        const string script = """
            set -euo pipefail

            find_running_data_dir() {
                ps -eo user,args --no-headers \
                    | awk -v pat="$IDK_TARGET" '$1=="ss14" && /Robust.Server/ && index($0, pat)>0 { sub(/^[^ ]+[ ]+/, "", $0); print; exit }' \
                    | sed -n 's/.*--data-dir \([^ ]*\).*/\1/p'
            }

            data_dir="$(find_running_data_dir || true)"
            if [ -n "${IDK_CRASH_REPORT_DIR:-}" ]; then
                report_dir="$IDK_CRASH_REPORT_DIR"
            elif [ -n "$data_dir" ]; then
                report_dir="$data_dir/crash-reports"
            else
                echo "Unable to find running server data-dir for $IDK_TARGET." >&2
                exit 10
            fi

            if [ ! -d "$report_dir" ]; then
                echo "Crash report directory does not exist: $report_dir" >&2
                exit 11
            fi

            report="$report_dir/last-crash.log"
            if [ ! -f "$report" ]; then
                report="$(find "$report_dir" -maxdepth 1 -type f -name 'crash_*.log' -printf '%T@ %p\n' | sort -nr | head -n1 | cut -d' ' -f2-)"
            fi

            if [ -z "$report" ] || [ ! -f "$report" ]; then
                echo "No crash report found in $report_dir." >&2
                exit 12
            fi

            printf 'IDK_CRASH_REPORT_PATH=%s\n' "$report"
            printf 'IDK_CRASH_REPORT_MTIME=%s\n' "$(date -r "$report" --iso-8601=seconds 2>/dev/null || stat -c %Y "$report")"
            printf 'IDK_CRASH_REPORT_SIZE=%s\n' "$(wc -c < "$report" | tr -d ' ')"
            printf 'IDK_CRASH_REPORT_CONTENT_BEGIN\n'
            cat "$report"
            """;

        return new ProcessCommand(
            "sudo",
            [
                "-n",
                "-H",
                "-u",
                "ss14",
                "env",
                $"IDK_TARGET={server.LagtraceTarget}",
                $"IDK_CRASH_REPORT_DIR={server.CrashReportDirectory ?? string.Empty}",
                "bash",
                "-lc",
                script,
            ],
            Timeout: TimeSpan.FromSeconds(15));
    }

    private static CrashReportLookupResult ParseResult(ServerDefinition server, string output)
    {
        var markerIndex = output.IndexOf(ContentMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return new CrashReportLookupResult(server, null, "Crash report command returned an invalid response.");

        var header = output[..markerIndex];
        var content = output[(markerIndex + ContentMarker.Length)..].TrimStart('\r', '\n');
        var path = ReadHeaderValue(header, "IDK_CRASH_REPORT_PATH") ?? "<unknown>";
        var mtime = TryParseDateTime(ReadHeaderValue(header, "IDK_CRASH_REPORT_MTIME"));
        var size = TryParseLong(ReadHeaderValue(header, "IDK_CRASH_REPORT_SIZE"));

        return new CrashReportLookupResult(
            server,
            new CrashReport(server, path, mtime, size, content),
            null);
    }

    private static string? ReadHeaderValue(string header, string name)
    {
        var prefix = name + "=";
        foreach (var line in header.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return line[prefix.Length..];
        }

        return null;
    }

    private static DateTimeOffset? TryParseDateTime(string? value)
    {
        if (value == null)
            return null;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            return parsed;

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        return null;
    }

    private static long? TryParseLong(string? value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string FormatFailure(ProcessResult result)
    {
        if (result.TimedOut)
            return "Crash report lookup timed out.";

        var error = result.StandardError.Trim();
        if (!string.IsNullOrWhiteSpace(error))
            return error;

        var output = result.StandardOutput.Trim();
        return string.IsNullOrWhiteSpace(output)
            ? $"Crash report lookup failed with exit code {result.ExitCode}."
            : output;
    }
}
