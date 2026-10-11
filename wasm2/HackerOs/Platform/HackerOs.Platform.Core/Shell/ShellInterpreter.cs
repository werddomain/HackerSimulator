using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.IO;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Platform.Core.Shell;

/// <summary>
/// The headless shell <c>hsh</c> (ADR 0050, sections 1 to 4 and 7): parses a line, expands its
/// words, opens redirections, runs pipeline stages at the same time connected by
/// <see cref="ShellPipe"/>s, and chains pipelines with <c>&amp;&amp;</c>. Commands are started by
/// <see cref="AppLifecycleOrchestrator"/> as children of the shell's process. The interpreter has
/// no UI: the terminal and <c>ExecuteCommandIntent</c> both drive it.
/// </summary>
/// <remarks>
/// Exit codes: <c>127</c> command not found, <c>2</c> syntax error, <c>130</c> interrupted,
/// <c>1</c> unhandled fault or failed redirection, <c>141</c> broken pipe; otherwise the
/// command's own code. A pipeline's code is its last stage's (no <c>pipefail</c>).
/// </remarks>
public sealed class ShellInterpreter
{
    /// <summary>Per-user alias file read for the first word of each command (unchanged location).</summary>
    public const string AliasFileRelativePath = ".hackeros/aliases";

    /// <summary>Exit code of a command that could not be found.</summary>
    public const int CommandNotFoundExitCode = 127;

    /// <summary>Exit code of a line that did not parse.</summary>
    public const int SyntaxErrorExitCode = 2;

    /// <summary>Exit code of an interrupted pipeline (128 + SIGINT).</summary>
    public const int InterruptedExitCode = 130;

    private static readonly Encoding FileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly AppLifecycleOrchestrator _orchestrator;
    private readonly IAppEnablementRegistry _enablement;

