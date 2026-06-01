using ScottPlot;
using SkiaSharp;

namespace Idk.Bot.Diagnostics;

public sealed class TraceAnalysisChartRenderer
{
    private const int Width = 1600;
    private const int Height = 1080;
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

    public byte[] RenderPng(TraceAnalysisResult result)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(Background);
        DrawHeader(canvas, result);
        DrawKpis(canvas, result);

        DrawChartPanel(canvas, new SKRect(Margin, 270, 520, 590), "CPU split", RenderCpuSplitChart(result, 410, 232));
        DrawChartPanel(canvas, new SKRect(544, 270, Width - Margin, 590), "Categories", RenderCategoryChart(result, 946, 232));
        DrawChartPanel(canvas, new SKRect(Margin, 614, 690, Height - Margin), "Threads", RenderThreadChart(result, 580, 330));
        DrawFunctionPanel(canvas, new SKRect(714, 614, Width - Margin, Height - Margin), result);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 92);
        return data.ToArray();
    }

    private static void DrawHeader(SKCanvas canvas, TraceAnalysisResult result)
    {
        DrawText(canvas, $"{result.Archive.Server.Id} trace analysis", Margin, 62, 34, Text, true);
        DrawText(canvas, Path.GetFileName(result.Archive.Path), Margin, 98, 18, Muted);

        var archiveRect = new SKRect(980, 38, Width - Margin, 108);
        DrawRoundRect(canvas, archiveRect, new SKColor(34, 43, 58), PanelStroke);
        DrawText(canvas, "archive", archiveRect.Left + 18, archiveRect.Top + 24, 14, Muted, true);
        DrawText(canvas, Shorten(Path.GetFileName(result.Archive.Path), 48), archiveRect.Left + 18, archiveRect.Top + 54, 21, Text, true);
    }

    private static void DrawKpis(SKCanvas canvas, TraceAnalysisResult result)
    {
        var y = 134f;
        var width = (Width - Margin * 2 - Gap * 3) / 4f;
        DrawKpi(canvas, new SKRect(Margin, y, Margin + width, y + 112), "process avg", FormatPercent(result.Process?.AverageCpuPercent), SKColors.CornflowerBlue);
        DrawKpi(canvas, new SKRect(Margin + (width + Gap), y, Margin + (width + Gap) + width, y + 112), "process peak", FormatPercent(result.Process?.MaxCpuPercent), new SKColor(242, 170, 80));
        DrawKpi(canvas, new SKRect(Margin + (width + Gap) * 2, y, Margin + (width + Gap) * 2 + width, y + 112), "host idle", FormatPercent(result.Hardware?.AverageCpuIdlePercent), new SKColor(72, 198, 140));
        DrawKpi(canvas, new SKRect(Margin + (width + Gap) * 3, y, Margin + (width + Gap) * 3 + width, y + 112), "iowait", FormatPercent(result.Hardware?.AverageCpuWaitPercent), new SKColor(174, 125, 255));
    }

    private static void DrawKpi(SKCanvas canvas, SKRect rect, string label, string value, SKColor accent)
    {
        DrawRoundRect(canvas, rect, Panel, PanelStroke);
        DrawText(canvas, label, rect.Left + 22, rect.Top + 34, 15, Muted, true);
        DrawText(canvas, value, rect.Left + 22, rect.Top + 82, 36, accent, true);
    }

    private static void DrawChartPanel(SKCanvas canvas, SKRect rect, string title, byte[] chartPng)
    {
        DrawRoundRect(canvas, rect, Panel, PanelStroke);
        DrawText(canvas, title, rect.Left + 24, rect.Top + 34, 20, Text, true);

        using var chart = SKBitmap.Decode(chartPng);
        var target = new SKRect(rect.Left + 28, rect.Top + 58, rect.Right - 34, rect.Bottom - 30);
        canvas.DrawBitmap(chart, target);
    }

    private static void DrawFunctionPanel(SKCanvas canvas, SKRect rect, TraceAnalysisResult result)
    {
        DrawRoundRect(canvas, rect, Panel, PanelStroke);
        DrawText(canvas, "Functions", rect.Left + 24, rect.Top + 34, 20, Text, true);

        DrawText(canvas, "#", rect.Left + 28, rect.Top + 72, 13, Faint, true);
        DrawText(canvas, "exc", rect.Left + 72, rect.Top + 72, 13, Faint, true);
        DrawText(canvas, "inc", rect.Left + 148, rect.Top + 72, 13, Faint, true);
        DrawText(canvas, "category", rect.Left + 224, rect.Top + 72, 13, Faint, true);
        DrawText(canvas, "method", rect.Left + 350, rect.Top + 72, 13, Faint, true);

        var y = rect.Top + 112;
        foreach (var function in result.HotFunctions.Take(8))
        {
            var color = ToSkColor(ColorForCategory(function.Category));
            DrawText(canvas, $"{function.Rank}.", rect.Left + 28, y, 16, Muted, true);
            DrawText(canvas, FormatPercent(function.ExclusivePercent), rect.Left + 72, y, 16, color, true);
            DrawText(canvas, FormatPercent(function.InclusivePercent), rect.Left + 148, y, 16, Muted, true);
            DrawCategoryPill(canvas, rect.Left + 224, y - 20, function.Category);
            DrawText(canvas, Shorten(CompactFunctionName(function.Name), 61), rect.Left + 350, y, 16, Text);
            y += 42;
        }
    }

    private static byte[] RenderCpuSplitChart(TraceAnalysisResult result, int width, int height)
    {
        var values = new[]
        {
            Math.Max(result.Process?.AverageUserCpuPercent ?? 0, 0),
            Math.Max(result.Process?.AverageSystemCpuPercent ?? 0, 0),
            Math.Max(result.Process?.AverageWaitPercent ?? 0, 0),
        };

        var slices = new List<PieSlice>
        {
            new() { Value = values[0], FillColor = ScottPlot.Color.FromHex("#5C8FF7"), LegendText = $"user {FormatPercent(values[0])}" },
            new() { Value = values[1], FillColor = ScottPlot.Color.FromHex("#F2AA50"), LegendText = $"system {FormatPercent(values[1])}" },
            new() { Value = values[2], FillColor = ScottPlot.Color.FromHex("#AE7DFF"), LegendText = $"wait {FormatPercent(values[2])}" },
        };

        if (values.Sum() <= 0)
            slices = [new PieSlice { Value = 1, FillColor = PlotMuted, LegendText = "n/a" }];

        var plot = CreateBasePlot();
        var pie = plot.Add.Pie(slices);
        pie.DonutFraction = 0.62;
        pie.SliceLabelDistance = 0;
        pie.LineColor = PlotBackground;
        pie.LineWidth = 2;
        plot.ShowLegend(Alignment.MiddleRight);
        plot.Legend.FontColor = PlotText;
        plot.Legend.BackgroundColor = PlotBackground;
        plot.Legend.OutlineColor = PlotBackground;
        plot.HideAxesAndGrid();

        return plot.GetImageBytes(width, height, ImageFormat.Png);
    }

    private static byte[] RenderCategoryChart(TraceAnalysisResult result, int width, int height)
    {
        var samples = result.Categories.Take(6).ToArray();
        if (samples.Length == 0)
            samples = [new TraceCategorySample("n/a", 0)];

        return RenderHorizontalBars(
            samples.Select(x => x.Category).ToArray(),
            samples.Select(x => x.ExclusivePercent).ToArray(),
            samples.Select(x => ColorForCategory(x.Category)).ToArray(),
            width,
            height,
            178);
    }

    private static byte[] RenderThreadChart(TraceAnalysisResult result, int width, int height)
    {
        var samples = result.Threads.Take(8).ToArray();
        if (samples.Length == 0)
            samples = [new TraceThreadSample("n/a", 0, 0, 0)];

        return RenderHorizontalBars(
            samples.Select(x => Shorten(x.Name, 28)).ToArray(),
            samples.Select(x => x.AverageCpuPercent).ToArray(),
            samples.Select(_ => ScottPlot.Color.FromHex("#5C8FF7")).ToArray(),
            width,
            height,
            196);
    }

    private static byte[] RenderHorizontalBars(string[] labels, double[] values, ScottPlot.Color[] colors, int width, int height, float leftPadding)
    {
        var plot = CreateBasePlot();
        labels = labels.Reverse().ToArray();
        values = values.Reverse().ToArray();
        colors = colors.Reverse().ToArray();

        var bars = labels.Select((label, index) => new Bar
        {
            Position = index,
            Value = values[index],
            FillColor = colors[index],
            ValueLabel = FormatPercent(values[index]),
            Label = string.Empty,
            Size = 0.58,
            LineColor = colors[index],
        }).ToArray();

        var barPlot = plot.Add.Bars(bars);
        barPlot.Horizontal = true;
        barPlot.LabelsOnTop = true;
        barPlot.ValueLabelStyle.ForeColor = PlotText;
        barPlot.ValueLabelStyle.FontSize = 13;
        barPlot.ValueLabelStyle.Bold = true;

        plot.Axes.Left.SetTicks(Enumerable.Range(0, labels.Length).Select(x => (double)x).ToArray(), labels);
        plot.Layout.Fixed(new PixelPadding(0)
        {
            Left = leftPadding,
            Right = 88,
            Bottom = 58,
            Top = 36,
        });

        plot.Axes.Left.MinimumSize = leftPadding;
        plot.Axes.Left.TickLabelStyle.FontSize = 11;
        plot.Axes.Bottom.TickLabelStyle.FontSize = 12;
        plot.Axes.Bottom.MinimumSize = 46;
        var max = Math.Max(values.DefaultIfEmpty(0).Max(), 1);
        plot.Axes.SetLimits(0, max * 1.42, -1.15, labels.Length + 0.15);
        plot.Axes.Margins(0, 0, 0, 0);
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

    private static void DrawCategoryPill(SKCanvas canvas, float x, float y, string category)
    {
        var rect = new SKRect(x, y, x + 100, y + 24);
        var color = ToSkColor(ColorForCategory(category));
        DrawRoundRect(canvas, rect, color.WithAlpha(34), color.WithAlpha(86));
        DrawText(canvas, Shorten(category, 13), x + 10, y + 17, 12, color, true);
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

    private static ScottPlot.Color ColorForCategory(string category)
    {
        return category switch
        {
            "Network" => ScottPlot.Color.FromHex("#5C8FF7"),
            "PVS" => ScottPlot.Color.FromHex("#AE7DFF"),
            "Serialization" => ScottPlot.Color.FromHex("#48C68C"),
            "Physics/Movement" => ScottPlot.Color.FromHex("#F2AA50"),
            "Atmos" => ScottPlot.Color.FromHex("#53BEC4"),
            "Power" => ScottPlot.Color.FromHex("#EFD55B"),
            "NPC" => ScottPlot.Color.FromHex("#CB78AA"),
            _ => PlotMuted,
        };
    }

    private static SKColor ToSkColor(ScottPlot.Color color)
    {
        return new SKColor(color.Red, color.Green, color.Blue, color.Alpha);
    }

    private static string FormatPercent(double? value)
    {
        return value == null ? "n/a" : FormatPercent(value.Value);
    }

    private static string FormatPercent(double value)
    {
        return $"{value:0.##}%";
    }

    private static string CompactFunctionName(string name)
    {
        var paren = name.IndexOf('(');
        var prefix = paren >= 0 ? name[..paren] : name;

        var separator = prefix.LastIndexOf('!');
        if (separator < 0)
            separator = prefix.LastIndexOf('.');

        if (separator >= 0 && separator + 1 < prefix.Length)
            prefix = prefix[(separator + 1)..];

        return paren >= 0 ? $"{prefix}()" : prefix;
    }

    private static string Shorten(string text, int maxLength)
    {
        return text.Length <= maxLength
            ? text
            : text[..(maxLength - 3)] + "...";
    }
}
