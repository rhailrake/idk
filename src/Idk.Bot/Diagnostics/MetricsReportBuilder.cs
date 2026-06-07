using System.Globalization;

namespace Idk.Bot.Diagnostics;

public sealed class MetricsReportBuilder
{
    public MetricsReport Build(ServerDefinition server, TimeSpan range, IReadOnlyList<MetricsSnapshot> snapshots)
    {
        if (snapshots.Count < 2)
            return CreateFailure(server, range, "Need at least 2 metric samples. Wait for the sampler to collect history.");

        var first = snapshots[0];
        var last = snapshots[^1];
        var covered = last.CapturedAt - first.CapturedAt;
        if (covered <= TimeSpan.Zero)
            return CreateFailure(server, range, "Metric history has no elapsed time.");

        var serverAreas = BuildTimedAreas(first, last, covered, "robust_server_update_usage", "area", 10);

        return new MetricsReport(
            server,
            true,
            null,
            range,
            covered,
            snapshots.Count,
            first.CapturedAt,
            last.CapturedAt,
            BuildServerSummary(first, last, covered, serverAreas),
            BuildGauges(last),
            BuildPhysics(first, last),
            BuildNetwork(first, last, covered),
            serverAreas,
            BuildTimedAreas(first, last, covered, "robust_entity_systems_update_usage", "system", 12),
            BuildTimedAreas(first, last, covered, "robust_game_state_update_usage", "area", 8),
            BuildTimedAreas(first, last, covered, "robust_entity_physics_phase_usage", "phase", 8),
            BuildPhysicsControllers(first, last, covered));
    }

    private static MetricsReport CreateFailure(ServerDefinition server, TimeSpan range, string error)
    {
        return new MetricsReport(
            server,
            false,
            error,
            range,
            TimeSpan.Zero,
            0,
            DateTimeOffset.MinValue,
            DateTimeOffset.MinValue,
            new MetricsServerSummary(null, null, null, null, null),
            new MetricsGaugeSummary(null, null, null, null, null),
            new MetricsPhysicsSummary(null, null, null, null, null, null, null, null, null, null),
            new MetricsNetworkSummary(null, null, null, null, null, null, null, null, null),
            [],
            [],
            [],
            [],
            []);
    }

    private static MetricsServerSummary BuildServerSummary(
        MetricsSnapshot first,
        MetricsSnapshot last,
        TimeSpan covered,
        IReadOnlyList<MetricsTimedArea> serverAreas)
    {
        var tickRate = CounterRate(first, last, covered, "robust_server_curtick");
        if (tickRate is <= 0)
            tickRate = null;

        var mainLoopMillisecondsPerSecond = serverAreas.Sum(area => area.MillisecondsPerSecond);
        double? mainLoopAverageMilliseconds = tickRate == null
            ? null
            : mainLoopMillisecondsPerSecond / tickRate.Value;

        return new MetricsServerSummary(
            tickRate,
            mainLoopMillisecondsPerSecond,
            mainLoopAverageMilliseconds,
            MaxOrNull(serverAreas.Select(area => area.P95Milliseconds)),
            MaxOrNull(serverAreas.Select(area => area.P99Milliseconds)));
    }

    private static MetricsGaugeSummary BuildGauges(MetricsSnapshot latest)
    {
        return new MetricsGaugeSummary(
            latest.GetValue("robust_player_count"),
            latest.GetValue("robust_entities_count"),
            latest.GetValue("physics_active_mover_count"),
            latest.GetValue("npc_active_count"),
            latest.GetValue("npc_steering_active_count"));
    }

    private static MetricsPhysicsSummary BuildPhysics(MetricsSnapshot first, MetricsSnapshot latest)
    {
        return new MetricsPhysicsSummary(
            latest.GetValue("robust_physics_awake_bodies"),
            latest.GetValue("robust_physics_active_contacts"),
            latest.GetValue("robust_physics_moved_grids"),
            latest.GetValue("robust_physics_move_buffer"),
            latest.GetValue("robust_physics_new_contact_pairs"),
            latest.GetValue("physics_sanity_candidates"),
            latest.GetValue("physics_sanity_tracked_bodies"),
            CounterDelta(first, latest, "physics_sanity_resolved_count"),
            CounterDelta(first, latest, "physics_sanity_failed_resolve_count"),
            CounterDelta(first, latest, "physics_sanity_resolve_limit_reached_count"));
    }