    /// <summary>Creates an interpreter over the launch mechanism and the installed commands.</summary>
    /// <param name="orchestrator">Starts every command as a child process of the shell.</param>
    /// <param name="catalog">Commands are the catalog's terminal apps, found by name or static alias.</param>
    /// <param name="enablement">An uninstalled command is "not found", like a removed binary (ADR 0048).</param>
    public ShellInterpreter(AppLifecycleOrchestrator orchestrator, AppCatalog catalog, IAppEnablementRegistry enablement)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _enablement = enablement ?? throw new ArgumentNullException(nameof(enablement));
        Resolver = new ShellCommandResolver(catalog ?? throw new ArgumentNullException(nameof(catalog)));
    }

    /// <summary>Gets the command lookup (installed or not), shared with <c>help</c> and completion.</summary>
    public ShellCommandResolver Resolver { get; }

    /// <summary>
    /// Gets the installed commands, ordered by name: what <c>help</c> lists through
    /// <see cref="IShellSession.GetCommands"/> and what the terminal completes (ADR 0051, section 2).
    /// </summary>
    public IReadOnlyList<ShellCommandInfo> GetInstalledCommands() =>
        [.. Resolver.GetTerminalCommands()
            .Where(manifest => _enablement.IsEnabled(manifest.Id))
            .Select(manifest => new ShellCommandInfo(
                manifest.Terminal!.Name, manifest.Terminal.Aliases, manifest.Terminal.Usage, manifest.Description))];

    /// <summary>Runs one line in <paramref name="session"/>.</summary>
    /// <param name="session">The shell: process, environment, <c>$?</c>.</param>
    /// <param name="commandLine">The line as typed.</param>
    /// <param name="options">Output streams, built-ins and prompts.</param>
    /// <param name="cancellationToken">Ctrl+C: cancels every stage of the running pipeline and stops the line.</param>
    /// <returns>The exit code of the last pipeline that ran, and why it failed if it did not run.</returns>
    public async Task<ShellExecutionResult> ExecuteAsync(
        ShellSession session,
        string commandLine,
        ShellExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(options);

        ShellParseResult parsed = ShellParser.Parse(commandLine);
        if (parsed.Error is { } syntaxError)
        {
            await options.StandardError.WriteLineAsync($"hsh: {syntaxError.Message}").ConfigureAwait(false);
            session.LastExitCode = SyntaxErrorExitCode;
            return new ShellExecutionResult(SyntaxErrorExitCode, ShellFailure.Syntax, syntaxError, []);
        }

        if (parsed.Commands is null)
        {
            // A blank line runs nothing and, as in bash, leaves $? alone.
            return new ShellExecutionResult(session.LastExitCode, ShellFailure.None, null, []);
        }

        LineState line = new(options.Elevation);
        int exitCode = 0;
        ShellFailure failure = ShellFailure.None;
        for (int index = 0; index < parsed.Commands.Pipelines.Count; index++)
        {
            if (index > 0 && exitCode != 0)
            {
                break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                exitCode = InterruptedExitCode;
                break;
            }

            (exitCode, failure) = await RunPipelineAsync(
                session, parsed.Commands.Pipelines[index], options, line, cancellationToken).ConfigureAwait(false);
            // $? follows every pipeline, so `false && …` then `echo $?` and `a && echo $?` behave as in bash.
            session.LastExitCode = exitCode;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            exitCode = InterruptedExitCode;
        }

        session.LastExitCode = exitCode;
        return new ShellExecutionResult(exitCode, failure, null, line.PermissionDenials);
    }

    /// <summary>Expands every stage, then runs them all at once, connected by pipes.</summary>
    private async Task<(int ExitCode, ShellFailure Failure)> RunPipelineAsync(
        ShellSession session,
        ShellPipeline pipeline,
        ShellExecutionOptions options,
        LineState line,
        CancellationToken cancellationToken)
    {
        // Expansion happens for the whole pipeline before any stage starts: stages run
        // concurrently, so none may observe another one's side effects through $VAR or $?.
        List<ExpandedStage> stages = [];
        foreach (ShellCommand command in pipeline.Commands)
        {
            stages.Add(await ExpandAsync(session, command, options.StandardError, cancellationToken).ConfigureAwait(false));
        }

        using CancellationTokenSource pipelineCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ShellPipe[] pipes = [.. Enumerable.Range(0, stages.Count - 1).Select(_ => new ShellPipe())];
        Task<StageOutcome>[] running = new Task<StageOutcome>[stages.Count];
        for (int index = 0; index < stages.Count; index++)
        {
            ShellPipe? input = index == 0 ? null : pipes[index - 1];
            ShellPipe? output = index == stages.Count - 1 ? null : pipes[index];
            running[index] = RunStageAsync(
                session, stages[index], input, output, options, line, elevation: null, piped: stages.Count > 1, pipelineCancellation.Token);
        }

        StageOutcome[] outcomes = await Task.WhenAll(running).ConfigureAwait(false);
        StageOutcome last = outcomes[^1];

        // ADR 0042: a lone command that failed on a permission denial may be retried once with an
        // administrator's one-shot approval. Its input was empty, so rerunning it is safe; a
        // command that read the terminal's input is not rerun, the input is gone.
        if (stages.Count == 1
            && options.StandardInput is null
            && last is { Failure: ShellFailure.None, ExitCode: not 0, Elevated: false, Denials.Count: > 0 }
            && last.Denials[0].RequiredCapability is not null
            && options.PermissionApproval is { } approve
            && !cancellationToken.IsCancellationRequested)
        {
            ShellElevation? ticket = await approve(
                new ShellPermissionDenial(stages[0].Arguments[0], last.Denials[0]), cancellationToken).ConfigureAwait(false);
            if (ticket is not null)
            {
                last = await RunStageAsync(session, stages[0], null, null, options, line, ticket, piped: false, cancellationToken).ConfigureAwait(false);
            }
        }

        return (last.ExitCode, last.Failure);
    }

    /// <summary>Runs one stage with its input, output and redirections, then closes its pipe ends.</summary>
    private async Task<StageOutcome> RunStageAsync(
        ShellSession session,
        ExpandedStage stage,
        ShellPipe? inputPipe,
        ShellPipe? outputPipe,
        ShellExecutionOptions options,
        LineState line,
        ShellElevation? elevation,
        bool piped,
        CancellationToken cancellationToken)
    {
        TextReader input = inputPipe?.Reader ?? options.StandardInput ?? TextReader.Null;
        TextWriter output = outputPipe?.Writer ?? options.StandardOutput;
        TextWriter error = options.StandardError;
        RedirectionTarget? redirection = null;
        StageOutcome outcome;
        try
        {
            outcome = await RunStageCoreAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = StageOutcome.Failed(InterruptedExitCode, ShellFailure.None);
        }
        finally
        {
            // Like a process exiting: its pipe ends close, so the reader sees end of stream and the
            // writer feeding it gets "Broken pipe".
            outputPipe?.CloseWriter();
            inputPipe?.CloseReader();
        }

        if (redirection is not null && !await redirection.CloseAsync(error).ConfigureAwait(false))
        {
            // The file could not be published (e.g. `>>` to a file the user cannot write).
            outcome = StageOutcome.Failed(1, ShellFailure.Redirection);
        }

        return outcome;

        async Task<StageOutcome> RunStageCoreAsync()
        {
            if (stage.Error is not null)
            {
                return StageOutcome.Failed(1, ShellFailure.Fault);
            }

            if (stage.Redirections.Count > 0)
            {
                redirection = await OpenRedirectionsAsync(session, stage.Redirections, error, cancellationToken).ConfigureAwait(false);
                if (redirection is null)
                {
                    return StageOutcome.Failed(1, ShellFailure.Redirection);
                }

                // The stage's output now goes to the file: the next stage reads an empty input.
                outputPipe?.CloseWriter();
                output = redirection.Writer;
            }

            if (stage.Arguments.Count == 0)
            {
                // `> f` alone: the redirection was the whole command.
                return StageOutcome.Completed(0, [], elevated: false);
            }

            return await LaunchAsync(session, stage.Arguments, input, output, error, options, line, elevation, piped, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Resolves and starts one command, as a child of the shell's process.</summary>
    private async Task<StageOutcome> LaunchAsync(
        ShellSession session,
        IReadOnlyList<string> arguments,
        TextReader input,
        TextWriter output,
        TextWriter error,
        ShellExecutionOptions options,
        LineState line,
        ShellElevation? elevation,
        bool piped,
        CancellationToken cancellationToken)
    {
        string name = arguments[0];
        ShellCommandResolution? resolution = Resolver.Resolve(name, [.. arguments.Skip(1)]);
        if (resolution is null || !_enablement.IsEnabled(resolution.AppId))
        {
            await error.WriteLineAsync($"hsh: {name}: command not found").ConfigureAwait(false);
            return StageOutcome.Failed(CommandNotFoundExitCode, resolution is null ? ShellFailure.CommandNotFound : ShellFailure.Disabled);
        }

        // ADR 0051 section 3: a command declaring shell.control run by an interactive shell acts on
        // the shell itself, or on a copy inside a pipe (a subshell, whose changes are lost). The
        // orchestrator still hands it over only if the capability is granted.
        IShellSession? shell = session.IsInteractive && DeclaresShellControl(resolution.Manifest)
            ? new ShellSessionAccess(this, piped ? session.Fork() : session, options, input)
            : null;

        elevation ??= line.TakeElevation();
        StageOutcome outcome = await StartAsync(
            "hsh", session, resolution, session.ProcessId, input, output, error, options, elevation, shell, cancellationToken).ConfigureAwait(false);
        line.AddDenials(outcome.Denials);
        return outcome;
    }

    /// <summary>
    /// <c>sudo</c>'s side of the shell (ADR 0051, section 4; ADR 0042): refuses a shell built-in,
    /// asks an administrator's credentials, then runs the command as root, as a child of the caller
    /// (the <c>sudo</c> process) in the shell's directory, without access to the shell.
    /// </summary>
    internal async ValueTask<int> RunElevatedAsync(
        ShellSession session,
        IReadOnlyList<string> arguments,
        TextReader input,
        TextWriter output,
        TextWriter error,
        ShellExecutionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (arguments.Count == 0 || string.IsNullOrEmpty(arguments[0]))
        {
            throw new ArgumentException("A command name is required.", nameof(arguments));
        }

        string name = arguments[0];
        ShellCommandResolution? resolution = Resolver.Resolve(name, [.. arguments.Skip(1)]);
        if (resolution is null || !_enablement.IsEnabled(resolution.AppId))
        {
            await error.WriteLineAsync($"sudo: {name}: command not found").ConfigureAwait(false);
            return 1;
        }

        if (DeclaresShellControl(resolution.Manifest))
        {
            // The target never reaches the shell: an elevated `cd` would have nothing to act on.
            await error.WriteLineAsync($"sudo: {name}: shell builtin").ConfigureAwait(false);
            return 1;
        }

        if (options.CredentialPrompt is not { } prompt)
        {
            await error.WriteLineAsync("sudo: a terminal is required to read the password").ConfigureAwait(false);
            return 1;
        }

        ShellElevation? ticket = await prompt($"Run as root: {string.Join(" ", arguments)}", cancellationToken).ConfigureAwait(false);
        if (ticket is null)
        {
            await error.WriteLineAsync("sudo: authentication failed").ConfigureAwait(false);
            return 1;
        }

        // Only the command is elevated: a redirection was already opened by the unelevated shell,
        // so `sudo echo x > /etc/f` fails as on Linux.
        ProcessId? parent = HackExecutionContext.CurrentOrNull?.ProcessId ?? session.ProcessId;
        StageOutcome outcome = await StartAsync(
            "sudo", session, resolution, parent, input, output, error, options, ticket, shell: null, cancellationToken).ConfigureAwait(false);
        return outcome.ExitCode;
    }

    /// <summary>Starts a resolved command through the orchestrator and maps its launch status to an exit code.</summary>
    private async Task<StageOutcome> StartAsync(
        string caller,
        ShellSession session,
        ShellCommandResolution resolution,
        ProcessId? parent,
        TextReader input,
        TextWriter output,
        TextWriter error,
        ShellExecutionOptions options,
        ShellElevation? elevation,
        IShellSession? shell,
        CancellationToken cancellationToken)
    {
        string name = resolution.CommandName;
        AppLaunchResult launch = await _orchestrator.LaunchAsync(
            new AppLaunchRequest(
                resolution.AppId,
                session.Principal,
                resolution.Arguments,
                ParentPid: parent,
                WorkingDirectory: VirtualPath.Parse(session.CurrentDirectory),
                ElevationToken: elevation?.Token,
                ElevationScope: elevation?.Scope,
                ApprovalPrompt: options.ApprovalPrompt,
                // A shell with no process of its own passes its environment explicitly.
                Environment: parent is null ? session.GetVariables() : null,
                StandardInput: input,
                StandardOutput: output,
                StandardError: error,
                Shell: shell,
                CommandName: name),
            options.FullScreen,
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<FileSystemError> denials = launch.PermissionDenials ?? [];
        switch (launch.Status)
        {
            case AppLaunchStatus.Launched or AppLaunchStatus.FocusedExisting:
                return StageOutcome.Completed(launch.ExitCode ?? 0, denials, elevation is not null);
            case AppLaunchStatus.NotFound:
                await error.WriteLineAsync($"{caller}: {name}: command not found").ConfigureAwait(false);
                return StageOutcome.Failed(CommandNotFoundExitCode, ShellFailure.CommandNotFound);
            case AppLaunchStatus.Disabled:
                await error.WriteLineAsync($"{caller}: {name}: command not found").ConfigureAwait(false);
                return StageOutcome.Failed(CommandNotFoundExitCode, ShellFailure.Disabled);
            case AppLaunchStatus.ElevationDenied:
                await error.WriteLineAsync($"{caller}: {name}: elevation denied").ConfigureAwait(false);
                return StageOutcome.Failed(1, ShellFailure.ElevationDenied);
            default:
                if (cancellationToken.IsCancellationRequested)
                {
                    return StageOutcome.Failed(InterruptedExitCode, ShellFailure.None);
                }

                await error.WriteLineAsync($"{caller}: {name}: {launch.ErrorMessage ?? launch.ErrorCode}").ConfigureAwait(false);
                return StageOutcome.Failed(1, ShellFailure.Fault);
        }
    }

    /// <summary>Whether a command asks for its calling shell (ADR 0051, section 3).</summary>
    private static bool DeclaresShellControl(AppManifest manifest) =>
        manifest.Capabilities.Contains(AppCapabilities.ShellControl, StringComparer.Ordinal);

    /// <summary>
    /// Expands one command's words (ADR 0050, section 3) and applies alias substitution to its
    /// first word, as a shell does before resolving a command.
    /// </summary>
    private static async Task<ExpandedStage> ExpandAsync(
        ShellSession session, ShellCommand command, TextWriter error, CancellationToken cancellationToken)
    {
        List<ShellWord> words = [.. command.Words];
        List<ShellRedirection> redirections = [.. command.Redirections];
        string? prefixError = null;

        if (words.Count > 0 && words[0].PlainText is { } name && name != "alias")
        {
            string? value = await TryReadAliasAsync(session, name, cancellationToken).ConfigureAwait(false);
            if (value is not null)
            {
                ShellParseResult alias = ShellParser.Parse(value);
                if (alias.Commands is { Pipelines: [{ Commands: [ShellCommand aliased] }] } && aliased.Words.Count > 0)
                {
                    words = [.. aliased.Words, .. words.Skip(1)];
                    redirections = [.. aliased.Redirections, .. redirections];
                }
                else
                {
                    prefixError = $"hsh: alias {name}: only a simple command can be aliased";
                }
            }
        }

        if (prefixError is not null)
        {
            await error.WriteLineAsync(prefixError).ConfigureAwait(false);
            return new ExpandedStage([], [], prefixError);
        }

        return new ExpandedStage(
            [.. words.Select(word => Expand(word, session))],
            [.. redirections.Select(r => (r.Kind, Expand(r.Target, session)))],
            null);
    }

    /// <summary>Substitutes variables, <c>$?</c> and <c>~</c>; the result is one argument, never re-split.</summary>
    internal static string Expand(ShellWord word, ShellSession session)
    {
        StringBuilder text = new();
        foreach (ShellWordPart part in word.Parts)
        {
            text.Append(part switch
            {
                ShellLiteralPart literal => literal.Text,
                ShellVariablePart variable => session.GetVariable(variable.Name) ?? string.Empty,
                ShellExitStatusPart => session.LastExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ShellTildePart => session.HomeDirectory,
                _ => string.Empty
            });
        }

        return text.ToString();
    }

    /// <summary>Looks <paramref name="name"/> up in <c>~/.hackeros/aliases</c> (<c>name=value</c> lines), as the shell's user.</summary>
    private static async Task<string?> TryReadAliasAsync(ShellSession session, string name, CancellationToken cancellationToken)
    {
        if (session.Host is null)
        {
            return null;
        }

        string content;
        try
        {
            using (HackExecutionContext.Enter(session.Host))
            {
                string path = $"{session.HomeDirectory.TrimEnd('/')}/{AliasFileRelativePath}";
                if (!await HackFile.ExistsAsync(path, cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                content = await HackFile.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }

        foreach (string rawLine in content.Split('\n'))
        {
            string entry = rawLine.TrimEnd('\r').Trim();
            int equals = entry.IndexOf('=');
            if (equals > 0 && string.Equals(entry[..equals], name, StringComparison.Ordinal))
            {
                return entry[(equals + 1)..];
            }
        }

        return null;
    }

    /// <summary>
    /// Opens a stage's redirections in order, with the host's identity and capabilities (ADR 0050,
    /// section 4): each one is created or emptied (<c>&gt;</c>) or created (<c>&gt;&gt;</c>), and
    /// the last one receives the output. Returns <see langword="null"/> after printing
    /// <c>hsh: target: reason</c> when one cannot be opened.
    /// </summary>
    private static async Task<RedirectionTarget?> OpenRedirectionsAsync(
        ShellSession session,
        IReadOnlyList<(ShellRedirectionKind Kind, string Target)> redirections,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (session.Host is null)
        {
            await error.WriteLineAsync($"hsh: {redirections[0].Target}: cannot open files without a calling app").ConfigureAwait(false);
            return null;
        }

        HackFileStream? stream = null;
        string target = string.Empty;
        for (int index = 0; index < redirections.Count; index++)
        {
            (ShellRedirectionKind kind, target) = redirections[index];
            try
            {
                using (HackExecutionContext.Enter(session.Host))
                {
                    if (kind == ShellRedirectionKind.Append)
                    {
                        // The engine has no append mode to check at open: an empty append creates the
                        // file or proves it writable, so a permission problem stops the stage first.
                        await HackFile.AppendAllTextAsync(target, string.Empty, cancellationToken).ConfigureAwait(false);
                        stream = await HackFile.OpenAppendAsync(target, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        stream = await HackFile.OpenWriteAsync(target, cancellationToken).ConfigureAwait(false);
                    }
                }

                // Truncation (or creation) happens at open, like O_TRUNC|O_CREAT, so a permission
                // problem stops the stage before it starts.
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                if (stream is not null)
                {
                    await DisposeQuietlyAsync(stream).ConfigureAwait(false);
                }

                await error.WriteLineAsync($"hsh: {target}: {ShellErrorText.Describe(exception)}").ConfigureAwait(false);
                return null;
            }

            if (index < redirections.Count - 1)
            {
                await DisposeQuietlyAsync(stream).ConfigureAwait(false);
                stream = null;
            }
        }

        return new RedirectionTarget(target, new StreamWriter(stream!, FileEncoding) { NewLine = "\n" });
    }

    private static async Task DisposeQuietlyAsync(HackFileStream stream)
    {
        try
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing more was written to it; the open already reported what mattered.
        }
    }

    /// <summary>A stage after expansion.</summary>
    private sealed record ExpandedStage(
        IReadOnlyList<string> Arguments,
        IReadOnlyList<(ShellRedirectionKind Kind, string Target)> Redirections,
        string? Error);

    /// <summary>What one stage did.</summary>
    private sealed record StageOutcome(int ExitCode, ShellFailure Failure, IReadOnlyList<FileSystemError> Denials, bool Elevated)
    {
        public static StageOutcome Completed(int exitCode, IReadOnlyList<FileSystemError> denials, bool elevated) =>
            new(exitCode, ShellFailure.None, denials, elevated);

        public static StageOutcome Failed(int exitCode, ShellFailure failure) => new(exitCode, failure, [], false);
    }

    /// <summary>State shared by the pipelines of one line.</summary>
    private sealed class LineState(ShellElevation? elevation)
    {
        private readonly object _sync = new();
        private readonly List<FileSystemError> _denials = [];
        private ShellElevation? _elevation = elevation;

        public IReadOnlyList<FileSystemError> PermissionDenials
        {
            get
            {
                lock (_sync)
                {
                    return [.. _denials];
                }
            }
        }

        /// <summary>Hands the line's ticket to the first command launched; single use.</summary>
        public ShellElevation? TakeElevation()
        {
            lock (_sync)
            {
                ShellElevation? taken = _elevation;
                _elevation = null;
                return taken;
            }
        }

        public void AddDenials(IReadOnlyList<FileSystemError> denials)
        {
            lock (_sync)
            {
                _denials.AddRange(denials);
            }
        }
    }

    /// <summary>The open file of a redirected stage.</summary>
    private sealed class RedirectionTarget(string target, StreamWriter writer)
    {
        public StreamWriter Writer { get; } = writer;

        /// <summary>Publishes the file; prints <c>hsh: target: reason</c> and returns false if that fails.</summary>
        public async Task<bool> CloseAsync(TextWriter error)
        {
            try
            {
                await Writer.DisposeAsync().ConfigureAwait(false);
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                await error.WriteLineAsync($"hsh: {target}: {ShellErrorText.Describe(exception)}").ConfigureAwait(false);
                return false;
            }
        }
    }
}
