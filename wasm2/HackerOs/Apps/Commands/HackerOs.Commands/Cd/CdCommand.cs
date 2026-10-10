using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.Shell;

namespace HackerOs.Commands.Cd;

/// <summary>
/// Implements <c>cd [directory|-]</c> (ADR 0051, section 4). Like the bash built-in, it changes
/// the directory of the shell that ran it, through <see cref="TerminalExecutionContext.Shell"/>
/// (capability <c>shell.control</c>), not its own: the change is visible to the rest of an
/// <c>&amp;&amp;</c> list and to every command started afterwards.
/// </summary>
public sealed class CdCommand : TerminalAppBase
{
    /// <summary>Initializes the command with its manifest.</summary>
    public CdCommand(AppManifest manifest) : base(manifest)
    {
    }

    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Shell is not { } shell)
        {
            await context.StandardError.WriteLineAsync("cd: no shell session");
            return 1;
        }

        if (context.Arguments.Count > 1)
        {
            await context.StandardError.WriteLineAsync("cd: too many arguments");
            return 1;
        }

        // `cd` alone goes to HOME, `cd -` to OLDPWD (and prints it), as in bash.
        string target;
        bool printTarget = false;
        if (context.Arguments.Count == 0)
        {
            if (!shell.Environment.TryGetValue("HOME", out string? home) || home.Length == 0)
            {
                await context.StandardError.WriteLineAsync("cd: HOME not set");
                return 1;
            }

            target = home;
        }
        else if (context.Arguments[0] == "-")
        {
            if (!shell.Environment.TryGetValue("OLDPWD", out string? previous) || previous.Length == 0)
            {
                await context.StandardError.WriteLineAsync("cd: OLDPWD not set");
                return 1;
            }

            target = previous;
            printTarget = true;
        }
        else
        {
            target = context.Arguments[0];
        }

        try
        {
            await shell.ChangeDirectoryAsync(target, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await context.StandardError.WriteLineAsync($"cd: {target}: {ShellErrorText.Describe(exception)}");
            return 1;
        }

        if (printTarget)
        {
            await context.StandardOutput.WriteLineAsync(shell.WorkingDirectory);
        }

        return 0;
    }
}
