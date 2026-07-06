using System.Text;
using Discord.WebSocket;
using Idk.Bot.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Commands;

public sealed class CrashReportResponder(ILogger<CrashReportResponder> logger)
{
    private const long FallbackUploadLimit = 25L * 1024L * 1024L;

    public async Task SendReportAsync(SocketSlashCommand command, CrashReport report)
    {
        var bytes = Encoding.UTF8.GetBytes(report.Content);
        var uploadLimit = GetUploadLimit(command);
        if (bytes.Length > uploadLimit)
        {
            await command.ModifyOriginalResponseAsync(message => message.Content = $"""
                Crash report is too large to upload.
                Size: `{FormatBytes(bytes.Length)}`
                Limit: `{FormatBytes(uploadLimit)}`
                Path: `{report.Path}`
                """);
            return;
        }

        await command.ModifyOriginalResponseAsync(message => message.Content = FormatSummary(report));

        try
        {
            await using var stream = new MemoryStream(bytes);
            await command.FollowupWithFileAsync(
                stream,
                BuildFileName(report),
                text: $"`{report.Server.Id}` latest crash report",
                ephemeral: true);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to upload crash report {CrashReportPath}.", report.Path);
            await command.FollowupAsync($"Failed to upload crash report. Path: `{report.Path}`", ephemeral: true);
        }
    }

    private static string FormatSummary(CrashReport report)
    {
        var modified = report.LastModified == null
            ? "unknown"
            : report.LastModified.Value.ToString("yyyy-MM-dd HH:mm:ss zzz");

        var size = report.SizeBytes == null
            ? "unknown"
            : FormatBytes(report.SizeBytes.Value);

        return $"""
            `{report.Server.Id}` latest crash report
            Time: `{modified}`
            Size: `{size}`
            Path: `{report.Path}`
            """;
    }

    private static string BuildFileName(CrashReport report)
    {
        var originalName = Path.GetFileName(report.Path);
        if (string.IsNullOrWhiteSpace(originalName))
            originalName = "last-crash.log";

        return $"{report.Server.Id}_{originalName}";
    }

    private static long GetUploadLimit(SocketSlashCommand command)
    {
        if (command.User is SocketGuildUser guildUser)
        {
            var guildLimit = Convert.ToInt64(guildUser.Guild.MaxUploadLimit);
            return guildLimit > 0 ? guildLimit : FallbackUploadLimit;
        }

        return FallbackUploadLimit;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double) bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
