using HackerOs.App.Abstractions;
using HackerOs.AppSdk;

namespace HackerOs.Commands.Help;

/// <summary>
/// Implements <c>help [command]</c> / <c>man</c> (ADR 0051, section 4): lists the commands
/// installed right now, read from the shell (<see cref="IShellSession.GetCommands"/>), or prints
/// one command's usage. Nothing is hard-coded: a third-party command appears as soon as it is
/// installed and disappears when it is uninstalled.
/// </summary>
public sealed class HelpCommand : TerminalAppBase
{
    /// <summary>Initializes the command with its manifest.</summary>
    public HelpCommand(AppManifest manifest) : base(manifest) { }

    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        TextWriter output = context.StandardOutput;
        if (context.Shell is not { } shell)
        {
            await context.StandardError.WriteLineAsync("help: no shell session");
            return 1;
        }

        IReadOnlyList<ShellCommandInfo> commands = shell.GetCommands();
        if (context.Arguments.Count > 0)
        {
            int status = 0;
            foreach (string name in context.Arguments)
            {
                ShellCommandInfo? command = commands.FirstOrDefault(c =>
                    string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
                    || c.Aliases.Any(alias => string.Equals(alias, name, StringComparison.OrdinalIgnoreCase)));
                if (command is null)
                {
                    await context.StandardError.WriteLineAsync($"help: no help topics match '{name}'");
                    status = 1;
                    continue;
                }

                await output.WriteLineAsync($"{command.Name} - {command.Description}");
                await output.WriteLineAsync($"usage: {command.Usage}");
                if (command.Aliases.Count > 0)
                {
                    await output.WriteLineAsync($"aliases: {string.Join(", ", command.Aliases)}");
                }
            }

            return status;
        }

        await output.WriteLineAsync("HackerOS shell (hsh). Installed commands:");
        int width = commands.Count == 0 ? 0 : commands.Max(c => c.Name.Length);
        foreach (ShellCommandInfo command in commands)
        {
            await output.WriteLineAsync($"  {command.Name.PadRight(width)}  {command.Description}");
        }

        await output.WriteLineAsync();
        await output.WriteLineAsync("Operators: cmd | cmd, cmd > file, cmd >> file, cmd && cmd, $VAR, ${VAR}, $?, ~");
        await output.WriteLineAsync("Type 'help <command>' for its usage.");
        return 0;
    }
}
