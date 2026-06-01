namespace Idk.Bot.Execution;

public sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut,
    TimeSpan Duration)
{
    public bool Success => ExitCode == 0 && !TimedOut;
}
