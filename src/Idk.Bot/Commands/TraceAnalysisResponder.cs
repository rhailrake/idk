using Discord.WebSocket;
using Idk.Bot.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Idk.Bot.Commands;

public sealed class TraceAnalysisResponder(
    TraceAnalysisChartRenderer chartRenderer,
    ILogger<TraceAnalysisResponder> logger)
{
    public async Task SendAnalysisAsync(SocketSlashCommand command, TraceAnalysisResult result)
    {
        await command.ModifyOriginalResponseAsync(message => message.Content = FormatAnalysis(result));

        if (!result.Success)
            return;

        try
        {
            var png = chartRenderer.RenderPng(result);
            await using var stream = new MemoryStream(png);
            await command.FollowupWithFileAsync(
                stream,
                "trace-analysis.png");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to render trace analysis graph for {Server}.", result.Archive.Server.Id);
        }
    }

    private static string FormatAnalysis(TraceAnalysisResult result)
    {
        if (!result.Success)
        {
            return $"""
                `{result.Archive.Server.Id}` analysis failed
                archive: `{Path.GetFileName(result.Archive.Path)}`
                error: `{TrimForDiscord(result.Error ?? "unknown error", 900)}`
                """;
        }

        var process = result.Process;
        var hardware = result.Hardware;
        var topCategories = result.Categories.Take(4).ToArray();
        var topThreads = result.Threads.Take(3).ToArray();

        var categories = topCategories.Length == 0
            ? "n/a"
            : string.Join(", ", topCategories.Select(category => $"{category.Category} `{FormatPercent(category.ExclusivePercent)}`"));

        var threads = topThreads.Length == 0
            ? "n/a"
            : string.Join(", ", topThreads.Select(thread => $"{thread.Name} `{FormatPercent(thread.AverageCpuPercent)}`"));

        var hot = result.HotFunctions.FirstOrDefault();
        var hotPath = hot == null
            ? "n/a"
            : $"`{FormatPercent(hot.ExclusivePercent)}` {TrimForDiscord(CompactFunctionName(hot.Name), 90)}";

        return TrimForDiscord($"""
            `{result.Archive.Server.Id}` analysis
            `{Path.GetFileName(result.Archive.Path)}`

            cpu: avg `{FormatNullablePercent(process?.AverageCpuPercent)}`, peak `{FormatNullablePercent(process?.MaxCpuPercent)}`; host idle `{FormatNullablePercent(hardware?.AverageCpuIdlePercent)}`, iowait `{FormatNullablePercent(hardware?.AverageCpuWaitPercent)}`
            categories: {categories}
            threads: {threads}
            top: {hotPath}
            """, 1900);
    }

    private static string FormatPercent(double value)
    {
        return $"{value.ToString("0.##", CultureInfo.InvariantCulture)}%";
    }

    private static string FormatNullablePercent(double? value)
    {
        return value == null ? "n/a" : FormatPercent(value.Value);
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

    private static string TrimForDiscord(string value, int maxLength)
    {
        value = value.Trim();
        if (value.Length <= maxLength)
            return value;

        return value[..maxLength] + "...";
    }
}
