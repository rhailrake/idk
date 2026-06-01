using Idk.Bot.Configuration;
using Idk.Bot.Commands;
using Idk.Bot.Diagnostics;
using Idk.Bot.Discord;
using Idk.Bot.Execution;
using Idk.Bot.SelfUpdate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Hosting;

public static class HostApplicationBuilderExtensions
{
    public static void ConfigureIdkLogging(this HostApplicationBuilder builder, BotOptions options)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(console =>
        {
            console.SingleLine = true;
            console.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        });
        builder.Logging.SetMinimumLevel(options.LogLevel);
    }

    public static IServiceCollection AddIdkBot(this IServiceCollection services, BotOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton(options.Diagnostics);
        services.AddSingleton(options.SelfUpdate);
        services.AddSingleton<IServerRegistry, StaticServerRegistry>();
        services.AddSingleton<IPerfStatusService, PerfStatusService>();
        services.AddSingleton<IPerfTraceService, PerfTraceService>();
        services.AddSingleton<PrometheusTextParser>();
        services.AddSingleton<MetricsHistoryStore>();
        services.AddSingleton<MetricsService>();
        services.AddSingleton<IMetricsService>(provider => provider.GetRequiredService<MetricsService>());
        services.AddHostedService(provider => provider.GetRequiredService<MetricsService>());
        services.AddSingleton<ITraceArchiveStore, TraceArchiveStore>();
        services.AddSingleton<ITraceAnalysisService, TraceAnalysisService>();
        services.AddSingleton<ITraceCleanupService, TraceCleanupService>();
        services.AddSingleton<TraceAnalysisChartRenderer>();
        services.AddSingleton<MetricsReportBuilder>();
        services.AddSingleton<MetricsReportRenderer>();
        services.AddSingleton<IProcessExecutor, ProcessExecutor>();
        services.AddSingleton<IBotMaintenanceService, BotMaintenanceService>();
        services.AddSingleton<CommandAccessService>();
        services.AddSingleton<PerfCommandHandler>();
        services.AddSingleton<MetricsCommandHandler>();
        services.AddSingleton<BotCommandHandler>();
        services.AddSingleton<TraceArchiveResponder>();
        services.AddSingleton<TraceAnalysisResponder>();
        services.AddSingleton<SlashCommandDispatcher>();
        services.AddSingleton<SlashCommandRegistrar>();
        services.AddSingleton(DiscordClientFactory.Create());
        services.AddSingleton<DiscordLogForwarder>();
        services.AddHostedService<DiscordBotHostedService>();

        return services;
    }
}
