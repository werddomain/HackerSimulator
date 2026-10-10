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

    /// <summary>Gets the canonical absolute working directory of the code currently running.</summary>
    /// <exception cref="HackExecutionContextMissingException">No scope is active.</exception>
    public static string WorkingDirectory => RequireFrame().WorkingDirectory;

    /// <summary>
    /// Makes <paramref name="context"/> the ambient app context until the returned scope is
    /// disposed, which restores whatever was ambient before (scopes nest).
    /// </summary>
    /// <param name="context">The app context to make ambient.</param>
    /// <param name="workingDirectory">
    /// Absolute working directory; <see langword="null"/> means the user's home directory
    /// (<see cref="IAppExecutionContext.HomeDirectory"/>).
    /// </param>
    /// <returns>A scope whose disposal restores the previous ambient context.</returns>
    /// <exception cref="ArgumentException"><paramref name="workingDirectory"/> is not a valid absolute path.</exception>
    public static IDisposable Enter(IAppExecutionContext context, string? workingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        string directory = Canonicalize(workingDirectory ?? context.HomeDirectory, nameof(workingDirectory));
        HackAmbientFrame? previous = CurrentFrame.Value;
        CurrentFrame.Value = new HackAmbientFrame(context, directory);
        return new Scope(previous);
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

    /// <summary>One ambient frame: the running app and its working directory.</summary>
    private sealed record HackAmbientFrame(IAppExecutionContext App, string WorkingDirectory);

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
