namespace Idk.Bot.SelfUpdate;

public sealed record BotVersion(
    string? Commit,
    string? Branch);