    private static MetricsNetworkSummary BuildNetwork(MetricsSnapshot first, MetricsSnapshot last, TimeSpan covered)
    {
        return new MetricsNetworkSummary(
            CounterRate(first, last, covered, "robust_net_sent_bytes"),
            CounterRate(first, last, covered, "robust_net_recv_bytes"),
            CounterRate(first, last, covered, "robust_net_sent_packets"),
            CounterRate(first, last, covered, "robust_net_recv_packets"),
            CounterRate(first, last, covered, "robust_net_sent_messages"),
            CounterRate(first, last, covered, "robust_net_recv_messages"),
            CounterRate(first, last, covered, "robust_net_resent_delay"),
            CounterRate(first, last, covered, "robust_net_resent_hole"),
            CounterRate(first, last, covered, "robust_net_dropped"));
    }

    private static IReadOnlyList<MetricsTimedArea> BuildTimedAreas(
        MetricsSnapshot first,
        MetricsSnapshot last,
        TimeSpan covered,
        string histogramName,
        string label,
        int limit)
    {
        var sumName = histogramName + "_sum";
        var countName = histogramName + "_count";
        var areas = new List<MetricsTimedArea>();

        foreach (var lastSum in last.Samples.Values.Where(sample => sample.Name == sumName))
        {
            if (!lastSum.Labels.TryGetValue(label, out var area))
                continue;

            var firstSum = FindMatching(first, lastSum);
            var lastCount = FindByLabels(last, countName, lastSum.Labels);
            var firstCount = lastCount == null ? null : FindMatching(first, lastCount);
            if (firstSum == null || lastCount == null || firstCount == null)
                continue;

            var deltaSum = CounterDelta(firstSum.Value, lastSum.Value);
            var deltaCount = CounterDelta(firstCount.Value, lastCount.Value);
            if (deltaSum == null || deltaCount is null or <= 0)
                continue;

            areas.Add(new MetricsTimedArea(
                area,
                deltaSum.Value / covered.TotalSeconds * 1000,
                deltaSum.Value / deltaCount.Value * 1000,
                deltaCount.Value / covered.TotalSeconds,
                BuildHistogramQuantile(first, last, histogramName, lastSum.Labels, 0.95),
                BuildHistogramQuantile(first, last, histogramName, lastSum.Labels, 0.99)));
        }

        return areas
            .Where(area => area.MillisecondsPerSecond > 0)
            .OrderByDescending(area => area.MillisecondsPerSecond)
            .Take(limit)
            .ToArray();
    }

    private static IReadOnlyList<MetricsTimedArea> BuildPhysicsControllers(
        MetricsSnapshot first,
        MetricsSnapshot last,
        TimeSpan covered)
    {
        var controllers = new Dictionary<string, (double MsPerSecond, double SumSeconds, double Count)>(StringComparer.Ordinal);

        AddControllers("robust_entity_physics_controller_before_solve");
        AddControllers("robust_entity_physics_controller_after_solve");

        return controllers
            .Select(controller => new MetricsTimedArea(
                controller.Key,
                controller.Value.MsPerSecond,
                controller.Value.Count <= 0 ? 0 : controller.Value.SumSeconds / controller.Value.Count * 1000,
                controller.Value.Count / covered.TotalSeconds))
            .Where(controller => controller.MillisecondsPerSecond > 0)
            .OrderByDescending(controller => controller.MillisecondsPerSecond)
            .Take(8)
            .ToArray();

        void AddControllers(string histogramName)
        {
            foreach (var area in BuildTimedAreas(first, last, covered, histogramName, "controller", 64))
            {
                controllers.TryGetValue(area.Name, out var existing);
                var sumSeconds = area.MillisecondsPerSecond / 1000 * covered.TotalSeconds;
                var count = area.CallsPerSecond * covered.TotalSeconds;
                controllers[area.Name] = (
                    existing.MsPerSecond + area.MillisecondsPerSecond,
                    existing.SumSeconds + sumSeconds,
                    existing.Count + count);
            }
        }
    }

