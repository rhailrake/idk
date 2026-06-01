using Discord;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Discord;

public sealed class DiscordLogForwarder(ILogger<DiscordLogForwarder> logger)
{
    public Task ForwardAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information,
        };

        logger.Log(level, message.Exception, "[Discord:{Source}] {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }
}
