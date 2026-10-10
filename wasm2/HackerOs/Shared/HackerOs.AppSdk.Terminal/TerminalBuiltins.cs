using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Platform.Core.Shell;

namespace HackerOs.AppSdk.Terminal;

/// <summary>
/// The terminal's provisional built-ins (<c>cd</c>, <c>pwd</c>, <c>clear</c>, <c>help</c>,
/// <c>sudo</c>), handed to <see cref="ShellInterpreter"/> through <see cref="IShellBuiltins"/>.
/// They now run inside the shell grammar (pipes, redirections, <c>&amp;&amp;</c>) and act on the
/// shell's process, but their names stay hard-coded until <c>rebuild/3-shell-commands</c> moves
/// them to the catalog behind <c>IShellSession</c> (ADR 0051).
/// </summary>
internal sealed class TerminalBuiltins(
    ShellCommandResolver resolver,
    Action clearScreen,
    Func<string, Task<ElevationRequestResult?>> requestSudo) : IShellBuiltins
{
    /// <summary>Names the terminal still intercepts; they are hidden from the catalog listing.</summary>
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.Ordinal)
    {
        "cd", "pwd", "clear", "help", "sudo"
    };

    /// <inheritdoc />
    public async ValueTask<ShellBuiltinResult> TryRunAsync(ShellBuiltinInvocation invocation, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> args = invocation.Arguments;
        switch (args[0])
        {
            case "clear":
                clearScreen();
                return ShellBuiltinResult.Completed(0);

            case "pwd":
                await invocation.StandardOutput.WriteLineAsync(invocation.Session.CurrentDirectory);
                return ShellBuiltinResult.Completed(0);

            case "cd":
                return ShellBuiltinResult.Completed(await ChangeDirectoryAsync(invocation, cancellationToken));

            case "help":
                return ShellBuiltinResult.Completed(await HelpAsync(invocation));

            case "sudo":
                return await SudoAsync(invocation);

            default:
                return ShellBuiltinResult.NotHandled;
        }
    }

    private static async Task<int> ChangeDirectoryAsync(ShellBuiltinInvocation invocation, CancellationToken cancellationToken)
    {
        // `cd` alone goes home; `~` was already expanded by the shell.
        string target = invocation.Arguments.Count > 1 ? invocation.Arguments[1] : invocation.Session.HomeDirectory;
        try
        {
            await invocation.Session.ChangeDirectoryAsync(target, cancellationToken);
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await invocation.StandardError.WriteLineAsync($"cd: {target}: {ShellErrorText.Describe(exception)}");
            return 1;
        }
    }

    private async Task<int> HelpAsync(ShellBuiltinInvocation invocation)
    {
        TextWriter output = invocation.StandardOutput;
        if (invocation.Arguments.Count > 1)
        {
            string name = invocation.Arguments[1];
            AppManifest? target = resolver.GetTerminalCommands()
                .FirstOrDefault(m => string.Equals(m.Terminal!.Name, name, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                await invocation.StandardError.WriteLineAsync($"help: no manual entry for '{name}'");
                return 1;
            }

            await output.WriteLineAsync($"{target.Terminal!.Name} - {target.Description}");
            await output.WriteLineAsync($"usage: {target.Terminal.Usage}");
            return 0;
        }

        await output.WriteLineAsync("Built-in commands: clear, cd, pwd, help, sudo");
        await output.WriteLineAsync("Catalog commands: " + string.Join(", ", CatalogCommandNames(resolver)));
        await output.WriteLineAsync("Operators: cmd | cmd, cmd > file, cmd >> file, cmd && cmd, $VAR, $?, ~");
        await output.WriteLineAsync("Type 'help <command>' for usage.");
        return 0;
    }

    private async Task<ShellBuiltinResult> SudoAsync(ShellBuiltinInvocation invocation)
    {
        IReadOnlyList<string> args = invocation.Arguments;
        if (args.Count < 2)
        {
            await invocation.StandardError.WriteLineAsync("sudo: usage: sudo <command> [args...]");
            return ShellBuiltinResult.Completed(1);
        }

        IReadOnlyList<string> command = [.. args.Skip(1)];
        if (Names.Contains(command[0]))
        {
            await invocation.StandardError.WriteLineAsync($"sudo: {command[0]}: command not found");
            return ShellBuiltinResult.Completed(1);
        }

        ElevationRequestResult? approval = await requestSudo($"Run as root: {string.Join(" ", command)}");
        if (approval is not { Success: true, Token: Guid token })
        {
            await invocation.StandardError.WriteLineAsync("sudo: authentication failed");
            return ShellBuiltinResult.Completed(1);
        }

        // Only the command is elevated: a redirection was already opened by the unelevated shell,
        // so `sudo echo x > /etc/f` fails as on Linux.
        return ShellBuiltinResult.Rewrite(command, new ShellElevation(token, ElevationScope.SudoRoot));
    }

    /// <summary>Installed catalog command names, without the ones the terminal intercepts.</summary>
    public static IEnumerable<string> CatalogCommandNames(ShellCommandResolver resolver) =>
        resolver.GetTerminalCommands()
            .Select(m => m.Terminal!.Name)
            .Where(name => !Names.Contains(name));
}
