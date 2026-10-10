using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Shell;

/// <summary>
/// The state of one shell (ADR 0050, section 1): its process, its history and the exit code of
/// the last pipeline. The working directory and the environment are deliberately <b>not</b>
/// copied here: they are those of the shell's process (section 5), read and changed through
/// <see cref="IAppEnvironmentGateway"/>, so <c>ps</c>, <c>/proc/&lt;pid&gt;/cwd</c> and every
/// command the shell starts see exactly what the shell sees.
/// </summary>
/// <remarks>
/// The shell's process is the process of the app hosting it: the Terminal window for the
/// terminal, the calling app for <c>ExecuteCommandIntent</c>. A host that embeds several shells
/// (e.g. one terminal tab per folder in the code editor) therefore shares one directory between
/// them, as threads of one Linux process share theirs.
/// </remarks>
public sealed class ShellSession
{
    private readonly List<string> _history = [];
    private readonly IAppEnvironmentGateway _environment;
    private int _historyIndex;

    /// <summary>Creates a shell running in the process of <paramref name="host"/>.</summary>
    /// <param name="host">
    /// The hosting app's context: its process becomes the parent of every command, its file
    /// gateway opens redirection targets (with its capabilities and the file's mode bits, D16).
    /// </param>
    /// <param name="principal">The user the shell and its commands run as.</param>
    /// <param name="isInteractive">
    /// <see langword="true"/> for a terminal (history, approval prompts, streamed output);
    /// <see langword="false"/> for <c>ExecuteCommandIntent</c> (ADR 0050, section 7).
    /// </param>
    public ShellSession(IAppExecutionContext host, AuthenticatedPrincipal principal, bool isInteractive = true)
        : this(principal, host.Environment, host, host.ProcessId, isInteractive)
    {
        // ADR 0050 section 5: the shell adds TERM to its own environment and keeps PWD current.
        TrySetVariable("TERM", "xterm-256color");
        TrySetVariable("PWD", CurrentDirectory);
    }

    private ShellSession(
        AuthenticatedPrincipal principal,
        IAppEnvironmentGateway environment,
        IAppExecutionContext? host,
        ProcessId? processId,
        bool isInteractive)
    {
        Principal = principal ?? throw new ArgumentNullException(nameof(principal));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        Host = host;
        ProcessId = processId;
        IsInteractive = isInteractive;
    }

    /// <summary>
    /// Creates a non-interactive shell with no hosting process (an <c>ExecuteCommandIntent</c>
    /// whose caller supplied no context): its commands start with no parent, so they get the login
    /// environment, and it cannot open redirection targets.
    /// </summary>
    /// <param name="principal">The user the commands run as.</param>
    /// <param name="workingDirectory">Starting directory; the home directory when omitted.</param>
    /// <param name="hostName">Machine name for <c>$HOSTNAME</c>-like displays.</param>
    internal static ShellSession CreateDetached(AuthenticatedPrincipal principal, VirtualPath? workingDirectory, string hostName)
    {
        ArgumentNullException.ThrowIfNull(principal);
        string directory = workingDirectory?.Value ?? principal.HomePath;
        Dictionary<string, string> variables = new(
            Processes.LoginEnvironment.Build(principal.LoginName.Value, principal.HomePath, environmentFile: null),
            StringComparer.Ordinal)
        {
            ["PWD"] = directory
        };
        return new ShellSession(
            principal,
            new DetachedShellEnvironment(principal.LoginName.Value, hostName, directory, variables),
            host: null,
            processId: null,
            isInteractive: false);
    }

    /// <summary>Gets the hosting app's context, or <see langword="null"/> for a detached shell.</summary>
    public IAppExecutionContext? Host { get; }

    /// <summary>Gets the user the shell runs as.</summary>
    public AuthenticatedPrincipal Principal { get; }

    /// <summary>Gets the shell's process, parent of every command it starts; <see langword="null"/> when detached.</summary>
    public ProcessId? ProcessId { get; }

    /// <summary>Gets whether this is an interactive shell (a terminal).</summary>
    public bool IsInteractive { get; }

    /// <summary>Gets or sets the exit code of the last pipeline, read by <c>$?</c>.</summary>
    public int LastExitCode { get; set; }

    /// <summary>Gets the shell process's working directory.</summary>
    public string CurrentDirectory => _environment.CurrentDirectory;

    /// <summary>Gets <c>HOME</c>, or the principal's home directory when it is unset.</summary>
    public string HomeDirectory => _environment.GetVariable("HOME") is { Length: > 0 } home ? home : Principal.HomePath;

    /// <summary>Gets the login name the shell runs as.</summary>
    public string UserName => _environment.UserName;

    /// <summary>Gets the machine name (first line of <c>/etc/hostname</c>, read at session start).</summary>
    public string HostName => _environment.MachineName;

