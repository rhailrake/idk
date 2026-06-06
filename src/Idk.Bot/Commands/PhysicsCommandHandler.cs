using System.Text;
using Discord.WebSocket;
using Idk.Bot.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Idk.Bot.Commands;

public sealed class PhysicsCommandHandler(
    IServerRegistry serverRegistry,
    IPhysicsDiagnosticsService diagnosticsService,
    ILogger<PhysicsCommandHandler> logger)
{
    private const int DefaultLimit = 30;
    private const int MaxInlineLength = 1800;

    public async Task HandleAsync(SocketSlashCommand command)
    {
        var subCommand = command.Data.Options.FirstOrDefault();
        if (subCommand == null)
        {
            await command.RespondAsync("Unknown physics command.", ephemeral: true);
            return;
        }

        switch (subCommand.Name)
        {
            case "diag":
                await HandleDiagAsync(command, subCommand);
                return;
            default:
                await command.RespondAsync("Unknown physics command.", ephemeral: true);
                return;
        }
    }

    private async Task HandleDiagAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var server = await ReadServerAsync(command, subCommand);
        if (server == null)
            return;

        var limit = GetIntOption(subCommand, "limit") ?? DefaultLimit;
        if (limit is < 1 or > 200)
        {
            await command.RespondAsync("Limit must be between 1 and 200.");
            return;
        }

        await command.DeferAsync();

        try
        {
            var diagnostics = await diagnosticsService.GetDiagnosticsAsync(server, limit, CancellationToken.None);
            await SendDiagnosticsAsync(command, server, diagnostics);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get physics diagnostics for {Server}.", server.Id);
            await command.ModifyOriginalResponseAsync(message => message.Content = $"Failed to get physics diagnostics for `{server.Id}`.");
        }
    }

    private static async Task SendDiagnosticsAsync(SocketSlashCommand command, ServerDefinition server, string diagnostics)
    {
        if (diagnostics.Length <= MaxInlineLength)
        {
            await command.ModifyOriginalResponseAsync(message => message.Content = $"""
                `{server.Id}` physics diagnostics
                ```text
                {diagnostics.TrimEnd()}
                ```
                """);
            return;
        }

        await command.ModifyOriginalResponseAsync(message => message.Content = $"`{server.Id}` physics diagnostics");

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(diagnostics));
        await command.FollowupWithFileAsync(stream, $"physicsdiag-{server.Id}.txt");
    }

    private async Task<ServerDefinition?> ReadServerAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var serverId = GetStringOption(subCommand, "server");
        if (serverId != null && serverRegistry.TryGetServer(serverId, out var server))
            return server;

        await command.RespondAsync("Unknown server.", ephemeral: true);
        return null;
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
}
