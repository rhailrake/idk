using Discord.WebSocket;
using Idk.Bot.SelfUpdate;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Commands;

public sealed class BotCommandHandler(
    IBotMaintenanceService maintenanceService,
    ILogger<BotCommandHandler> logger)
{
    public async Task HandleAsync(SocketSlashCommand command)
    {
        var subCommand = command.Data.Options.FirstOrDefault();
        if (subCommand == null)
        {
            await command.RespondAsync("Unknown bot command.", ephemeral: true);
            return;
        }

        switch (subCommand.Name)
        {
            case "version":
                await HandleVersionAsync(command);
                return;
            case "update":
                await HandleUpdateAsync(command);
                return;
            case "restart":
                await HandleRestartAsync(command);
                return;
            default:
                await command.RespondAsync("Unknown bot command.", ephemeral: true);
                return;
        }
    }

    private async Task HandleVersionAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        try
        {
            var version = await maintenanceService.GetVersionAsync(CancellationToken.None);
            await command.ModifyOriginalResponseAsync(message => message.Content = FormatVersion(version));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get bot version.");
            await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to get bot version.");
        }
    }

    private async Task HandleUpdateAsync(SocketSlashCommand command)
    {
        try
        {
            await maintenanceService.StartUpdateAsync();
            await command.RespondAsync("Update started.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to start bot update.");
            await command.RespondAsync("Failed to start update.");
        }
    }

    private async Task HandleRestartAsync(SocketSlashCommand command)
    {
        try
        {
            await maintenanceService.StartRestartAsync();
            await command.RespondAsync("Restart started.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to start bot restart.");
            await command.RespondAsync("Failed to start restart.");
        }
    }

    private static string FormatVersion(BotVersion version)
    {
        return $"""
            `idk` version
            Branch: `{version.Branch ?? "unknown"}`
            Commit: `{version.Commit ?? "unknown"}`
            """;
    }
}
