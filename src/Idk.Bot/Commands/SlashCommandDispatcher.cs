using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Commands;

public sealed class SlashCommandDispatcher(
    CommandAccessService accessService,
    PerfCommandHandler perfCommandHandler,
    BotCommandHandler botCommandHandler,
    ILogger<SlashCommandDispatcher> logger)
{
    public async Task HandleAsync(SocketSlashCommand command)
    {
        try
        {
            if (!accessService.CanUse(command.User))
            {
                await command.RespondAsync("Access denied.", ephemeral: true);
                return;
            }

            switch (command.CommandName)
            {
                case "perf":
                    await perfCommandHandler.HandleAsync(command);
                    return;
                case "bot":
                    await botCommandHandler.HandleAsync(command);
                    return;
                default:
                    await command.RespondAsync("Unknown command.", ephemeral: true);
                    return;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to handle slash command {Command}.", command.CommandName);

            if (command.HasResponded)
                await command.ModifyOriginalResponseAsync(message => message.Content = "Command failed.");
            else
                await command.RespondAsync("Command failed.", ephemeral: true);
        }
    }
}
