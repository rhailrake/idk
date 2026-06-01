using Idk.Bot.Configuration;

namespace Idk.Bot.Diagnostics;

public sealed class TraceCleanupService(DiagnosticsOptions options) : ITraceCleanupService
{
    public Task<TraceCleanupResult> CleanupAsync(TimeSpan olderThan, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(options.TraceOutputDirectory))
            return Task.FromResult(new TraceCleanupResult(0, 0, 0, Array.Empty<string>()));

        var root = Path.GetFullPath(options.TraceOutputDirectory);
        var cutoff = DateTimeOffset.Now - olderThan;
        var errors = new List<string>();
        var deletedFiles = 0;
        var deletedDirectories = 0;
        long deletedBytes = 0;

        foreach (var file in Directory.EnumerateFiles(root, "lagtrace_*_light.tar.gz", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsInsideRoot(root, file) || File.GetLastWriteTime(file) >= cutoff)
                continue;

            try
            {
                var info = new FileInfo(file);
                deletedBytes += info.Exists ? info.Length : 0;
                File.Delete(file);
                deletedFiles++;
            }
            catch (Exception e)
            {
                errors.Add($"{Path.GetFileName(file)}: {e.Message}");
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(root, "hw_*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsInsideRoot(root, directory) || Directory.GetLastWriteTime(directory) >= cutoff)
                continue;

            try
            {
                deletedBytes += GetDirectorySize(directory);
                Directory.Delete(directory, recursive: true);
                deletedDirectories++;
            }
            catch (Exception e)
            {
                errors.Add($"{Path.GetFileName(directory)}: {e.Message}");
            }
        }

        deletedFiles += DeleteStaleLatestFiles(root, errors, cancellationToken);

        return Task.FromResult(new TraceCleanupResult(deletedFiles, deletedDirectories, deletedBytes, errors));
    }

    private static int DeleteStaleLatestFiles(string root, List<string> errors, CancellationToken cancellationToken)
    {
        var deleted = 0;

        foreach (var file in Directory.EnumerateFiles(root, "idk_latest_*.txt", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsInsideRoot(root, file))
                continue;

            try
            {
                var archivePath = File.ReadAllText(file).Trim();
                if (archivePath.Length != 0 && File.Exists(archivePath))
                    continue;

                File.Delete(file);
                deleted++;
            }
            catch (Exception e)
            {
                errors.Add($"{Path.GetFileName(file)}: {e.Message}");
            }
        }

        return deleted;
    }

    private static long GetDirectorySize(string directory)
    {
        long size = 0;

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            try
            {
                size += new FileInfo(file).Length;
            }
            catch
            {
                // Best effort cleanup accounting.
            }
        }

        return size;
    }

    private static bool IsInsideRoot(string root, string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
