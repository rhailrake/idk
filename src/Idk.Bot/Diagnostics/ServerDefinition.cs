namespace Idk.Bot.Diagnostics;

public sealed record ServerDefinition(
    string Id,
    string DisplayName,
    string LagtraceTarget,
    Uri MetricsEndpoint,
    string? CrashReportDirectory = null)
{
    public Uri PhysicsDiagnosticsEndpoint => new(MetricsEndpoint, "/physicsdiag/");
}
