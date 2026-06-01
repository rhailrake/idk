namespace Idk.Bot.Diagnostics;

public sealed record MetricsReport(
    ServerDefinition Server,
    bool Success,
    string? Error,
    TimeSpan Range,
    TimeSpan Covered,
    int SampleCount,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    MetricsGaugeSummary Gauges,
    MetricsNetworkSummary Network,
    IReadOnlyList<MetricsTimedArea> ServerAreas,
    IReadOnlyList<MetricsTimedArea> EntitySystems,
    IReadOnlyList<MetricsTimedArea> GameStateAreas,
    IReadOnlyList<MetricsTimedArea> PhysicsControllers);

public sealed record MetricsGaugeSummary(
    double? Players,
    double? Entities,
    double? ActiveMovers,
    double? ActiveNpcs,
    double? ActiveNpcSteering);

public sealed record MetricsNetworkSummary(
    double? SentBytesPerSecond,
    double? ReceivedBytesPerSecond,
    double? SentPacketsPerSecond,
    double? ReceivedPacketsPerSecond,
    double? SentMessagesPerSecond,
    double? ReceivedMessagesPerSecond,
    double? ResentDelayPerSecond,
    double? ResentHolePerSecond,
    double? DroppedPerSecond);

public sealed record MetricsTimedArea(
    string Name,
    double MillisecondsPerSecond,
    double AverageMilliseconds,
    double CallsPerSecond);
