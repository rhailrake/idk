using ScottPlot;
using SkiaSharp;

namespace Idk.Bot.Diagnostics;

internal enum ReportHealth
{
    Neutral,
    Ok,
    Warning,
    Critical,
}

internal static class ReportColors
{
    public static readonly SKColor Background = new(13, 17, 23);
    public static readonly SKColor Panel = new(24, 30, 39);
    public static readonly SKColor PanelStroke = new(48, 59, 75);
    public static readonly SKColor Text = new(239, 242, 247);
    public static readonly SKColor Muted = new(150, 162, 179);
    public static readonly SKColor Faint = new(104, 118, 138);

    public static readonly Color PlotBackground = Color.FromHex("#181E27");
    public static readonly Color PlotGrid = Color.FromHex("#303B4B");
    public static readonly Color PlotText = Color.FromHex("#EFF2F7");
    public static readonly Color PlotMuted = Color.FromHex("#96A2B3");

    public static readonly Color Info = Color.FromHex("#5C8FF7");
    public static readonly Color Ok = Color.FromHex("#48C68C");
    public static readonly Color Warning = Color.FromHex("#F2AA50");
    public static readonly Color Critical = Color.FromHex("#F05D5E");
    public static readonly Color Violet = Color.FromHex("#AE7DFF");
    public static readonly Color Cyan = Color.FromHex("#53BEC4");

    public static ReportHealth HigherIsWorse(double? value, double warning, double critical)
    {
        if (value == null)
            return ReportHealth.Neutral;

        if (value.Value >= critical)
            return ReportHealth.Critical;

        return value.Value >= warning ? ReportHealth.Warning : ReportHealth.Ok;
    }

    public static ReportHealth LowerIsWorse(double? value, double warning, double critical)
    {
        if (value == null)
            return ReportHealth.Neutral;

        if (value.Value <= critical)
            return ReportHealth.Critical;

        return value.Value <= warning ? ReportHealth.Warning : ReportHealth.Ok;
    }

    public static Color Plot(ReportHealth health)
    {
        return health switch
        {
            ReportHealth.Ok => Ok,
            ReportHealth.Warning => Warning,
            ReportHealth.Critical => Critical,
            _ => Info,
        };
    }

    public static Color PlotHigher(double? value, double warning, double critical)
    {
        return Plot(HigherIsWorse(value, warning, critical));
    }

    public static Color PlotLower(double? value, double warning, double critical)
    {
        return Plot(LowerIsWorse(value, warning, critical));
    }

    public static SKColor Sk(ReportHealth health)
    {
        return ToSkColor(Plot(health));
    }

    public static SKColor SkHigher(double? value, double warning, double critical)
    {
        return Sk(HigherIsWorse(value, warning, critical));
    }

    public static SKColor SkLower(double? value, double warning, double critical)
    {
        return Sk(LowerIsWorse(value, warning, critical));
    }

    public static SKColor ToSkColor(Color color)
    {
        return new SKColor(color.Red, color.Green, color.Blue, color.Alpha);
    }
}
