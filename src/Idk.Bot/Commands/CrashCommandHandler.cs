using Discord.WebSocket;
using Idk.Bot.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Commands;

public sealed class CrashCommandHandler(
    IServerRegistry serverRegistry,
    ICrashReportService crashReportService,
    CrashReportResponder crashReportResponder,
    ILogger<CrashCommandHandler> logger)
{
    public async Task HandleAsync(SocketSlashCommand command)
    {
        var subCommand = command.Data.Options.FirstOrDefault();
        if (subCommand == null)
        {
            await command.RespondAsync("No subcommand specified.", ephemeral: true);
            return;
        }

        switch (subCommand.Name)
        {
            case "latest":
                await HandleLatestAsync(command, subCommand);
                return;
            default:
                await command.RespondAsync("Unknown subcommand.", ephemeral: true);
                return;
        }
    }

    private async Task HandleLatestAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var server = await ReadServerAsync(command, subCommand);
        if (server == null)
            return;

        await command.DeferAsync(ephemeral: true);

        try
        {
            var result = await crashReportService.GetLatestAsync(server, CancellationToken.None);
            if (!result.Success || result.Report == null)
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = $"""
                    No crash report found for `{server.Id}`.
                    {result.Error ?? "Server has not written a managed crash report yet."}
                    """);
                return;
            }

            await crashReportResponder.SendReportAsync(command, result.Report);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get latest crash report for {Server}.", server.Id);
            await command.ModifyOriginalResponseAsync(message => message.Content = $"Failed to get latest crash report for `{server.Id}`.");
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
}