    /// <summary>Gets the commands entered in this shell, oldest first.</summary>
    public IReadOnlyList<string> History => _history;

    /// <summary>Gets one variable of the shell's process, or <see langword="null"/> when it is not set.</summary>
    public string? GetVariable(string name) => _environment.GetVariable(name);

    /// <summary>Gets a copy of the shell process's environment.</summary>
    public IReadOnlyDictionary<string, string> GetVariables() => _environment.GetVariables();

    /// <summary>Sets (or, with <see langword="null"/>, removes) a variable of the shell's own process.</summary>
    /// <exception cref="ArgumentException">The name or value is invalid.</exception>
    public void SetVariable(string name, string? value) => _environment.SetVariable(name, value);

    /// <summary>
    /// Changes the shell process's working directory, like the <c>cd</c> built-in: the target is
    /// checked through the host's file gateway (capabilities and mode bits), then <c>PWD</c> and
    /// <c>OLDPWD</c> are updated.
    /// </summary>
    /// <param name="path">Absolute, relative or <c>~</c>-prefixed directory.</param>
    /// <param name="cancellationToken">Cancels the existence check.</param>
    /// <exception cref="HackerOs.AppSdk.IO.HackDirectoryNotFoundException">The path does not exist.</exception>
    /// <exception cref="HackerOs.AppSdk.IO.HackIOException">The path is not a directory.</exception>
    /// <exception cref="HackerOs.AppSdk.IO.HackUnauthorizedAccessException">The path cannot be looked up.</exception>
    /// <exception cref="NotSupportedException">The shell is detached.</exception>
    public async Task ChangeDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        if (Host is null)
        {
            throw new NotSupportedException("A detached shell has no process to change directory.");
        }

        string previous = CurrentDirectory;
        using (HackExecutionContext.Enter(Host))
        {
            await HackEnvironment.SetCurrentDirectoryAsync(path, cancellationToken).ConfigureAwait(false);
        }

        TrySetVariable("OLDPWD", previous);
        TrySetVariable("PWD", CurrentDirectory);
    }

    /// <summary>Appends a command line to the history (skipping blanks and immediate repeats) and resets navigation.</summary>
    public void AddHistory(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return;
        }

        if (_history.Count == 0 || _history[^1] != commandLine)
        {
            _history.Add(commandLine);
        }

        _historyIndex = _history.Count;
    }

    /// <summary>
    /// Moves through the history: up (previous, <paramref name="delta"/> &lt; 0) or down (next,
    /// &gt; 0). Returns the entry to show, an empty string past the newest entry, or
    /// <see langword="null"/> when there is no history.
    /// </summary>
    public string? NavigateHistory(int delta)
    {
        if (_history.Count == 0)
        {
            return null;
        }

        _historyIndex = Math.Clamp(_historyIndex + delta, 0, _history.Count);
        return _historyIndex == _history.Count ? string.Empty : _history[_historyIndex];
    }

    /// <summary>
    /// Gets the prompt, <c>user@host:dir$</c> (<c>#</c> for root), with the home directory shown
    /// as <c>~</c>, like bash's default <c>PS1</c>.
    /// </summary>
    public string GetPrompt()
    {
        string directory = CurrentDirectory;
        string home = HomeDirectory.TrimEnd('/');
        if (home.Length > 0 && (directory == home || directory.StartsWith(home + "/", StringComparison.Ordinal)))
        {
            directory = "~" + directory[home.Length..];
        }

        char promptChar = UserName == "root" ? '#' : '$';
        return $"{UserName}@{HostName}:{directory}{promptChar}";
    }

    /// <summary>Sets a variable the shell maintains itself, ignoring a host that has no live environment.</summary>
    private void TrySetVariable(string name, string value)
    {
        try
        {
            _environment.SetVariable(name, value);
        }
        catch (NotSupportedException)
        {
            // A context with no live process environment (tests, legacy hosts): nothing to keep current.
        }
    }

    /// <summary>The environment of a detached shell: a private copy, since there is no process entry.</summary>
    private sealed class DetachedShellEnvironment(
        string userName,
        string machineName,
        string directory,
        Dictionary<string, string> variables) : IAppEnvironmentGateway
    {
        public string CurrentDirectory { get; private set; } = directory;

        public string UserName => userName;

        public string MachineName => machineName;

        public IReadOnlyList<string> GetCommandLineArgs() => ["hsh"];

        public string? GetVariable(string name) => variables.GetValueOrDefault(name);

        public IReadOnlyDictionary<string, string> GetVariables() => new Dictionary<string, string>(variables, StringComparer.Ordinal);

        public void SetCurrentDirectory(VirtualPath directory) => CurrentDirectory = directory.Value;

        public void SetVariable(string name, string? value)
        {
            if (value is null)
            {
                variables.Remove(name);
            }
            else
            {
                variables[name] = value;
            }
        }
    }
}
