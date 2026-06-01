using Discord;
using Discord.WebSocket;

namespace Idk.Bot.Discord;

public static class DiscordClientFactory
{
    public static DiscordSocketClient Create()
    {
        return new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds,
            LogLevel = LogSeverity.Info,
            AlwaysDownloadUsers = false,
        });
    }
}
