using Discord.WebSocket;
using Idk.Bot.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Commands;

public sealed class MetricsCommandHandler(
    IServerRegistry serverRegistry,
    IMetricsService metricsService,
    MetricsReportBuilder reportBuilder,
    MetricsReportRenderer reportRenderer,
    ILogger<MetricsCommandHandler> logger)
{
    private static readonly TimeSpan DefaultRange = TimeSpan.FromMinutes(15);

    public async Task HandleAsync(SocketSlashCommand command)
    {
        var subCommand = command.Data.Options.FirstOrDefault();
        if (subCommand == null)
        {
            await command.RespondAsync("Unknown metrics command.", ephemeral: true);
            return;
        }

        switch (subCommand.Name)
        {
            case "summary":
                await HandleSummaryAsync(command, subCommand);
                return;
            default:
                await command.RespondAsync("Unknown metrics command.", ephemeral: true);
                return;
        }
    }

    private async Task HandleSummaryAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var server = await ReadServerAsync(command, subCommand);
        if (server == null)
            return;

        var minutes = GetIntOption(subCommand, "minutes") ?? (int)DefaultRange.TotalMinutes;
        if (minutes is < 1 or > 120)
        {
            await command.RespondAsync("Metrics range must be between 1 and 120 minutes.");
            return;
        }

        await command.DeferAsync();

        try
        {
            var range = TimeSpan.FromMinutes(minutes);
            var snapshots = metricsService.GetSnapshots(server, range);
            var report = reportBuilder.Build(server, range, snapshots);
            await command.ModifyOriginalResponseAsync(message => message.Content = FormatReport(report));

            if (!report.Success)
                return;

            var png = reportRenderer.RenderPng(report);
            await using var stream = new MemoryStream(png);
            await command.FollowupWithFileAsync(stream, "metrics-summary.png");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to build metrics report for {Server}.", server.Id);
            await command.ModifyOriginalResponseAsync(message => message.Content = $"Failed to build metrics report for `{server.Id}`.");
        }
    }

    private async Task<ServerDefinition?> ReadServerAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var serverId = GetStringOption(subCommand, "server");
        if (serverId != null && serverRegistry.TryGetServer(serverId, out var server))
            return server;

        await command.RespondAsync("Unknown server.", ephemeral: true);
        return null;
    }

    private static string FormatReport(MetricsReport report)
    {
        if (!report.Success)
        {
            return $"""
                `{report.Server.Id}` metrics failed
                range: `{FormatDuration(report.Range)}`
                error: `{report.Error}`
                """;
        }

        var topSystems = report.EntitySystems.Take(4).ToArray();
        var systems = topSystems.Length == 0
            ? "n/a"
            : string.Join(", ", topSystems.Select(system => $"{system.Name} `{FormatMillisecondsPerSecond(system.MillisecondsPerSecond)}`"));

        return $"""
            `{report.Server.Id}` metrics
            range: `{FormatDuration(report.Covered)}`, samples: `{report.SampleCount}`

            players: `{FormatCount(report.Gauges.Players)}`, entities: `{FormatCount(report.Gauges.Entities)}`
            server: main `{FormatMilliseconds(report.ServerSummary.MainLoopAverageMilliseconds)}/tick`, load `{FormatMillisecondsPerSecond(report.ServerSummary.MainLoopMillisecondsPerSecond)}`, tickrate `{FormatRate(report.ServerSummary.TickRate)}`
            spikes: area p95 `{FormatMilliseconds(report.ServerSummary.WorstAreaP95Milliseconds)}`, area p99 `{FormatMilliseconds(report.ServerSummary.WorstAreaP99Milliseconds)}`
            net: out `{FormatBytesPerSecond(report.Network.SentBytesPerSecond)}`, in `{FormatBytesPerSecond(report.Network.ReceivedBytesPerSecond)}`, dropped `{FormatRate(report.Network.DroppedPerSecond)}`
            systems: {systems}
            """;
    }

    private static string? GetStringOption(SocketSlashCommandDataOption subCommand, string name)
    {
        return subCommand.Options.FirstOrDefault(option => option.Name == name)?.Value as string;
    }

    private static int? GetIntOption(SocketSlashCommandDataOption subCommand, string name)
    {
        var value = subCommand.Options.FirstOrDefault(option => option.Name == name)?.Value;
        return value switch
        {
            int integer => integer,
            long integer => (int)integer,
            _ => null,
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalMinutes >= 1
            ? $"{duration.TotalMinutes:0.#}m"
            : $"{duration.TotalSeconds:0.#}s";
    }

    private static string FormatMillisecondsPerSecond(double? value)
    {
        return value == null ? "n/a" : $"{value.Value:0.##}ms/s";
    }

    private static string FormatMilliseconds(double? value)
    {
        return value == null ? "n/a" : $"{value.Value:0.##}ms";
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
}
