using System.Diagnostics;
using Idk.Bot.Configuration;
using Idk.Bot.Execution;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.SelfUpdate;

public sealed class BotMaintenanceService(
    SelfUpdateOptions options,
    IProcessExecutor processExecutor,
    ILogger<BotMaintenanceService> logger) : IBotMaintenanceService
{
    public async Task<BotVersion> GetVersionAsync(CancellationToken cancellationToken)
    {
        var commitTask = ReadGitValueAsync("rev-parse", ["--short", "HEAD"], cancellationToken);
        var branchTask = ReadGitValueAsync("rev-parse", ["--abbrev-ref", "HEAD"], cancellationToken);

        await Task.WhenAll(commitTask, branchTask);

        return new BotVersion(
            NormalizeGitValue(commitTask.Result),
            NormalizeGitValue(branchTask.Result));
    }

    public Task StartUpdateAsync()
    {
        StartDetached(options.UpdateScriptPath);
        return Task.CompletedTask;
    }

    public Task StartRestartAsync()
    {
        StartDetached(options.RestartScriptPath);
        return Task.CompletedTask;
    }

    private async Task<string?> ReadGitValueAsync(string gitCommand, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var allArguments = new List<string> { gitCommand };
        allArguments.AddRange(arguments);

        var result = await processExecutor.ExecuteAsync(
            new ProcessCommand(
                "git",
                allArguments,
                WorkingDirectory: options.RepositoryPath,
                Timeout: TimeSpan.FromSeconds(5)),
            cancellationToken);

        return result.Success
            ? result.StandardOutput
            : null;
    }

    private void StartDetached(string scriptPath)
    {
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("Maintenance script was not found.", scriptPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "bash",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("-lc");
        startInfo.ArgumentList.Add("sleep 1; exec \"$IDK_SCRIPT\"");
        startInfo.Environment["IDK_SCRIPT"] = scriptPath;

        logger.LogInformation("Starting maintenance script: {ScriptPath}", scriptPath);
        Process.Start(startInfo)?.Dispose();
    }

    private static string? NormalizeGitValue(string? value)
    {
        value = value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