    private static double? BuildHistogramQuantile(
        MetricsSnapshot first,
        MetricsSnapshot last,
        string histogramName,
        IReadOnlyDictionary<string, string> labels,
        double quantile)
    {
        var bucketName = histogramName + "_bucket";
        var buckets = new List<(double Bound, double Count)>();

        foreach (var lastBucket in last.Samples.Values.Where(sample => sample.Name == bucketName))
        {
            if (!LabelsMatchExceptLe(lastBucket.Labels, labels) ||
                !lastBucket.Labels.TryGetValue("le", out var le) ||
                !TryParseBucketBound(le, out var bound))
            {
                continue;
            }

            var firstBucket = FindMatching(first, lastBucket);
            if (firstBucket == null)
                continue;

            var delta = CounterDelta(firstBucket.Value, lastBucket.Value);
            if (delta == null)
                continue;

            buckets.Add((bound, delta.Value));
        }

        if (buckets.Count == 0)
            return null;

        buckets.Sort((left, right) => left.Bound.CompareTo(right.Bound));
        var total = buckets.LastOrDefault(bucket => double.IsPositiveInfinity(bucket.Bound)).Count;
        if (total <= 0)
            total = buckets[^1].Count;

        if (total <= 0)
            return null;

        var rank = total * quantile;
        var previousBound = 0d;
        var previousCount = 0d;

        foreach (var bucket in buckets)
        {
            var count = Math.Max(bucket.Count, previousCount);
            if (count < rank)
            {
                previousBound = bucket.Bound;
                previousCount = count;
                continue;
            }

            if (double.IsPositiveInfinity(bucket.Bound))
                return double.IsPositiveInfinity(previousBound) ? null : previousBound * 1000;

            if (count <= previousCount)
                return bucket.Bound * 1000;

            var position = (rank - previousCount) / (count - previousCount);
            return (previousBound + (bucket.Bound - previousBound) * position) * 1000;
        }

        var lastFinite = buckets.LastOrDefault(bucket => !double.IsPositiveInfinity(bucket.Bound)).Bound;
        return lastFinite > 0 ? lastFinite * 1000 : null;
    }

    private static PrometheusMetricSample? FindMatching(MetricsSnapshot snapshot, PrometheusMetricSample sample)
    {
        return snapshot.Samples.TryGetValue(sample.Identity, out var found) ? found : null;
    }

    private static PrometheusMetricSample? FindByLabels(
        MetricsSnapshot snapshot,
        string name,
        IReadOnlyDictionary<string, string> labels)
    {
        var labelsKey = string.Join(",", labels
            .OrderBy(label => label.Key, StringComparer.Ordinal)
            .Select(label => $"{label.Key}={label.Value}"));

        var identity = new PrometheusMetricIdentity(name, labelsKey);
        return snapshot.Samples.TryGetValue(identity, out var sample) ? sample : null;
    }

    private static bool LabelsMatchExceptLe(
        IReadOnlyDictionary<string, string> candidate,
        IReadOnlyDictionary<string, string> expected)
    {
        if (candidate.Count != expected.Count + 1)
            return false;

        foreach (var label in expected)
        {
            if (!candidate.TryGetValue(label.Key, out var value) ||
                !StringComparer.Ordinal.Equals(value, label.Value))
            {
                return false;
            }
        }

        return candidate.ContainsKey("le");
    }

    private static bool TryParseBucketBound(string value, out double bound)
    {
        if (value == "+Inf")
        {
            bound = double.PositiveInfinity;
            return true;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out bound) &&
               bound >= 0;
    }

    private static double? CounterRate(MetricsSnapshot first, MetricsSnapshot last, TimeSpan covered, string name)
    {
        var delta = CounterDelta(first, last, name);
        return delta == null ? null : delta.Value / covered.TotalSeconds;
    }

    private static double? CounterDelta(MetricsSnapshot first, MetricsSnapshot last, string name)
    {
        var firstValue = first.GetValue(name);
        var lastValue = last.GetValue(name);
        if (firstValue == null || lastValue == null)
            return null;

        return CounterDelta(firstValue.Value, lastValue.Value);
    }

    private static double? CounterDelta(double first, double last)
    {
        if (last < first)
            return null;

        return last - first;
    }

    private static double? MaxOrNull(IEnumerable<double?> values)
    {
        var max = values
            .Where(value => value != null)
            .Select(value => value!.Value)
            .DefaultIfEmpty(double.NaN)
            .Max();

        return double.IsNaN(max) ? null : max;
    }
}
