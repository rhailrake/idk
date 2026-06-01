using Discord.WebSocket;
using Idk.Bot.Configuration;
using Idk.Bot.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Commands;

public sealed class PerfCommandHandler(
    DiagnosticsOptions diagnosticsOptions,
    IServerRegistry serverRegistry,
    IPerfStatusService perfStatusService,
    IPerfTraceService perfTraceService,
    ITraceArchiveStore archiveStore,
    ITraceAnalysisService traceAnalysisService,
    ITraceCleanupService cleanupService,
    TraceArchiveResponder archiveResponder,
    TraceAnalysisResponder analysisResponder,
    ILogger<PerfCommandHandler> logger)
{
    private const int DefaultCleanupDays = 7;

    public async Task HandleAsync(SocketSlashCommand command)
    {
        var subCommand = command.Data.Options.FirstOrDefault();
        if (subCommand == null)
        {
            await command.RespondAsync("Unknown perf command.", ephemeral: true);
            return;
        }

        switch (subCommand.Name)
        {
            case "status":
                await HandleStatusAsync(command, subCommand);
                return;
            case "trace":
                await HandleTraceAsync(command, subCommand);
                return;
            case "latest":
                await HandleLatestAsync(command, subCommand);
                return;
            case "analyze":
                await HandleAnalyzeAsync(command, subCommand);
                return;
            case "cleanup":
                await HandleCleanupAsync(command, subCommand);
                return;
            default:
                await command.RespondAsync("Unknown perf command.", ephemeral: true);
                return;
        }
    }

    private async Task HandleStatusAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var server = await ReadServerAsync(command, subCommand);
        if (server == null)
            return;

        await command.DeferAsync();

        try
        {
            var status = await perfStatusService.GetStatusAsync(server, CancellationToken.None);
            await command.ModifyOriginalResponseAsync(message => message.Content = FormatStatus(status));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get perf status for {Server}.", server.Id);
            await command.ModifyOriginalResponseAsync(message => message.Content = $"Failed to get status for `{server.Id}`.");
        }
    }

    private async Task HandleTraceAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var server = await ReadServerAsync(command, subCommand);
        if (server == null)
            return;

        var seconds = GetIntOption(subCommand, "seconds") ?? (int)diagnosticsOptions.DefaultTraceDuration.TotalSeconds;
        if (seconds is < 1 or > 600)
        {
            await command.RespondAsync("Trace seconds must be between 1 and 600.");
            return;
        }

        await command.DeferAsync();

        try
        {
            var result = await perfTraceService.CollectTraceAsync(server, TimeSpan.FromSeconds(seconds), CancellationToken.None);

            if (result.AlreadyRunning)
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = $"Trace is already running for `{server.Id}`.");
                return;
            }

            await command.ModifyOriginalResponseAsync(message => message.Content = FormatTraceResult(result));

            if (result.Success && result.ArchivePath != null)
                await archiveResponder.SendArchiveAsync(command, archiveStore.GetArchive(server, result.ArchivePath));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to collect trace for {Server}.", server.Id);
            await command.ModifyOriginalResponseAsync(message => message.Content = $"Failed to collect trace for `{server.Id}`.");
        }
    }

    private async Task HandleLatestAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var server = await ReadServerAsync(command, subCommand);
        if (server == null)
            return;

        await command.DeferAsync();

        try
        {
            var archive = await archiveStore.GetLatestAsync(server, CancellationToken.None);
            await command.ModifyOriginalResponseAsync(message => message.Content = FormatLatestArchive(server, archive));

            if (archive?.SizeBytes != null)
                await archiveResponder.SendArchiveAsync(command, archive);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get latest trace for {Server}.", server.Id);
            await command.ModifyOriginalResponseAsync(message => message.Content = $"Failed to get latest trace for `{server.Id}`.");
        }
    }

    private async Task HandleAnalyzeAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var server = await ReadServerAsync(command, subCommand);
        if (server == null)
            return;

        var trace = GetStringOption(subCommand, "trace");

        await command.DeferAsync();

        try
        {
            var archive = await archiveStore.ResolveAsync(server, trace, CancellationToken.None);
            if (archive == null)
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = $"Trace archive not found for `{server.Id}`.");
                return;
            }

            var result = await traceAnalysisService.AnalyzeAsync(archive, CancellationToken.None);
            await analysisResponder.SendAnalysisAsync(command, result);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to analyze trace for {Server}.", server.Id);
            await command.ModifyOriginalResponseAsync(message => message.Content = $"Failed to analyze trace for `{server.Id}`.");
        }
    }

    private async Task HandleCleanupAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var days = GetIntOption(subCommand, "days") ?? DefaultCleanupDays;
        if (days is < 0 or > 365)
        {
            await command.RespondAsync("Cleanup days must be between 0 and 365.");
            return;
        }

        await command.DeferAsync();

        try
        {
            var result = await cleanupService.CleanupAsync(TimeSpan.FromDays(days), CancellationToken.None);
            await command.ModifyOriginalResponseAsync(message => message.Content = FormatCleanupResult(days, result));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to cleanup traces.");
            await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to cleanup traces.");
        }
    }

    private async Task<ServerDefinition?> ReadServerAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var serverId = GetStringOption(subCommand, "server");
        if (serverId != null && serverRegistry.TryGetServer(serverId, out var server))
            return server;

        await command.RespondAsync("Unknown server.", ephemeral: true);
        return null;
    }

    private static string? GetStringOption(SocketSlashCommandDataOption subCommand, string name)
    {
        return subCommand.Options.FirstOrDefault(option => option.Name == name)?.Value as string;
    }

    private static int? GetIntOption(SocketSlashCommandDataOption subCommand, string name)
    {
        var value = subCommand.Options.FirstOrDefault(option => option.Name == name)?.Value;
        return value switch
        {
            int integer => integer,
            long integer => (int)integer,
            _ => null,
        };
    }

    private static string FormatStatus(PerfStatus status)
    {
        if (!status.Found)
        {
            var details = string.IsNullOrWhiteSpace(status.Details)
                ? string.Empty
                : $"{Environment.NewLine}Details: `{TrimForDiscord(status.Details, 700)}`";

            return $"`{status.Server.Id}`: Robust.Server process not found.{details}";
        }

        return $"""
            `{status.Server.Id}` status
            PID: `{status.ProcessId}`
            User: `{status.User}`
            CPU: `{status.CpuPercent:0.##}%`
            RAM: `{status.MemoryPercent:0.##}%`
            Uptime: `{status.Elapsed}`
            """;
    }

    private static string FormatTraceResult(PerfTraceResult result)
    {
        if (!result.Success)
        {
            var error = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;

            return $"""
                `{result.Server.Id}` trace failed
                Duration: `{FormatDuration(result.Duration)}`
                Error: `{TrimForDiscord(error, 900)}`
                """;
        }

        return $"""
            `{result.Server.Id}` trace complete
            Duration: `{FormatDuration(result.Duration)}`
            Archive: `{result.ArchivePath}`
            """;
    }

    private static string FormatLatestArchive(ServerDefinition server, TraceArchive? archive)
    {
        if (archive == null)
            return $"`{server.Id}`: no trace archive recorded yet.";

        if (archive.SizeBytes == null)
            return $"""
                `{server.Id}` latest trace
                Archive: `{archive.Path}`
                File is missing on disk.
                """;

        return $"""
            `{server.Id}` latest trace
            Archive: `{archive.Path}`
            Size: `{FormatBytes(archive.SizeBytes.Value)}`
            Modified: `{archive.LastWriteTime:yyyy-MM-dd HH:mm:ss}`
            """;
    }

    private static string FormatCleanupResult(int days, TraceCleanupResult result)
    {
        var errors = result.Errors.Count == 0
            ? string.Empty
            : $"{Environment.NewLine}Errors: `{TrimForDiscord(string.Join("; ", result.Errors), 900)}`";

        return $"""
            Trace cleanup complete
            Older than: `{days} day(s)`
            Deleted files: `{result.DeletedFiles}`
            Deleted directories: `{result.DeletedDirectories}`
            Freed: `{FormatBytes(result.DeletedBytes)}`
            {errors}
            """;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalSeconds < 60
            ? $"{duration.TotalSeconds:0.0}s"
            : $"{duration.TotalMinutes:0.0}m";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private static string TrimForDiscord(string value, int maxLength)
    {
        value = value.Trim();
        if (value.Length <= maxLength)
            return value;

        return value[..maxLength] + "...";
    }
}
