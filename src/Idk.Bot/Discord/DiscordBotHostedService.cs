using Discord;
using Discord.WebSocket;
using Idk.Bot.Commands;
using Idk.Bot.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Discord;

public sealed class DiscordBotHostedService(
    BotOptions options,
    DiscordSocketClient client,
    DiscordLogForwarder logForwarder,
    SlashCommandRegistrar commandRegistrar,
    SlashCommandDispatcher commandDispatcher,
    ILogger<DiscordBotHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        client.Log += logForwarder.ForwardAsync;
        client.Ready += OnReadyAsync;
        client.SlashCommandExecuted += commandDispatcher.HandleAsync;

        await client.LoginAsync(TokenType.Bot, options.DiscordToken);
        await client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        client.SlashCommandExecuted -= commandDispatcher.HandleAsync;
        client.Ready -= OnReadyAsync;
        client.Log -= logForwarder.ForwardAsync;

        try
        {
            await client.StopAsync();
            await client.LogoutAsync();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to stop Discord client cleanly.");
        }
    }

    private Task OnReadyAsync()
    {
        logger.LogInformation("Logged in as {User} ({UserId}).", client.CurrentUser.Username, client.CurrentUser.Id);
        return commandRegistrar.RegisterAsync();
    }
}
