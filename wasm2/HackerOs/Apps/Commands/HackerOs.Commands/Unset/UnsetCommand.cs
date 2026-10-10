using HackerOs.App.Abstractions;
using HackerOs.AppSdk;

namespace HackerOs.Commands.Unset;

/// <summary>
/// Implements <c>unset NAME...</c> (ADR 0051, section 4): removes variables from the calling shell
/// through <see cref="IShellSession.UnsetVariable"/>. Unsetting a variable that is not set is not
/// an error, as in bash.
/// </summary>
public sealed class UnsetCommand : TerminalAppBase
{
    /// <summary>Initializes the command with its manifest.</summary>
    public UnsetCommand(AppManifest manifest) : base(manifest) { }

    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Shell is not { } shell)
        {
            await context.StandardError.WriteLineAsync("unset: no shell session");
            return 1;
        }

        int status = 0;
        foreach (string name in context.Arguments)
        {
            try
            {
                shell.UnsetVariable(name);
            }
            catch (ArgumentException)
            {
                await context.StandardError.WriteLineAsync($"unset: `{name}': not a valid identifier");
                status = 1;
            }
        }

        return status;
    }
}
