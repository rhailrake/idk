using System.Globalization;
using Idk.Bot.Execution;

namespace Idk.Bot.Diagnostics;

public sealed class PerfStatusService(IProcessExecutor processExecutor) : IPerfStatusService
{
    public async Task<PerfStatus> GetStatusAsync(ServerDefinition server, CancellationToken cancellationToken)
    {
        var result = await processExecutor.ExecuteAsync(CreateStatusCommand(server), cancellationToken);
        if (!result.Success)
            return new PerfStatus(server, false, null, null, null, null, null, result.StandardError.Trim());

        var line = result.StandardOutput.Trim();
        if (line.Length == 0)
            return new PerfStatus(server, false, null, null, null, null, null, null);

        return ParseStatusLine(server, line);
    }

    private static ProcessCommand CreateStatusCommand(ServerDefinition server)
    {
        const string script = """
            ps -eo pid,user,pcpu,pmem,etime,args --no-headers \
                | awk -v pat="$IDK_TARGET" '$2=="ss14" && /Robust.Server/ && index($0, pat)>0 { if (($3+0)>max) { max=$3+0; line=$0 } } END { print line }' \
                | sed -E 's/(watchdog\.token=)[^ ]+/\1REDACTED/g'
            """;

        return new ProcessCommand(
            "bash",
            ["-lc", script],
            Environment: new Dictionary<string, string>
            {
                ["IDK_TARGET"] = server.LagtraceTarget,
            },
            Timeout: TimeSpan.FromSeconds(10));
    }

    private static PerfStatus ParseStatusLine(ServerDefinition server, string line)
    {
        var parts = line.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 6)
            return new PerfStatus(server, false, null, null, null, null, null, line);

        var pid = int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPid)
            ? parsedPid
            : (int?)null;

        var cpu = double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedCpu)
            ? parsedCpu
            : (double?)null;

        var memory = double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMemory)
            ? parsedMemory
            : (double?)null;

        return new PerfStatus(server, true, pid, parts[1], cpu, memory, parts[4], parts[5]);
    }
}
