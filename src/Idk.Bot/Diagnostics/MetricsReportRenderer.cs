using ScottPlot;
using SkiaSharp;

namespace Idk.Bot.Diagnostics;

public sealed class MetricsReportRenderer
{
    private const int Width = 1600;
    private const int Height = 1320;
    private const float Margin = 48;
    private const float Gap = 24;

    private static readonly SKColor Background = new(13, 17, 23);
    private static readonly SKColor Panel = new(24, 30, 39);
    private static readonly SKColor PanelStroke = new(48, 59, 75);
    private static readonly SKColor Text = new(239, 242, 247);
    private static readonly SKColor Muted = new(150, 162, 179);
    private static readonly SKColor Faint = new(104, 118, 138);

    private static readonly ScottPlot.Color PlotBackground = ScottPlot.Color.FromHex("#181E27");
    private static readonly ScottPlot.Color PlotGrid = ScottPlot.Color.FromHex("#303B4B");
    private static readonly ScottPlot.Color PlotText = ScottPlot.Color.FromHex("#EFF2F7");
    private static readonly ScottPlot.Color PlotMuted = ScottPlot.Color.FromHex("#96A2B3");
    private static readonly ScottPlot.Color Blue = ScottPlot.Color.FromHex("#5C8FF7");
    private static readonly ScottPlot.Color Orange = ScottPlot.Color.FromHex("#F2AA50");
    private static readonly ScottPlot.Color Purple = ScottPlot.Color.FromHex("#AE7DFF");
    private static readonly ScottPlot.Color Green = ScottPlot.Color.FromHex("#48C68C");

    public byte[] RenderPng(MetricsReport report)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(Background);
        DrawHeader(canvas, report);
        DrawKpis(canvas, report);

        DrawChartPanel(canvas, new SKRect(Margin, 282, 760, 610), "Main loop", RenderBarChart(report.ServerAreas, 642, 238, Orange));
        DrawChartPanel(canvas, new SKRect(784, 282, Width - Margin, 610), "Entity systems", RenderBarChart(report.EntitySystems.Take(8), 710, 238, Blue));
        DrawChartPanel(canvas, new SKRect(Margin, 634, 760, 962), "PVS / game state", RenderBarChart(report.GameStateAreas, 642, 238, Purple));
        DrawChartPanel(canvas, new SKRect(784, 634, Width - Margin, 962), "Physics controllers", RenderBarChart(report.PhysicsControllers, 710, 238, Green));

