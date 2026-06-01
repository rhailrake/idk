namespace Idk.Bot.Configuration;

public sealed class SelfUpdateOptions
{
    public const string UpdateScriptPathEnvironmentVariable = "IDK_UPDATE_SCRIPT_PATH";
    public const string RestartScriptPathEnvironmentVariable = "IDK_RESTART_SCRIPT_PATH";
    public const string RepositoryPathEnvironmentVariable = "IDK_REPOSITORY_PATH";

    public required string UpdateScriptPath { get; init; }

    public required string RestartScriptPath { get; init; }

    public required string RepositoryPath { get; init; }

    public static SelfUpdateOptions FromEnvironment()
    {
        return new SelfUpdateOptions
        {
            UpdateScriptPath = ReadString(UpdateScriptPathEnvironmentVariable, "/opt/idk/update.sh"),
            RestartScriptPath = ReadString(RestartScriptPathEnvironmentVariable, "/opt/idk/restart.sh"),
            RepositoryPath = ReadString(RepositoryPathEnvironmentVariable, "/opt/idk-src"),
        };
    }

    private static string ReadString(string environmentVariable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
