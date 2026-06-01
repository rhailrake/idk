using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Idk.Bot.Diagnostics;

public sealed class TraceAnalysisService : ITraceAnalysisService
{
    private static readonly Regex TopFunctionRegex = new(
        @"^\s*(?<rank>\d+)\.\s+(?<name>.+?)\s+(?<inclusive>[\d.,]+)%\s+(?<exclusive>[\d.,]+)%\s*$",
        RegexOptions.Compiled);

    private static readonly string[] NoiseMarkers =
    [
        "LowLevelLifoSemaphore",
        "PortableThreadPool",
        "GateThread",
        "SocketAsyncEngine.EventLoop",
        "CounterGroup.PollForValues",
        "TimerQueue.TimerThread",
        "PrecisionSleep",
        "PollGC",
        "WaitHandle",
        "ManualResetEventSlim",
        "Monitor.Enter",
        "NativeRuntimeEventSource",
        "Thread.Sleep",
        "Task.SpinWait",
        "Interop+Kernel32"
    ];

    public async Task<TraceAnalysisResult> AnalyzeAsync(TraceArchive archive, CancellationToken cancellationToken)
    {
        if (archive.SizeBytes == null || !File.Exists(archive.Path))
            return CreateFailure(archive, "Archive file is missing.");

        Dictionary<string, string> entries;
        try
        {
            entries = await ReadInterestingEntriesAsync(archive.Path, cancellationToken);
        }
        catch (Exception e)
        {
            return CreateFailure(archive, $"Failed to read archive: {e.Message}");
        }

        if (!entries.TryGetValue("top.txt", out var topText))
            return CreateFailure(archive, "Archive does not contain top.txt.");

        var functions = ParseTopFunctions(topText);
        var hotFunctions = functions
            .Where(function => !IsNoise(function.Name))
            .Take(10)
            .ToArray();

        var categories = functions
            .Where(function => !IsNoise(function.Name))
            .GroupBy(function => function.Category)
            .Select(group => new TraceCategorySample(group.Key, group.Sum(function => function.ExclusivePercent)))
            .Where(category => category.ExclusivePercent > 0)
            .OrderByDescending(category => category.ExclusivePercent)
            .Take(8)
            .ToArray();

        var process = ParseProcessSummary(
            entries.GetValueOrDefault("selected-process.txt"),
            entries.GetValueOrDefault("pidstat-process.txt"));

        var hardware = ParseHardwareSummary(
            entries.GetValueOrDefault("uptime.txt"),
            entries.GetValueOrDefault("vmstat.txt"));

        var threads = ParseThreadSamples(entries.GetValueOrDefault("pidstat-threads.txt"))
            .Take(8)
            .ToArray();

        return new TraceAnalysisResult(
            archive,
            true,
            null,
            process,
            hardware,
            threads,
            hotFunctions,
            categories);
    }

    private static TraceAnalysisResult CreateFailure(TraceArchive archive, string error)
    {
        return new TraceAnalysisResult(
            archive,
            false,
            error,
            null,
            null,
            [],
            [],
            []);
    }

