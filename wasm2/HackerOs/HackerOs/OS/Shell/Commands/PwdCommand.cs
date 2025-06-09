
using HackerOs.OS.Shell;

namespace HackerOs.OS.Shell.Commands;

/// <summary>
/// Print working directory command (pwd)
/// </summary>
public class PwdCommand : CommandBase
{
    public override string Name => "pwd";
    public override string Description => "Print current working directory";
    public override string Usage => "pwd";

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        string[] args,
        Stream stdin,
        Stream stdout,
        Stream stderr,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await WriteLineAsync(stdout, context.CurrentWorkingDirectory);
            return 0;
        }
        catch (Exception ex)
        {
            await WriteLineAsync(stderr, $"pwd: {ex.Message}");
            return 1;
        }
    }

    public override Task<IEnumerable<string>> GetCompletionsAsync(
        CommandContext context,
        string[] args,
        string currentArg)
    {
        // pwd doesn't need completions
        return Task.FromResult(Enumerable.Empty<string>());
    }

    public override CommandValidationResult ValidateArguments(string[] args)
    {
        if (args.Length > 0)
        {
            return CommandValidationResult.Error("pwd: too many arguments");
        }

        return CommandValidationResult.Success();
    }
}
