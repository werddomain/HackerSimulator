using HackerOs.App.Abstractions;
using HackerOs.AppSdk;

namespace HackerOs.Commands.Clear;

/// <summary>
/// Implements the <c>clear</c> terminal command (`P4-W5-CMD-006`).
/// Clears the active terminal buffer.
/// </summary>
public sealed class ClearCommand : TerminalAppBase
{
    public ClearCommand(AppManifest manifest) : base(manifest) { }

    public override ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        // ANSI escape code for clear screen and reset cursor
        context.StandardOutput.Write("\x1b[2J\x1b[H");
        return ValueTask.FromResult(0);
    }
}