    private static async Task<Dictionary<string, string>> ReadInterestingEntriesAsync(string archivePath, CancellationToken cancellationToken)
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await using var file = File.OpenRead(archivePath);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);

        TarEntry? entry;
        while ((entry = tar.GetNextEntry()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = entry.Name.Replace('\\', '/');
            var fileName = name.Split('/').Last();
            if (!IsInterestingFile(fileName) || entry.DataStream == null)
                continue;

            using var reader = new StreamReader(entry.DataStream);
            entries[fileName] = await reader.ReadToEndAsync(cancellationToken);
        }

        return entries;
    }

    private static bool IsInterestingFile(string fileName)
    {
        return fileName is
            "top.txt" or
            "selected-process.txt" or
            "pidstat-process.txt" or
            "pidstat-threads.txt" or
            "vmstat.txt" or
            "uptime.txt" or
            "free.txt";
    }

    private static IReadOnlyList<TraceFunctionSample> ParseTopFunctions(string topText)
    {
        var functions = new List<TraceFunctionSample>();

        foreach (var line in ReadLines(topText))
        {
            var match = TopFunctionRegex.Match(line);
            if (!match.Success)
                continue;

            var name = match.Groups["name"].Value.Trim();
            functions.Add(new TraceFunctionSample(
                int.Parse(match.Groups["rank"].Value, CultureInfo.InvariantCulture),
                name,
                CategorizeFunction(name),
                ParseNumber(match.Groups["inclusive"].Value),
                ParseNumber(match.Groups["exclusive"].Value)));
        }

        return functions;
    }

    private static TraceProcessSummary? ParseProcessSummary(string? selectedProcessText, string? pidstatProcessText)
    {
        int? processId = null;
        string? user = null;
        double? snapshotCpu = null;
        double? snapshotMemory = null;
        string? elapsed = null;

        foreach (var line in ReadLines(selectedProcessText))
        {
            var parts = SplitFields(line);
            if (parts.Length < 6 || !int.TryParse(parts[0], out var parsedPid))
                continue;

            processId = parsedPid;
            user = parts[1];
            snapshotCpu = ParseNullableNumber(parts[2]);
            snapshotMemory = ParseNullableNumber(parts[3]);
            elapsed = parts[4];
            break;
        }

        var samples = new List<(double User, double System, double Wait, double Cpu)>();
        foreach (var line in ReadLines(pidstatProcessText))
        {
            var parts = SplitFields(line);
            if (parts.Length != 10 || !parts[^1].Equals("Robust.Server", StringComparison.Ordinal))
                continue;

            var userCpu = ParseNullableNumber(parts[3]);
            var systemCpu = ParseNullableNumber(parts[4]);
            var waitCpu = ParseNullableNumber(parts[6]);
            var cpu = ParseNullableNumber(parts[7]);
            if (userCpu == null || systemCpu == null || waitCpu == null || cpu == null)
                continue;

            samples.Add((userCpu.Value, systemCpu.Value, waitCpu.Value, cpu.Value));
        }

        if (processId == null && samples.Count == 0)
            return null;

        return new TraceProcessSummary(
            processId,
            user,
            snapshotCpu,
            snapshotMemory,
            elapsed,
            samples.Count == 0 ? null : samples.Average(sample => sample.Cpu),
            samples.Count == 0 ? null : samples.Max(sample => sample.Cpu),
            samples.Count == 0 ? null : samples.Average(sample => sample.User),
            samples.Count == 0 ? null : samples.Average(sample => sample.System),
            samples.Count == 0 ? null : samples.Average(sample => sample.Wait));
    }

    private static TraceHardwareSummary? ParseHardwareSummary(string? uptimeText, string? vmstatText)
    {
        var samples = new List<(int RunQueue, double User, double System, double Idle, double Wait)>();

        foreach (var line in ReadLines(vmstatText))
        {
            var parts = SplitFields(line);
            if (parts.Length < 17 || !int.TryParse(parts[0], out var runQueue))
                continue;

            var user = ParseNullableNumber(parts[12]);
            var system = ParseNullableNumber(parts[13]);
            var idle = ParseNullableNumber(parts[14]);
            var wait = ParseNullableNumber(parts[15]);
            if (user == null || system == null || idle == null || wait == null)
                continue;

            samples.Add((runQueue, user.Value, system.Value, idle.Value, wait.Value));
        }

        var uptime = ReadLines(uptimeText).FirstOrDefault();
        if (uptime == null && samples.Count == 0)
            return null;

        return new TraceHardwareSummary(
            uptime,
            samples.Count == 0 ? null : samples.Average(sample => sample.Idle),
            samples.Count == 0 ? null : samples.Average(sample => sample.Wait),
            samples.Count == 0 ? null : samples.Average(sample => sample.User),
            samples.Count == 0 ? null : samples.Average(sample => sample.System),
            samples.Count == 0 ? null : samples.Average(sample => sample.RunQueue),
            samples.Count == 0 ? null : samples.Max(sample => sample.RunQueue));
    }

    private static IReadOnlyList<TraceThreadSample> ParseThreadSamples(string? pidstatThreadsText)
    {
        var samples = new List<(int ThreadId, string Name, double Cpu)>();

        foreach (var line in ReadLines(pidstatThreadsText))
        {
            var parts = SplitFields(line);
            if (parts.Length < 11 || parts[2] != "-" || !int.TryParse(parts[3], out var threadId))
                continue;

            var cpu = ParseNullableNumber(parts[8]);
            if (cpu == null)
                continue;

            var marker = line.IndexOf("|__", StringComparison.Ordinal);
            var name = marker >= 0
                ? line[(marker + 3)..].Trim()
                : parts[^1];

            samples.Add((threadId, name, cpu.Value));
        }

        return samples
            .GroupBy(sample => sample.ThreadId)
            .Select(group => new TraceThreadSample(
                group.First().Name,
                group.Average(sample => sample.Cpu),
                group.Max(sample => sample.Cpu),
                group.Count()))
            .Where(sample => sample.AverageCpuPercent > 0)
            .OrderByDescending(sample => sample.AverageCpuPercent)
            .ToArray();
    }

    private static string CategorizeFunction(string name)
    {
        if (ContainsAny(name, "PvsSystem", "PvsChunk"))
            return "PVS";

        if (ContainsAny(name, "NetSerializer", "RobustSerializer", "MsgEntity", "ZStd", "WriteToBuffer"))
            return "Serialization";

        if (ContainsAny(name, "NetPeer", "NetConnection", "NetReliableSenderChannel", "NetUnreliableSenderChannel", "Lidgren", "NetEncryption"))
            return "Network";

        if (ContainsAny(name, "Physics", "MoverController", "BroadPhase", "EntityLookupSystem"))
            return "Physics/Movement";

        if (ContainsAny(name, "AtmosphereSystem", "AtmosMonitor", "Gas"))
            return "Atmos";

        if (ContainsAny(name, "PowerNetSystem", "Battery", "ApcSystem"))
            return "Power";

        if (ContainsAny(name, "NPC", "Pathfinding", "HTNSystem"))
            return "NPC";

        return "Other";
    }

    private static bool IsNoise(string name)
    {
        return ContainsAny(name, NoiseMarkers);
    }

    private static bool ContainsAny(string value, params string[] markers)
    {
        return markers.Any(marker => value.Contains(marker, StringComparison.Ordinal));
    }

    private static IEnumerable<string> ReadLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
            yield return line;
    }

    private static string[] SplitFields(string line)
    {
        return line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    private static double ParseNumber(string value)
    {
        return double.Parse(NormalizeNumber(value), CultureInfo.InvariantCulture);
    }

    private static double? ParseNullableNumber(string value)
    {
        return double.TryParse(NormalizeNumber(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static string NormalizeNumber(string value)
    {
        return value.Replace(',', '.');
    }
}
