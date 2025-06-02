using HackerOs.OS.User;

namespace HackerOs.OS.Applications;

/// <summary>
/// Event arguments for application state changes
/// </summary>
public class ApplicationStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// The application whose state changed
    /// </summary>
    public IApplication Application { get; }

    /// <summary>
    /// Previous state of the application
    /// </summary>
    public ApplicationState PreviousState { get; }

    /// <summary>
    /// New state of the application
    /// </summary>
    public ApplicationState NewState { get; }

    /// <summary>
    /// Timestamp when the state change occurred
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Reason for the state change (optional)
    /// </summary>
    public string? Reason { get; }

    public ApplicationStateChangedEventArgs(IApplication application, ApplicationState previousState, ApplicationState newState, string? reason = null)
    {
        Application = application;
        PreviousState = previousState;
        NewState = newState;
        Timestamp = DateTime.UtcNow;
        Reason = reason;
    }
}

/// <summary>
/// Event arguments for application output
/// </summary>
public class ApplicationOutputEventArgs : EventArgs
{
    /// <summary>
    /// The application that produced the output
    /// </summary>
    public IApplication Application { get; }

    /// <summary>
    /// Output text
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Output stream type
    /// </summary>
    public OutputStreamType StreamType { get; }

    /// <summary>
    /// Timestamp when the output was produced
    /// </summary>
    public DateTime Timestamp { get; }

    public ApplicationOutputEventArgs(IApplication application, string text, OutputStreamType streamType)
    {
        Application = application;
        Text = text;
        StreamType = streamType;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for application errors
/// </summary>
public class ApplicationErrorEventArgs : EventArgs
{
    /// <summary>
    /// The application that encountered the error
    /// </summary>
    public IApplication Application { get; }

    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Exception details (if available)
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Error severity level
    /// </summary>
    public ErrorSeverity Severity { get; }

    /// <summary>
    /// Timestamp when the error occurred
    /// </summary>
    public DateTime Timestamp { get; }

    public ApplicationErrorEventArgs(IApplication application, string message, ErrorSeverity severity, Exception? exception = null)
    {
        Application = application;
        Message = message;
        Severity = severity;
        Exception = exception;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for application launch
/// </summary>
public class ApplicationLaunchedEventArgs : EventArgs
{
    /// <summary>
    /// The application that was launched
    /// </summary>
    public IApplication Application { get; }

    /// <summary>
    /// Launch context used
    /// </summary>
    public ApplicationLaunchContext LaunchContext { get; }

    /// <summary>
    /// User session that launched the application
    /// </summary>
    public UserSession UserSession { get; }

    /// <summary>
    /// Timestamp when the application was launched
    /// </summary>
    public DateTime Timestamp { get; }

    public ApplicationLaunchedEventArgs(IApplication application, ApplicationLaunchContext launchContext)
    {
        Application = application;
        LaunchContext = launchContext;
        UserSession = launchContext.UserSession;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for application termination
/// </summary>
public class ApplicationTerminatedEventArgs : EventArgs
{
    /// <summary>
    /// The application that was terminated
    /// </summary>
    public IApplication Application { get; }

    /// <summary>
    /// Exit code of the application
    /// </summary>
    public int ExitCode { get; }

    /// <summary>
    /// Whether the termination was forced
    /// </summary>
    public bool WasForced { get; }

    /// <summary>
    /// Reason for termination
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// User session that owned the application
    /// </summary>
    public UserSession? UserSession { get; }

    /// <summary>
    /// Timestamp when the application was terminated
    /// </summary>
    public DateTime Timestamp { get; }

    public ApplicationTerminatedEventArgs(IApplication application, int exitCode, bool wasForced, string? reason = null)
    {
        Application = application;
        ExitCode = exitCode;
        WasForced = wasForced;
        Reason = reason;
        UserSession = application.OwnerSession;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Output stream types
/// </summary>
public enum OutputStreamType
{
    /// <summary>
    /// Standard output stream
    /// </summary>
    StandardOutput,

    /// <summary>
    /// Standard error stream
    /// </summary>
    StandardError,

    /// <summary>
    /// Debug output
    /// </summary>
    Debug,

    /// <summary>
    /// Application-specific output
    /// </summary>
    Application
}

/// <summary>
/// Error severity levels
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// Informational message
    /// </summary>
    Info,

    /// <summary>
    /// Warning that doesn't affect functionality
    /// </summary>
    Warning,

    /// <summary>
    /// Error that affects functionality but application continues
    /// </summary>
    Error,

    /// <summary>
    /// Critical error that may cause application termination
    /// </summary>
    Critical,

    /// <summary>
    /// Fatal error that causes immediate termination
    /// </summary>
    Fatal
}
