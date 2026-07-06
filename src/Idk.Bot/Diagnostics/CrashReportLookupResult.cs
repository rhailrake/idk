namespace Idk.Bot.Diagnostics;

public sealed record CrashReportLookupResult(
    ServerDefinition Server,
    CrashReport? Report,
    string? Error)
{
    public bool Success => Report != null;
}
