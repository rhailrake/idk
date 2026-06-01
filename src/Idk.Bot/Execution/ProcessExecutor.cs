using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Execution;

public sealed class ProcessExecutor(ILogger<ProcessExecutor> logger) : IProcessExecutor
{
    public async Task<ProcessResult> ExecuteAsync(ProcessCommand command, CancellationToken cancellationToken)
    {
        using var process = CreateProcess(command);
        using var timeoutCts = command.Timeout is { } timeout
            ? new CancellationTokenSource(timeout)
            : new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Starting process: {FileName}", command.FileName);

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            stopwatch.Stop();
            return new ProcessResult(process.ExitCode, stdout, stderr, false, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            KillProcessTree(process);
            stopwatch.Stop();

            return new ProcessResult(-1, await ReadOutputAfterKill(stdoutTask), await ReadOutputAfterKill(stderrTask), true, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            throw;
        }
    }

    private static Process CreateProcess(ProcessCommand command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (!string.IsNullOrWhiteSpace(command.WorkingDirectory))
            startInfo.WorkingDirectory = command.WorkingDirectory;

        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (command.Environment != null)
        {
            foreach (var (key, value) in command.Environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        return new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = false,
        };
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Process may have exited between the timeout and kill call.
        }
    }

    private static async Task<string> ReadOutputAfterKill(Task<string> outputTask)
    {
        try
        {
            return await outputTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch
        {
            return string.Empty;
        }
    }
}
