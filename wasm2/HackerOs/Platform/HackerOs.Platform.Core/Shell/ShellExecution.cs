using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.Policy;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Platform.Core.Shell;

/// <summary>A single-use elevation ticket (ADR 0042) handed to the next command the shell launches.</summary>
/// <param name="Token">Ticket token minted by <c>IElevationService</c>.</param>
/// <param name="Scope">What the ticket authorizes.</param>
public sealed record ShellElevation(Guid Token, ElevationScope Scope);

/// <summary>How one line is run by <see cref="ShellInterpreter.ExecuteAsync"/>.</summary>
public sealed class ShellExecutionOptions
{
    /// <summary>
    /// Gets where the last stage of each pipeline writes when it is not redirected: the terminal,
    /// or a buffer for a non-interactive shell. Never closed by the shell.
    /// </summary>
    public required TextWriter StandardOutput { get; init; }

    /// <summary>Gets where every stage writes its errors, and the shell its own messages. Never closed by the shell.</summary>
    public required TextWriter StandardError { get; init; }

    /// <summary>Gets the optional alternate screen offered to commands such as <c>nano</c>.</summary>
    public IFullScreenTerminalSession? FullScreen { get; init; }

    /// <summary>Gets the prompt for pending dangerous capabilities (ADR 0049 section 5), e.g. the terminal's text mode.</summary>
    public IDangerousCapabilityApprovalPrompt? ApprovalPrompt { get; init; }

    /// <summary>
    /// Gets the host's built-in commands, consulted for every stage before the catalog. The
    /// terminal still provides <c>cd</c>, <c>pwd</c>, <c>clear</c>, <c>help</c> and <c>sudo</c>
    /// here until <c>rebuild/3-shell-commands</c> moves them to the catalog (ADR 0051).
    /// </summary>
    public IShellBuiltins? Builtins { get; init; }

    /// <summary>
    /// Gets the callback offered a one-shot approval (ADR 0042) when a single-command pipeline
    /// failed on a permission denial; returning a ticket reruns the command with it.
    /// </summary>
    public Func<ShellPermissionDenial, CancellationToken, Task<ShellElevation?>>? PermissionApproval { get; init; }

    /// <summary>Gets a ticket consumed by the first command this line launches (e.g. an <c>ExecuteCommandIntent</c> retry).</summary>
    public ShellElevation? Elevation { get; init; }
}

/// <summary>A permission denial a command hit, offered to <see cref="ShellExecutionOptions.PermissionApproval"/>.</summary>
/// <param name="CommandName">The command as typed (after alias expansion).</param>
/// <param name="Error">The first filesystem denial the command raised.</param>
public sealed record ShellPermissionDenial(string CommandName, FileSystemError Error);

/// <summary>Why the last stage that ran did not complete normally, when it did not.</summary>
public enum ShellFailure
{
    /// <summary>The stage ran (whatever its exit code).</summary>
    None,

    /// <summary>The line did not parse; nothing ran (exit 2).</summary>
    Syntax,

    /// <summary>No installed command has this name (exit 127).</summary>
    CommandNotFound,

    /// <summary>The command exists but is uninstalled; the shell says "command not found" (exit 127).</summary>
    Disabled,

    /// <summary>The elevation ticket was refused (exit 1).</summary>
    ElevationDenied,

    /// <summary>The command's entry point threw (exit 1).</summary>
    Fault,

    /// <summary>A redirection target could not be opened; the stage did not start (exit 1).</summary>
    Redirection
}

/// <summary>Outcome of one line.</summary>
/// <param name="ExitCode">Exit code of the last pipeline that ran (also stored in <see cref="ShellSession.LastExitCode"/>).</param>
/// <param name="Failure">Why the last stage that ran failed to run, if it did.</param>
/// <param name="SyntaxError">The syntax error, when the line did not parse.</param>
/// <param name="PermissionDenials">Filesystem permission denials raised by the commands of the line.</param>
public sealed record ShellExecutionResult(
    int ExitCode,
    ShellFailure Failure,
    ShellSyntaxError? SyntaxError,
    IReadOnlyList<FileSystemError> PermissionDenials);

/// <summary>
/// Commands a shell host implements itself, consulted for each stage after expansion and alias
/// substitution, before the catalog (ADR 0050). Kept for the terminal's provisional built-ins.
/// </summary>
public interface IShellBuiltins
{
    /// <summary>Runs <paramref name="invocation"/> if it is a built-in.</summary>
    /// <returns>
    /// <see cref="ShellBuiltinResult.NotHandled"/> to let the catalog resolve the command,
    /// <see cref="ShellBuiltinResult.Completed"/> with an exit code, or
    /// <see cref="ShellBuiltinResult.Rewrite"/> to run another command line (e.g. <c>sudo</c>'s target) with a ticket.
    /// </returns>
    ValueTask<ShellBuiltinResult> TryRunAsync(ShellBuiltinInvocation invocation, CancellationToken cancellationToken);
}

/// <summary>One stage handed to <see cref="IShellBuiltins"/>.</summary>
/// <param name="Session">The shell.</param>
/// <param name="Arguments">Expanded <c>argv</c>, command name first.</param>
/// <param name="StandardInput">The stage's input.</param>
/// <param name="StandardOutput">The stage's output (the terminal, a pipe or a redirected file).</param>
/// <param name="StandardError">The stage's error output.</param>
public sealed record ShellBuiltinInvocation(
    ShellSession Session,
    IReadOnlyList<string> Arguments,
    TextReader StandardInput,
    TextWriter StandardOutput,
    TextWriter StandardError);

/// <summary>Answer of <see cref="IShellBuiltins.TryRunAsync"/>.</summary>
public sealed record ShellBuiltinResult
{
    private ShellBuiltinResult()
    {
    }

    /// <summary>Gets the result meaning "not a built-in".</summary>
    public static ShellBuiltinResult NotHandled { get; } = new();

    /// <summary>Gets the exit code of a completed built-in, or <see langword="null"/>.</summary>
    public int? ExitCode { get; private init; }

    /// <summary>Gets the replacement <c>argv</c> of a rewrite, or <see langword="null"/>.</summary>
    public IReadOnlyList<string>? Arguments { get; private init; }

    /// <summary>Gets the ticket the rewritten command runs with.</summary>
    public ShellElevation? Elevation { get; private init; }

    /// <summary>The built-in ran and exited with <paramref name="exitCode"/>.</summary>
    public static ShellBuiltinResult Completed(int exitCode) => new() { ExitCode = exitCode };

    /// <summary>Run <paramref name="arguments"/> from the catalog instead, with <paramref name="elevation"/>.</summary>
    public static ShellBuiltinResult Rewrite(IReadOnlyList<string> arguments, ShellElevation? elevation)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 0)
        {
            throw new ArgumentException("A rewrite needs a command name.", nameof(arguments));
        }

        return new() { Arguments = arguments, Elevation = elevation };
    }
}
