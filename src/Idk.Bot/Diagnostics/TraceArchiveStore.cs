using Idk.Bot.Configuration;

namespace Idk.Bot.Diagnostics;

public sealed class TraceArchiveStore(DiagnosticsOptions options) : ITraceArchiveStore
{
    public async Task SaveLatestAsync(ServerDefinition server, string archivePath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.TraceOutputDirectory);
        await File.WriteAllTextAsync(GetLatestFilePath(server), archivePath, cancellationToken);
    }

    public async Task<TraceArchive?> GetLatestAsync(ServerDefinition server, CancellationToken cancellationToken)
    {
        var latestFile = GetLatestFilePath(server);
        if (!File.Exists(latestFile))
            return null;

        var archivePath = (await File.ReadAllTextAsync(latestFile, cancellationToken)).Trim();
        if (archivePath.Length == 0)
            return null;

        return GetArchive(server, archivePath);
    }

    public async Task<TraceArchive?> ResolveAsync(ServerDefinition server, string? trace, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trace) || trace.Equals("latest", StringComparison.OrdinalIgnoreCase))
            return await GetLatestAsync(server, cancellationToken);

        var archivePath = ResolveArchivePath(trace);
        return archivePath == null
            ? null
            : GetArchive(server, archivePath);
    }

    public TraceArchive GetArchive(ServerDefinition server, string archivePath)
    {
        if (!File.Exists(archivePath))
            return new TraceArchive(server, archivePath, null, null);

        var info = new FileInfo(archivePath);
        return new TraceArchive(server, archivePath, info.Length, info.LastWriteTime);
    }

    private string GetLatestFilePath(ServerDefinition server)
    {
        return Path.Combine(options.TraceOutputDirectory, $"idk_latest_{server.Id}.txt");
    }

    private string? ResolveArchivePath(string trace)
    {
        var root = Path.GetFullPath(options.TraceOutputDirectory);
        var value = trace.Trim().Trim('"', '\'');
        var candidates = new List<string>();

        AddCandidate(value);

        if (!value.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            AddCandidate(value + ".tar.gz");

        string? firstValidPath = null;
        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (!IsUnderRoot(fullPath, root))
                continue;

            firstValidPath ??= fullPath;
            if (File.Exists(fullPath))
                return fullPath;
        }

        return firstValidPath;

        void AddCandidate(string candidate)
        {
            candidates.Add(Path.IsPathRooted(candidate)
                ? candidate
                : Path.Combine(root, candidate));
        }
    }

    private static bool IsUnderRoot(string path, string root)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                             + Path.DirectorySeparatorChar;

        return path.StartsWith(normalizedRoot, StringComparison.Ordinal);
    }
}
