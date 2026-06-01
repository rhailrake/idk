namespace Idk.Bot.Diagnostics;

public sealed record ServerDefinition(
    string Id,
    string DisplayName,
    string LagtraceTarget,
    Uri MetricsEndpoint);
