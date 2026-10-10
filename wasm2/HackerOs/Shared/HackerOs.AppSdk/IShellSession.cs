namespace HackerOs.AppSdk;

/// <summary>
/// Controlled access to the shell that ran a command (ADR 0051, section 3). As on Linux, <c>cd</c>
/// and <c>export</c> must act on the shell itself rather than on their own process: this is how a
/// catalog command reaches it. Exposed by <see cref="TerminalExecutionContext.Shell"/> only to a
/// command that declares <c>shell.control</c> and was started by an interactive shell.
/// </summary>
/// <remarks>
/// A command alone in its pipeline (including after <c>&amp;&amp;</c>) receives the real shell:
/// <c>cd /tmp &amp;&amp; ls</c> lists <c>/tmp</c>. Inside a pipe (<c>cd /tmp | cat</c>) it receives
/// a copy, like the subshell bash runs its built-ins in there: the command succeeds but its changes
/// are lost when the stage ends.
/// </remarks>
public interface IShellSession
{
    /// <summary>Gets the shell's current working directory (canonical absolute path).</summary>
    string WorkingDirectory { get; }

    /// <summary>Gets a snapshot of the shell's environment.</summary>
    IReadOnlyDictionary<string, string> Environment { get; }

    /// <summary>Gets the command lines entered in the shell, oldest first.</summary>
    IReadOnlyList<string> History { get; }

    /// <summary>
    /// Changes the shell's working directory, like the <c>cd</c> built-in: the target is checked
    /// (existence, type, permission to traverse) under the calling command's identity, then the
    /// shell's process directory, <c>PWD</c> and <c>OLDPWD</c> are updated at once, so the rest of
    /// an <c>&amp;&amp;</c> list sees the change.
    /// </summary>
    /// <param name="path">Absolute path, or a path relative to <see cref="WorkingDirectory"/>.</param>
    /// <param name="cancellationToken">Cancels the existence check.</param>
    /// <exception cref="ArgumentException">The path is empty or malformed.</exception>
    /// <exception cref="IO.HackDirectoryNotFoundException">The path does not exist.</exception>
    /// <exception cref="IO.HackIOException">The path is not a directory.</exception>
    /// <exception cref="IO.HackUnauthorizedAccessException">The path cannot be looked up.</exception>
    ValueTask ChangeDirectoryAsync(string path, CancellationToken cancellationToken);

    /// <summary>Sets one variable of the shell; commands started afterwards inherit it.</summary>
    /// <exception cref="ArgumentException">The name is not a valid shell identifier, or the value contains NUL.</exception>
    void SetVariable(string name, string value);

    /// <summary>Removes one variable of the shell; nothing happens when it is not set.</summary>
    /// <exception cref="ArgumentException">The name is not a valid shell identifier.</exception>
    void UnsetVariable(string name);

    /// <summary>Gets the installed commands (name, aliases, usage, description), ordered by name.</summary>
    IReadOnlyList<ShellCommandInfo> GetCommands();

    /// <summary>
    /// Runs one command as root (ADR 0042, ADR 0051 section 4), as <c>sudo</c> does: the shell asks
    /// for an administrator's credentials, mints a single-use <c>SudoRoot</c> ticket and starts the
    /// command as a child of the caller with it, its output going to <paramref name="standardOutput"/>
    /// and <paramref name="standardError"/>. The target never receives access to the shell.
    /// </summary>
    /// <param name="arguments">The command's <c>argv</c>, command name first.</param>
    /// <param name="standardOutput">Where the command writes its output.</param>
    /// <param name="standardError">Where the command and the shell write errors.</param>
    /// <param name="cancellationToken">Interrupts the prompt or the command.</param>
    /// <returns>The command's exit code, or <c>1</c> (shell built-in, refused credentials) or <c>127</c> (not found).</returns>
    ValueTask<int> RunElevatedAsync(
        IReadOnlyList<string> arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken);
}

/// <summary>One installed command, as listed by <see cref="IShellSession.GetCommands"/> for <c>help</c>.</summary>
/// <param name="Name">The command name typed in the shell.</param>
/// <param name="Aliases">Other names declared by the command's manifest.</param>
/// <param name="Usage">Usage line from the manifest.</param>
/// <param name="Description">Short description from the manifest.</param>
public sealed record ShellCommandInfo(
    string Name,
    IReadOnlyList<string> Aliases,
    string Usage,
    string Description);
