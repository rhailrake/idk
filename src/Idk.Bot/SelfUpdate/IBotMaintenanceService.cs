namespace Idk.Bot.SelfUpdate;

public interface IBotMaintenanceService
{
    Task<BotVersion> GetVersionAsync(CancellationToken cancellationToken);

    Task StartUpdateAsync();

    Task StartRestartAsync();
}
