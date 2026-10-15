using HackerOs.App.Abstractions;
using HackerOs.AppSdk;

namespace HackerOs.Commands.Whoami;

/// <summary>
/// <c>whoami</c>: prints the effective user name (ADR 0064): <c>root</c> under <c>sudo</c>,
/// otherwise the login name of the process, like <c>id -un</c>.
/// </summary>
public sealed class WhoamiCommand(AppManifest manifest) : TerminalAppBase(manifest)
{
    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Arguments.Count > 0 && context.Arguments[0] is not "--help")
        {
            await context.StandardError.WriteLineAsync($"whoami: extra operand '{context.Arguments[0]}'");
            return 1;
        }

        await context.StandardOutput.WriteLineAsync(context.App.IsRootElevated ? "root" : context.App.UserLoginName);
        return 0;
    }
}
