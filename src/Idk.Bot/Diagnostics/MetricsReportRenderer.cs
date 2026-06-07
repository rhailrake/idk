using ScottPlot;
using SkiaSharp;

namespace Idk.Bot.Diagnostics;

public sealed class MetricsReportRenderer
{
    private const int Width = 1600;
    private const int Height = 1560;
    private const float Margin = 48;
    private const float Gap = 24;

    public byte[] RenderPng(MetricsReport report)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(ReportColors.Background);
        DrawHeader(canvas, report);
        DrawKpis(canvas, report);

        DrawChartPanel(canvas, new SKRect(Margin, 282, 760, 610), "Main loop", RenderBarChart(report.ServerAreas, 642, 238, TimedAreaScope.Server));
        DrawChartPanel(canvas, new SKRect(784, 282, Width - Margin, 610), "Entity systems", RenderBarChart(report.EntitySystems.Take(8), 710, 238, TimedAreaScope.EntitySystem));
        DrawChartPanel(canvas, new SKRect(Margin, 634, 760, 962), "Physics phases", RenderBarChart(report.PhysicsPhases, 642, 238, TimedAreaScope.PhysicsPhase));
        DrawChartPanel(canvas, new SKRect(784, 634, Width - Margin, 962), "Physics controllers", RenderBarChart(report.PhysicsControllers, 710, 238, TimedAreaScope.PhysicsController));

