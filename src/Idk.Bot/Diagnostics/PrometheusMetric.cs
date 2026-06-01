namespace Idk.Bot.Diagnostics;

public sealed record PrometheusMetricIdentity(string Name, string LabelsKey);

public sealed record PrometheusMetricSample(
    PrometheusMetricIdentity Identity,
    string Name,
    IReadOnlyDictionary<string, string> Labels,
    double Value);

public sealed record MetricsSnapshot(
    ServerDefinition Server,
    DateTimeOffset CapturedAt,
    IReadOnlyDictionary<PrometheusMetricIdentity, PrometheusMetricSample> Samples)
{
    public double? GetValue(string name)
    {
        var identity = new PrometheusMetricIdentity(name, string.Empty);
        return Samples.TryGetValue(identity, out var sample) ? sample.Value : null;
    }
}
