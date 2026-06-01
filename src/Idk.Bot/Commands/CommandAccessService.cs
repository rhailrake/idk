using Discord;
using Discord.WebSocket;
using Idk.Bot.Configuration;

namespace Idk.Bot.Commands;

public sealed class CommandAccessService(BotOptions options)
{
    public bool CanUse(IUser user)
    {
        if (options.AllowedRoleIds.Count == 0)
            return false;

        if (user is not SocketGuildUser guildUser)
            return false;

        return guildUser.Roles.Any(role => options.AllowedRoleIds.Contains(role.Id));
    }
}
