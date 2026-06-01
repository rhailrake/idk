using Discord;
using Discord.WebSocket;
using Idk.Bot.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Commands;

public sealed class SlashCommandRegistrar(
    DiscordSocketClient client,
    IServerRegistry serverRegistry,
    ILogger<SlashCommandRegistrar> logger)
{
    private bool _registered;

    public async Task RegisterAsync()
    {
        if (_registered)
            return;

        var commands = new[]
        {
            BuildPerfCommand(),
            BuildMetricsCommand(),
            BuildBotCommand(),
        };

        await client.BulkOverwriteGlobalApplicationCommandsAsync(commands);

        _registered = true;
        logger.LogInformation("Registered slash commands.");
    }

    private SlashCommandProperties BuildPerfCommand()
    {
        var serverOption = new SlashCommandOptionBuilder()
            .WithName("server")
            .WithDescription("Server")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true);

        foreach (var server in serverRegistry.Servers.OrderBy(server => server.Id))
        {
            serverOption.AddChoice(server.DisplayName, server.Id);
        }

        var statusSubCommand = new SlashCommandOptionBuilder()
            .WithName("status")
            .WithDescription("Show process status")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(CloneServerOption());

        var traceSubCommand = new SlashCommandOptionBuilder()
            .WithName("trace")
            .WithDescription("Collect CPU trace")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(CloneServerOption())
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("seconds")
                .WithDescription("Trace duration in seconds")
                .WithType(ApplicationCommandOptionType.Integer)
                .WithRequired(false)
                .WithMinValue(1)
                .WithMaxValue(600));

        var latestSubCommand = new SlashCommandOptionBuilder()
            .WithName("latest")
            .WithDescription("Show latest trace archive")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(CloneServerOption());

        var analyzeSubCommand = new SlashCommandOptionBuilder()
            .WithName("analyze")
            .WithDescription("Analyze a trace archive")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(CloneServerOption())
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("trace")
                .WithDescription("latest, archive file name, or archive path")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(false));

        var cleanupSubCommand = new SlashCommandOptionBuilder()
            .WithName("cleanup")
            .WithDescription("Delete old trace files")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("days")
                .WithDescription("Delete traces older than this many days")
                .WithType(ApplicationCommandOptionType.Integer)
                .WithRequired(false)
                .WithMinValue(0)
                .WithMaxValue(365));

        return new SlashCommandBuilder()
            .WithName("perf")
            .WithDescription("Performance diagnostics")
            .AddOption(statusSubCommand)
            .AddOption(traceSubCommand)
            .AddOption(latestSubCommand)
            .AddOption(analyzeSubCommand)
            .AddOption(cleanupSubCommand)
            .Build();

        SlashCommandOptionBuilder CloneServerOption()
        {
            var option = new SlashCommandOptionBuilder()
                .WithName(serverOption.Name)
                .WithDescription(serverOption.Description)
                .WithType(serverOption.Type)
                .WithRequired(serverOption.IsRequired ?? false);

            foreach (var choice in serverOption.Choices)
            {
                option.AddChoice(choice.Name, (string)choice.Value);
            }

            return option;
        }
    }

    private SlashCommandProperties BuildMetricsCommand()
    {
        var serverOption = new SlashCommandOptionBuilder()
            .WithName("server")
            .WithDescription("Server")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true);

        foreach (var server in serverRegistry.Servers.OrderBy(server => server.Id))
        {
            serverOption.AddChoice(server.DisplayName, server.Id);
        }

        var summarySubCommand = new SlashCommandOptionBuilder()
            .WithName("summary")
            .WithDescription("Show useful server metrics")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(serverOption)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("minutes")
                .WithDescription("History range in minutes")
                .WithType(ApplicationCommandOptionType.Integer)
                .WithRequired(false)
                .WithMinValue(1)
                .WithMaxValue(120));

        return new SlashCommandBuilder()
            .WithName("metrics")
            .WithDescription("Server metrics")
            .AddOption(summarySubCommand)
            .Build();
    }

    private static SlashCommandProperties BuildBotCommand()
    {
        var versionSubCommand = new SlashCommandOptionBuilder()
            .WithName("version")
            .WithDescription("Show bot version")
            .WithType(ApplicationCommandOptionType.SubCommand);

        var updateSubCommand = new SlashCommandOptionBuilder()
            .WithName("update")
            .WithDescription("Update and restart bot")
            .WithType(ApplicationCommandOptionType.SubCommand);

        var restartSubCommand = new SlashCommandOptionBuilder()
            .WithName("restart")
            .WithDescription("Restart bot")
            .WithType(ApplicationCommandOptionType.SubCommand);

        return new SlashCommandBuilder()
            .WithName("bot")
            .WithDescription("Bot maintenance")
            .AddOption(versionSubCommand)
            .AddOption(updateSubCommand)
            .AddOption(restartSubCommand)
            .Build();
    }
}
