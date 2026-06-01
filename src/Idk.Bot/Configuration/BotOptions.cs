using Microsoft.Extensions.Logging;

namespace Idk.Bot.Configuration;

public sealed class BotOptions
{
    public const string DiscordTokenEnvironmentVariable = "IDK_DISCORD_TOKEN";
    public const string LogLevelEnvironmentVariable = "IDK_LOG_LEVEL";
    public const string AllowedRoleIdsEnvironmentVariable = "IDK_ALLOWED_ROLE_IDS";

    public required string DiscordToken { get; init; }

    public LogLevel LogLevel { get; init; } = LogLevel.Information;

    public required HashSet<ulong> AllowedRoleIds { get; init; }

    public required DiagnosticsOptions Diagnostics { get; init; }

    public required SelfUpdateOptions SelfUpdate { get; init; }

    public static BotOptions FromEnvironment()
    {
        var token = Environment.GetEnvironmentVariable(DiscordTokenEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException($"{DiscordTokenEnvironmentVariable} is not set.");

        return new BotOptions
        {
            DiscordToken = token,
            LogLevel = ReadLogLevel(),
            AllowedRoleIds = ReadAllowedRoleIds(),
            Diagnostics = DiagnosticsOptions.FromEnvironment(),
            SelfUpdate = SelfUpdateOptions.FromEnvironment(),
        };
    }

    private static LogLevel ReadLogLevel()
    {
        var raw = Environment.GetEnvironmentVariable(LogLevelEnvironmentVariable);
        return Enum.TryParse<LogLevel>(raw, ignoreCase: true, out var level)
            ? level
            : LogLevel.Information;
    }

    private static HashSet<ulong> ReadAllowedRoleIds()
    {
        var raw = Environment.GetEnvironmentVariable(AllowedRoleIdsEnvironmentVariable);
        var result = new HashSet<ulong>();

        if (string.IsNullOrWhiteSpace(raw))
            return result;

        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!ulong.TryParse(part, out var roleId))
                throw new InvalidOperationException($"{AllowedRoleIdsEnvironmentVariable} contains invalid Discord role id: {part}");

            result.Add(roleId);
        }

        return result;
    }
}
