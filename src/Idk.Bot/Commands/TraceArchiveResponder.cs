using Discord.WebSocket;
using Idk.Bot.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Commands;

public sealed class TraceArchiveResponder(
    ILogger<TraceArchiveResponder> logger)
{
    private const long FallbackUploadLimit = 25L * 1024L * 1024L;

    public async Task SendArchiveAsync(SocketSlashCommand command, TraceArchive archive)
    {
        if (archive.SizeBytes == null)
        {
            await command.FollowupAsync($"Archive file is missing: `{archive.Path}`");
            return;
        }

        var uploadLimit = GetUploadLimit(command);
        if (archive.SizeBytes > uploadLimit)
        {
            await command.FollowupAsync($"""
                Archive is too large to upload.
                Size: `{FormatBytes(archive.SizeBytes.Value)}`
                Limit: `{FormatBytes(uploadLimit)}`
                Path: `{archive.Path}`
                """);
            return;
        }

        try
        {
            await command.FollowupWithFileAsync(
                archive.Path,
                Path.GetFileName(archive.Path),
                $"`{archive.Server.Id}` trace archive");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to upload trace archive {ArchivePath}.", archive.Path);
            await command.FollowupAsync($"Failed to upload archive. Path: `{archive.Path}`");
        }
    }

    private long GetUploadLimit(SocketSlashCommand command)
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
        var value = (double)bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