        DrawNetworkPanel(canvas, new SKRect(Margin, 986, 760, 1328), report);
        DrawServerPanel(canvas, new SKRect(784, 986, Width - Margin, 1328), report);
        DrawPhysicsSanityPanel(canvas, new SKRect(Margin, 1352, Width - Margin, Height - Margin), report);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 92);
        return data.ToArray();
    }

    private static void DrawHeader(SKCanvas canvas, MetricsReport report)
    {
        DrawText(canvas, $"{report.Server.Id} metrics", Margin, 62, 34, ReportColors.Text, true);
        DrawText(canvas, $"range {FormatDuration(report.Covered)} / samples {report.SampleCount}", Margin, 98, 18, ReportColors.Muted);

        var endpointRect = new SKRect(980, 38, Width - Margin, 108);
        DrawRoundRect(canvas, endpointRect, new SKColor(34, 43, 58), ReportColors.PanelStroke);
        DrawText(canvas, "endpoint", endpointRect.Left + 18, endpointRect.Top + 24, 14, ReportColors.Muted, true);
        DrawText(canvas, report.Server.MetricsEndpoint.ToString(), endpointRect.Left + 18, endpointRect.Top + 54, 21, ReportColors.Text, true);
    }

    private static void DrawKpis(SKCanvas canvas, MetricsReport report)
    {
        var y = 134f;
        var width = (Width - Margin * 2 - Gap * 3) / 4f;
        var tickBudget = GetTickBudgetMilliseconds(report);

        DrawKpi(canvas, new SKRect(Margin, y, Margin + width, y + 112), "main / tick", FormatMilliseconds(report.ServerSummary.MainLoopAverageMilliseconds), ReportColors.SkHigher(report.ServerSummary.MainLoopAverageMilliseconds, tickBudget * 0.75, tickBudget));
        DrawKpi(canvas, new SKRect(Margin + (width + Gap), y, Margin + (width + Gap) + width, y + 112), "players", FormatCount(report.Gauges.Players), ReportColors.ToSkColor(ReportColors.Info));
        DrawKpi(canvas, new SKRect(Margin + (width + Gap) * 2, y, Margin + (width + Gap) * 2 + width, y + 112), "entities", FormatCount(report.Gauges.Entities), ReportColors.SkHigher(report.Gauges.Entities, 100_000, 150_000));
        DrawKpi(canvas, new SKRect(Margin + (width + Gap) * 3, y, Margin + (width + Gap) * 3 + width, y + 112), "net out", FormatBytesPerSecond(report.Network.SentBytesPerSecond), ColorForBandwidth(report.Network.SentBytesPerSecond));
    }

    private static void DrawKpi(SKCanvas canvas, SKRect rect, string label, string value, SKColor accent)
    {
        DrawRoundRect(canvas, rect, ReportColors.Panel, ReportColors.PanelStroke);
        DrawText(canvas, label, rect.Left + 22, rect.Top + 34, 15, ReportColors.Muted, true);
        DrawText(canvas, value, rect.Left + 22, rect.Top + 82, 34, accent, true);
    }

    private static void DrawChartPanel(SKCanvas canvas, SKRect rect, string title, byte[] chartPng)
    {
        DrawRoundRect(canvas, rect, ReportColors.Panel, ReportColors.PanelStroke);
        DrawText(canvas, title, rect.Left + 24, rect.Top + 34, 20, ReportColors.Text, true);

        using var chart = SKBitmap.Decode(chartPng);
        var target = new SKRect(rect.Left + 34, rect.Top + 66, rect.Right - 38, rect.Bottom - 34);
        canvas.DrawBitmap(chart, target);
    }

    private static void DrawNetworkPanel(SKCanvas canvas, SKRect rect, MetricsReport report)
    {
        DrawRoundRect(canvas, rect, ReportColors.Panel, ReportColors.PanelStroke);
        DrawText(canvas, "Network", rect.Left + 24, rect.Top + 34, 20, ReportColors.Text, true);

        var left = rect.Left + 28;
        var right = rect.Left + rect.Width / 2f + 18;
        var y = rect.Top + 78;
        DrawMetricRow(canvas, left, y, "sent bytes", FormatBytesPerSecond(report.Network.SentBytesPerSecond), ColorForBandwidth(report.Network.SentBytesPerSecond));
        DrawMetricRow(canvas, right, y, "recv bytes", FormatBytesPerSecond(report.Network.ReceivedBytesPerSecond), ColorForBandwidth(report.Network.ReceivedBytesPerSecond));
        y += 58;
        DrawMetricRow(canvas, left, y, "sent packets", FormatRate(report.Network.SentPacketsPerSecond), ReportColors.SkHigher(report.Network.SentPacketsPerSecond, 2_500, 6_000));
        DrawMetricRow(canvas, right, y, "recv packets", FormatRate(report.Network.ReceivedPacketsPerSecond), ReportColors.SkHigher(report.Network.ReceivedPacketsPerSecond, 2_500, 6_000));
        y += 58;
        DrawMetricRow(canvas, left, y, "resent delay", FormatRate(report.Network.ResentDelayPerSecond), ReportColors.SkHigher(report.Network.ResentDelayPerSecond, 0.1, 1));
        DrawMetricRow(canvas, right, y, "dropped", FormatRate(report.Network.DroppedPerSecond), ReportColors.SkHigher(report.Network.DroppedPerSecond, 0.1, 1));
    }

    private static void DrawServerPanel(SKCanvas canvas, SKRect rect, MetricsReport report)
    {
        DrawRoundRect(canvas, rect, ReportColors.Panel, ReportColors.PanelStroke);
        DrawText(canvas, "Server", rect.Left + 24, rect.Top + 34, 20, ReportColors.Text, true);

        var left = rect.Left + 28;
        var right = rect.Left + rect.Width / 2f + 18;
        var y = rect.Top + 78;
        var tickBudget = GetTickBudgetMilliseconds(report);

        DrawMetricRow(canvas, left, y, "tickrate", FormatRate(report.ServerSummary.TickRate), ReportColors.SkLower(report.ServerSummary.TickRate, 24, 20));
        DrawMetricRow(canvas, right, y, "server load", FormatMillisecondsPerSecond(report.ServerSummary.MainLoopMillisecondsPerSecond), ReportColors.SkHigher(report.ServerSummary.MainLoopMillisecondsPerSecond, 700, 950));
        y += 58;
        DrawMetricRow(canvas, left, y, "area p95", FormatMilliseconds(report.ServerSummary.WorstAreaP95Milliseconds), ReportColors.SkHigher(report.ServerSummary.WorstAreaP95Milliseconds, tickBudget * 0.5, tickBudget));
        DrawMetricRow(canvas, right, y, "area p99", FormatMilliseconds(report.ServerSummary.WorstAreaP99Milliseconds), ReportColors.SkHigher(report.ServerSummary.WorstAreaP99Milliseconds, tickBudget * 0.75, tickBudget * 1.25));
        y += 52;
        DrawMetricRow(canvas, left, y, "active movers", FormatCount(report.Gauges.ActiveMovers), ReportColors.SkHigher(report.Gauges.ActiveMovers, 300, 700));
        DrawMetricRow(canvas, right, y, "active NPC", FormatCount(report.Gauges.ActiveNpcs), ReportColors.SkHigher(report.Gauges.ActiveNpcs, 250, 500));
        y += 52;
        DrawMetricRow(canvas, left, y, "awake bodies", FormatCount(report.Physics.AwakeBodies), ReportColors.SkHigher(report.Physics.AwakeBodies, 1_500, 4_000));
        DrawMetricRow(canvas, right, y, "contacts", FormatCount(report.Physics.ActiveContacts), ReportColors.SkHigher(report.Physics.ActiveContacts, 4_000, 12_000));
        y += 52;
        DrawMetricRow(canvas, left, y, "moved grids", FormatCount(report.Physics.MovedGrids), ReportColors.SkHigher(report.Physics.MovedGrids, 4, 12));
        DrawMetricRow(canvas, right, y, "move buffer", FormatCount(report.Physics.MoveBuffer), ReportColors.SkHigher(report.Physics.MoveBuffer, 4_000, 12_000));
        y += 52;
        DrawMetricRow(canvas, left, y, "new pairs", FormatCount(report.Physics.NewContactPairs), ReportColors.SkHigher(report.Physics.NewContactPairs, 2_000, 8_000));
        DrawMetricRow(canvas, right, y, "NPC steering", FormatCount(report.Gauges.ActiveNpcSteering), ReportColors.SkHigher(report.Gauges.ActiveNpcSteering, 250, 500));
    }

    private static void DrawPhysicsSanityPanel(SKCanvas canvas, SKRect rect, MetricsReport report)
    {
        DrawRoundRect(canvas, rect, ReportColors.Panel, ReportColors.PanelStroke);
        DrawText(canvas, "Physics sanity", rect.Left + 24, rect.Top + 34, 20, ReportColors.Text, true);

        var width = (rect.Width - 56) / 5f;
        var y = rect.Top + 86;
        var x = rect.Left + 28;

        DrawMetricRow(canvas, x, y, "candidates", FormatCount(report.Physics.SanityCandidates), ReportColors.SkHigher(report.Physics.SanityCandidates, 10, 100));
        x += width;
        DrawMetricRow(canvas, x, y, "tracked", FormatCount(report.Physics.SanityTrackedBodies), ReportColors.SkHigher(report.Physics.SanityTrackedBodies, 10, 100));
        x += width;
        DrawMetricRow(canvas, x, y, "resolved", FormatCount(report.Physics.SanityResolved), ReportColors.ToSkColor(ReportColors.Info));
        x += width;
        DrawMetricRow(canvas, x, y, "failed", FormatCount(report.Physics.SanityFailedResolve), ReportColors.SkHigher(report.Physics.SanityFailedResolve, 0, 10));
        x += width;
        DrawMetricRow(canvas, x, y, "limit", FormatCount(report.Physics.SanityLimitReached), ReportColors.SkHigher(report.Physics.SanityLimitReached, 0, 10));
    }

    private static void DrawMetricRow(SKCanvas canvas, float x, float y, string label, string value, SKColor accent)
    {
        DrawText(canvas, label, x, y, 14, ReportColors.Muted, true);
        DrawText(canvas, value, x, y + 32, 24, accent, true);
    }

    private static byte[] RenderBarChart(IEnumerable<MetricsTimedArea> source, int width, int height, TimedAreaScope scope)
    {
        var areas = source
            .Where(area => area.MillisecondsPerSecond > 0)
            .Take(8)
            .Reverse()
            .ToArray();

        if (areas.Length == 0)
        {
            areas =
            [
                new MetricsTimedArea("n/a", 0, 0, 0)
            ];
        }

        var plot = CreateBasePlot();
        var bars = areas.Select((area, index) =>
        {
            var color = ColorForTimedArea(area, scope);
            return new Bar
            {
                Position = index,
                Value = area.MillisecondsPerSecond,
                FillColor = color,
                LineColor = color,
                ValueLabel = FormatMillisecondsPerSecond(area.MillisecondsPerSecond),
                Label = string.Empty,
                Size = 0.58,
            };
        }).ToArray();

        var barPlot = plot.Add.Bars(bars);
        barPlot.Horizontal = true;
        barPlot.LabelsOnTop = true;
        barPlot.ValueLabelStyle.ForeColor = ReportColors.PlotText;
        barPlot.ValueLabelStyle.FontSize = 12;
        barPlot.ValueLabelStyle.Bold = true;

        var labels = areas.Select(area => Shorten(area.Name, 27)).ToArray();
        plot.Axes.Left.SetTicks(Enumerable.Range(0, labels.Length).Select(x => (double)x).ToArray(), labels);
        plot.Layout.Fixed(new PixelPadding(0)
        {
            Left = 226,
            Right = 96,
            Bottom = 60,
            Top = 38,
        });

        plot.Axes.Left.MinimumSize = 226;
        plot.Axes.Left.TickLabelStyle.FontSize = 11;
        plot.Axes.Bottom.TickLabelStyle.FontSize = 11;
        plot.Axes.Bottom.MinimumSize = 52;

        var max = Math.Max(areas.Max(area => area.MillisecondsPerSecond), 1);
        plot.Axes.SetLimits(0, max * 1.58, -1.25, labels.Length + 0.25);
        plot.Grid.MajorLineWidth = 1;
        plot.Grid.MinorLineWidth = 0;

        return plot.GetImageBytes(width, height, ImageFormat.Png);
    }

    private static Plot CreateBasePlot()
    {
        var plot = new Plot();
        plot.SetStyle(new ScottPlot.PlotStyles.Dark());
        plot.FigureBackground.Color = ReportColors.PlotBackground;
        plot.DataBackground.Color = ReportColors.PlotBackground;
        plot.Axes.Color(ReportColors.PlotMuted);
        plot.Axes.FrameColor(ReportColors.PlotBackground);
        plot.Grid.MajorLineColor = ReportColors.PlotGrid;
        plot.Grid.MinorLineColor = ReportColors.PlotBackground;
        plot.Font.Set("DejaVu Sans", FontWeight.Normal, FontSlant.Upright, FontSpacing.Normal);
        return plot;
    }

    private static ScottPlot.Color ColorForTimedArea(MetricsTimedArea area, TimedAreaScope scope)
    {
        if (area.Name == "n/a")
            return ReportColors.Info;

        return scope switch
        {
            TimedAreaScope.Server => ReportColors.PlotHigher(area.MillisecondsPerSecond, 250, 500),
            TimedAreaScope.EntitySystem => ReportColors.PlotHigher(area.MillisecondsPerSecond, 25, 80),
            TimedAreaScope.GameState => ReportColors.PlotHigher(area.MillisecondsPerSecond, 50, 150),
            TimedAreaScope.PhysicsPhase => ReportColors.PlotHigher(area.MillisecondsPerSecond, 25, 80),
            TimedAreaScope.PhysicsController => ReportColors.PlotHigher(area.MillisecondsPerSecond, 25, 80),
            _ => ReportColors.Info,
        };
    }

    private static SKColor ColorForBandwidth(double? bytesPerSecond)
    {
        return ReportColors.SkHigher(bytesPerSecond, 1024 * 1024, 3 * 1024 * 1024);
    }

    private static double GetTickBudgetMilliseconds(MetricsReport report)
    {
        return report.ServerSummary.TickRate is > 0
            ? 1000d / report.ServerSummary.TickRate.Value
            : 40d;
    }

    private static void DrawRoundRect(SKCanvas canvas, SKRect rect, SKColor fill, SKColor stroke)
    {
        using var fillPaint = new SKPaint { Color = fill, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawRoundRect(rect, 14, 14, fillPaint);

        using var strokePaint = new SKPaint { Color = stroke, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawRoundRect(rect, 14, 14, strokePaint);
    }

    private static void DrawText(SKCanvas canvas, string text, float x, float y, float size, SKColor color, bool bold = false)
    {
        using var typeface = SKTypeface.FromFamilyName("DejaVu Sans", bold ? SKFontStyle.Bold : SKFontStyle.Normal)
                             ?? SKTypeface.Default;
        using var font = new SKFont(typeface, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawText(text, x, y, font, paint);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalMinutes >= 1
            ? $"{duration.TotalMinutes:0.#}m"
            : $"{duration.TotalSeconds:0.#}s";
    }

    private static string FormatMilliseconds(double? value)
    {
        return value == null ? "n/a" : $"{value.Value:0.##}ms";
    }

    private static string FormatMillisecondsPerSecond(double? value)
    {
        return value == null ? "n/a" : $"{value.Value:0.##}ms/s";
    }

    private static string FormatCount(double? value)
    {
        return value == null ? "n/a" : $"{value.Value:0}";
    }

    private static string FormatRate(double? value)
    {
        return value == null ? "n/a" : $"{value.Value:0.##}/s";
    }

    private static string FormatBytesPerSecond(double? value)
    {
        if (value == null)
            return "n/a";

        string[] units = ["B/s", "KB/s", "MB/s", "GB/s"];
        var scaled = value.Value;
        var unit = 0;
        while (scaled >= 1024 && unit < units.Length - 1)
        {
            scaled /= 1024;
            unit++;
        }

        return $"{scaled:0.##}{units[unit]}";
    }

    private static string Shorten(string text, int maxLength)
    {
        return text.Length <= maxLength
            ? text
            : text[..(maxLength - 3)] + "...";
    }

    private enum TimedAreaScope
    {
        Server,
        EntitySystem,
        GameState,
        PhysicsPhase,
        PhysicsController,
    }
}
