namespace HackerOs.AppSdk;

/// <summary>
/// Provides renderer-independent command input, output, and shell state.
/// </summary>
public sealed class TerminalExecutionContext
{
    /// <summary>
    /// Initializes a command execution context.
    /// </summary>
    /// <param name="app">Scoped application execution context.</param>
    /// <param name="arguments">Parsed positional command arguments.</param>
    /// <param name="standardInput">Command standard input stream.</param>
    /// <param name="standardOutput">Command standard output stream.</param>
    /// <param name="standardError">Command standard error stream.</param>
    /// <param name="workingDirectory">Current virtual filesystem directory.</param>
    /// <param name="environment">Read-only shell environment for this execution.</param>
    public TerminalExecutionContext(
        IAppExecutionContext app,
        IReadOnlyList<string> arguments,
        TextReader standardInput,
        TextWriter standardOutput,
        TextWriter standardError,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(standardInput);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(environment);

        App = app;
        Arguments = arguments;
        StandardInput = standardInput;
        StandardOutput = standardOutput;
        StandardError = standardError;
        WorkingDirectory = workingDirectory;
        Environment = environment;
    }

    /// <summary>Gets the scoped application execution context.</summary>
    public IAppExecutionContext App { get; }

    /// <summary>Gets parsed positional command arguments.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>Gets the renderer-independent standard input stream.</summary>
    public TextReader StandardInput { get; }

    /// <summary>Gets the renderer-independent standard output stream.</summary>
    public TextWriter StandardOutput { get; }

    /// <summary>Gets the renderer-independent standard error stream.</summary>
    public TextWriter StandardError { get; }

    /// <summary>Gets the current virtual filesystem directory.</summary>
    public string WorkingDirectory { get; }

    /// <summary>Gets the read-only environment variables for this execution.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; }
}