        DrawNetworkPanel(canvas, new SKRect(Margin, 986, 760, Height - Margin), report);
        DrawCountsPanel(canvas, new SKRect(784, 986, Width - Margin, Height - Margin), report);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 92);
        return data.ToArray();
    }

    private static void DrawHeader(SKCanvas canvas, MetricsReport report)
    {
        DrawText(canvas, $"{report.Server.Id} metrics", Margin, 62, 34, Text, true);
        DrawText(canvas, $"range {FormatDuration(report.Covered)} / samples {report.SampleCount}", Margin, 98, 18, Muted);

        var endpointRect = new SKRect(980, 38, Width - Margin, 108);
        DrawRoundRect(canvas, endpointRect, new SKColor(34, 43, 58), PanelStroke);
        DrawText(canvas, "endpoint", endpointRect.Left + 18, endpointRect.Top + 24, 14, Muted, true);
        DrawText(canvas, report.Server.MetricsEndpoint.ToString(), endpointRect.Left + 18, endpointRect.Top + 54, 21, Text, true);
    }

    private static void DrawKpis(SKCanvas canvas, MetricsReport report)
    {
        var y = 134f;
        var width = (Width - Margin * 2 - Gap * 3) / 4f;

        DrawKpi(canvas, new SKRect(Margin, y, Margin + width, y + 112), "tick avg", FormatMilliseconds(GetAverageTickMilliseconds(report)), SKColors.CornflowerBlue);
        DrawKpi(canvas, new SKRect(Margin + (width + Gap), y, Margin + (width + Gap) + width, y + 112), "players", FormatCount(report.Gauges.Players), new SKColor(72, 198, 140));
        DrawKpi(canvas, new SKRect(Margin + (width + Gap) * 2, y, Margin + (width + Gap) * 2 + width, y + 112), "entities", FormatCount(report.Gauges.Entities), new SKColor(174, 125, 255));
        DrawKpi(canvas, new SKRect(Margin + (width + Gap) * 3, y, Margin + (width + Gap) * 3 + width, y + 112), "net out", FormatBytesPerSecond(report.Network.SentBytesPerSecond), new SKColor(242, 170, 80));
    }

    private static void DrawKpi(SKCanvas canvas, SKRect rect, string label, string value, SKColor accent)
    {
        DrawRoundRect(canvas, rect, Panel, PanelStroke);
        DrawText(canvas, label, rect.Left + 22, rect.Top + 34, 15, Muted, true);
        DrawText(canvas, value, rect.Left + 22, rect.Top + 82, 34, accent, true);
    }

    private static void DrawChartPanel(SKCanvas canvas, SKRect rect, string title, byte[] chartPng)
    {
        DrawRoundRect(canvas, rect, Panel, PanelStroke);
        DrawText(canvas, title, rect.Left + 24, rect.Top + 34, 20, Text, true);

        using var chart = SKBitmap.Decode(chartPng);
        var target = new SKRect(rect.Left + 34, rect.Top + 66, rect.Right - 38, rect.Bottom - 34);
        canvas.DrawBitmap(chart, target);
    }

    private static void DrawNetworkPanel(SKCanvas canvas, SKRect rect, MetricsReport report)
    {
        DrawRoundRect(canvas, rect, Panel, PanelStroke);
        DrawText(canvas, "Network", rect.Left + 24, rect.Top + 34, 20, Text, true);

        var left = rect.Left + 28;
        var right = rect.Left + rect.Width / 2f + 18;
        var y = rect.Top + 78;
        DrawMetricRow(canvas, left, y, "sent bytes", FormatBytesPerSecond(report.Network.SentBytesPerSecond), Blue);
        DrawMetricRow(canvas, right, y, "recv bytes", FormatBytesPerSecond(report.Network.ReceivedBytesPerSecond), Green);
        y += 58;
        DrawMetricRow(canvas, left, y, "sent packets", FormatRate(report.Network.SentPacketsPerSecond), Blue);
        DrawMetricRow(canvas, right, y, "recv packets", FormatRate(report.Network.ReceivedPacketsPerSecond), Green);
        y += 58;
        DrawMetricRow(canvas, left, y, "resent delay", FormatRate(report.Network.ResentDelayPerSecond), Purple);
        DrawMetricRow(canvas, right, y, "dropped", FormatRate(report.Network.DroppedPerSecond), Orange);
    }

    private static void DrawCountsPanel(SKCanvas canvas, SKRect rect, MetricsReport report)
    {
        DrawRoundRect(canvas, rect, Panel, PanelStroke);
        DrawText(canvas, "Counts", rect.Left + 24, rect.Top + 34, 20, Text, true);

        var gameState = report.ServerAreas.FirstOrDefault(area => area.Name == "GameState");
        var entitySystems = report.ServerAreas.FirstOrDefault(area => area.Name == "EntitySystems");

        var left = rect.Left + 28;
        var right = rect.Left + rect.Width / 2f + 18;
        var y = rect.Top + 78;
        DrawMetricRow(canvas, left, y, "game state", FormatMillisecondsPerSecond(gameState?.MillisecondsPerSecond), Purple);
        DrawMetricRow(canvas, right, y, "entity systems", FormatMillisecondsPerSecond(entitySystems?.MillisecondsPerSecond), Blue);
        y += 58;
        DrawMetricRow(canvas, left, y, "active movers", FormatCount(report.Gauges.ActiveMovers), Orange);
        DrawMetricRow(canvas, right, y, "active NPC", FormatCount(report.Gauges.ActiveNpcs), Green);
        y += 58;
        DrawMetricRow(canvas, left, y, "NPC steering", FormatCount(report.Gauges.ActiveNpcSteering), Green);
        DrawMetricRow(canvas, right, y, "covered", FormatDuration(report.Covered), PlotMuted);
    }

    private static void DrawMetricRow(SKCanvas canvas, float x, float y, string label, string value, ScottPlot.Color accent)
    {
        DrawText(canvas, label, x, y, 14, Muted, true);
        DrawText(canvas, value, x, y + 32, 24, ToSkColor(accent), true);
    }

    private static byte[] RenderBarChart(IEnumerable<MetricsTimedArea> source, int width, int height, ScottPlot.Color fallbackColor)
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
            var color = ColorForArea(area.Name, fallbackColor);
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
        barPlot.ValueLabelStyle.ForeColor = PlotText;
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
        plot.FigureBackground.Color = PlotBackground;
        plot.DataBackground.Color = PlotBackground;
        plot.Axes.Color(PlotMuted);
        plot.Axes.FrameColor(PlotBackground);
        plot.Grid.MajorLineColor = PlotGrid;
        plot.Grid.MinorLineColor = PlotBackground;
        plot.Font.Set("DejaVu Sans", FontWeight.Normal, FontSlant.Upright, FontSpacing.Normal);
        return plot;
    }

    private static ScottPlot.Color ColorForArea(string area, ScottPlot.Color fallback)
    {
        return area switch
        {
            "GameState" or "Serialize States" or "Send States" => Purple,
            "EntitySystems" or "EntityNet" => Blue,
            "PhysicsSystem" or "MoverController" or "SharedPhysicsSystem" => Orange,
            "PvsSystem" or "Get Chunks" or "Update Chunks & Overrides" => Purple,
            "NPCSystem" or "NPCSteeringSystem" => Green,
            _ => fallback,
        };
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

    private static double? GetAverageTickMilliseconds(MetricsReport report)
    {
        if (report.ServerAreas.Count == 0)
            return null;

        var callsPerSecond = report.ServerAreas.Max(area => area.CallsPerSecond);
        if (callsPerSecond <= 0)
            return null;

        return report.ServerAreas.Sum(area => area.MillisecondsPerSecond) / callsPerSecond;
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

    private static SKColor ToSkColor(ScottPlot.Color color)
    {
        return new SKColor(color.Red, color.Green, color.Blue, color.Alpha);
    }
}
