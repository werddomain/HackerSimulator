using HackerOs.App.Abstractions;

namespace HackerOs.AppSdk;

/// <summary>
/// Carries the app identity (and working directory) of the code currently running, so the
/// <c>HackerOs.AppSdk.IO</c> facade (<c>HackFile</c>, <c>HackDirectory</c>, ...) can reach the
/// caller's gateways without the developer passing an <see cref="IAppExecutionContext"/> around
/// (ADR 0043, section 2).
/// </summary>
/// <remarks>
/// <para>
/// The value lives in an <see cref="AsyncLocal{T}"/>, which flows across <c>await</c>,
/// <c>Task.Run</c> and <see cref="System.Threading.Timer"/> because all of them capture the
/// <see cref="ExecutionContext"/>. It does <b>not</b> flow into JavaScript callbacks
/// (<c>[JSInvokable]</c>): such a method must call <see cref="Enter"/> itself with its own
/// <c>AppContext</c>.
/// </para>
/// <para>
/// The OS opens a scope at every entry point where app code starts running (terminal command,
/// service run/stop, window component lifecycle and events, gateway callbacks). There is
/// deliberately no fallback: code running outside any scope gets a
/// <see cref="HackExecutionContextMissingException"/> instead of silently borrowing a system or
/// anonymous identity.
/// </para>
/// <para>
/// <see cref="Enter"/> is public on purpose: authority comes from the gateways, which only the
/// platform's trusted factory builds. An app that fabricates its own context only ever reaches its
/// own fake gateways, so entering it grants nothing.
/// </para>
/// </remarks>
public static class HackExecutionContext
{
    private static readonly AsyncLocal<HackAmbientFrame?> CurrentFrame = new();

    /// <summary>Gets the context of the app running the current code.</summary>
    /// <exception cref="HackExecutionContextMissingException">No scope is active.</exception>
    public static IAppExecutionContext Current => RequireFrame().App;

    /// <summary>Gets the context of the app running the current code, or <see langword="null"/> outside any scope.</summary>
    public static IAppExecutionContext? CurrentOrNull => CurrentFrame.Value?.App;

    /// <summary>
    /// Gets the canonical absolute working directory of the code currently running: the working
    /// directory of its process (ADR 0050, section 6), unless the scope was opened with an explicit
    /// one that no <see cref="HackEnvironment.SetCurrentDirectoryAsync"/> has replaced since.
    /// </summary>
    /// <exception cref="HackExecutionContextMissingException">No scope is active.</exception>
    public static string WorkingDirectory => RequireFrame().WorkingDirectory;

    /// <summary>
    /// Makes <paramref name="context"/> the ambient app context until the returned scope is
    /// disposed, which restores whatever was ambient before (scopes nest).
    /// </summary>
    /// <param name="context">The app context to make ambient.</param>
    /// <param name="workingDirectory">
    /// Absolute working directory pinned for this scope; <see langword="null"/> follows the
    /// process's own working directory (<see cref="IAppExecutionContext.Environment"/>), which is
    /// the home directory for a process started from the desktop.
    /// </param>
    /// <returns>A scope whose disposal restores the previous ambient context.</returns>
    /// <exception cref="ArgumentException"><paramref name="workingDirectory"/> is not a valid absolute path.</exception>
    public static IDisposable Enter(IAppExecutionContext context, string? workingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? directory = workingDirectory is null ? null : Canonicalize(workingDirectory, nameof(workingDirectory));
        HackAmbientFrame? previous = CurrentFrame.Value;
        CurrentFrame.Value = new HackAmbientFrame(context, directory);
        return new Scope(previous);
    }

    /// <summary>
    /// Drops the working directory pinned by <see cref="Enter"/> on the current scope, so it follows
    /// the process's directory again. Called after the process changed directory.
    /// </summary>
    internal static void UnpinWorkingDirectory()
    {
        if (CurrentFrame.Value is { } frame)
        {
            frame.PinnedDirectory = null;
        }
    }

    private static HackAmbientFrame RequireFrame() =>
        CurrentFrame.Value ?? throw new HackExecutionContextMissingException();

    private static string Canonicalize(string path, string parameterName)
    {
        try
        {
            return VirtualPath.Parse(path).Value;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new ArgumentException($"'{path}' is not a valid absolute working directory.", parameterName, exception);
        }
    }

    /// <summary>
    /// One ambient frame: the running app and an optional pinned working directory. The frame is
    /// shared by every flow that inherited it, so unpinning it is seen by all of them, like a
    /// process-wide <c>chdir</c>.
    /// </summary>
    private sealed class HackAmbientFrame(IAppExecutionContext app, string? pinnedDirectory)
    {
        public IAppExecutionContext App { get; } = app;

        public string? PinnedDirectory { get; set; } = pinnedDirectory;

        // The process table only ever stores canonical paths, so no re-parse is needed.
        public string WorkingDirectory => PinnedDirectory ?? App.Environment.CurrentDirectory;
    }

    /// <summary>Restores the previous frame exactly once.</summary>
    private sealed class Scope(HackAmbientFrame? previous) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                CurrentFrame.Value = previous;
            }
        }
    }
}

/// <summary>
/// Thrown when code that needs the ambient app context (the <c>Hack*</c> file API, for instance)
/// runs outside any <see cref="HackExecutionContext.Enter"/> scope.
/// </summary>
public sealed class HackExecutionContextMissingException : InvalidOperationException
{
    /// <summary>Initializes the exception with the standard explanatory message.</summary>
    public HackExecutionContextMissingException()
        : base("No HackerOS app context is active on this execution flow. App code normally runs inside a scope " +
               "opened by the OS; a [JSInvokable] callback or a thread started outside the app must call " +
               "HackExecutionContext.Enter(AppContext) itself.")
    {
    }
}
