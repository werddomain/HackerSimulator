using HackerOs.OS.User;

namespace HackerOs.OS.Applications;

/// <summary>
/// Interface for application management in HackerOS
/// </summary>
public interface IApplication
{
    /// <summary>
    /// Unique identifier for the application
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name of the application
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what the application does
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Application version string
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Type of application (windowed, command-line, etc.)
    /// </summary>
    ApplicationType Type { get; }

    /// <summary>
    /// Application manifest with metadata
    /// </summary>
    ApplicationManifest Manifest { get; }

    /// <summary>
    /// Current state of the application
    /// </summary>
    ApplicationState State { get; }

    /// <summary>
    /// User session that owns this application instance
    /// </summary>
    UserSession? OwnerSession { get; }

    /// <summary>
    /// Process ID assigned by the kernel
    /// </summary>
    int ProcessId { get; }

    /// <summary>
    /// Event raised when application state changes
    /// </summary>
    event EventHandler<ApplicationStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when application produces output
    /// </summary>
    event EventHandler<ApplicationOutputEventArgs>? OutputReceived;

    /// <summary>
    /// Event raised when application encounters an error
    /// </summary>
    event EventHandler<ApplicationErrorEventArgs>? ErrorReceived;

    /// <summary>
    /// Start the application with the given context
    /// </summary>
    /// <param name="context">Application launch context</param>
    /// <returns>True if started successfully</returns>
    Task<bool> StartAsync(ApplicationLaunchContext context);

    /// <summary>
    /// Stop the application gracefully
    /// </summary>
    /// <returns>True if stopped successfully</returns>
    Task<bool> StopAsync();

    /// <summary>
    /// Pause the application execution
    /// </summary>
    /// <returns>True if paused successfully</returns>
    Task<bool> PauseAsync();

    /// <summary>
    /// Resume the application execution
    /// </summary>
    /// <returns>True if resumed successfully</returns>
    Task<bool> ResumeAsync();

    /// <summary>
    /// Force terminate the application
    /// </summary>
    /// <returns>True if terminated successfully</returns>
    Task<bool> TerminateAsync();

    /// <summary>
    /// Send a message to the application
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <returns>True if message was delivered</returns>
    Task<bool> SendMessageAsync(object message);

    /// <summary>
    /// Get application statistics
    /// </summary>
    /// <returns>Application runtime statistics</returns>
    ApplicationStatistics GetStatistics();
}

/// <summary>
/// Types of applications supported by HackerOS
/// </summary>
public enum ApplicationType
{
    /// <summary>
    /// Graphical application with windows
    /// </summary>
    WindowedApplication,

    /// <summary>
    /// Command-line tool executed in shell
    /// </summary>
    CommandLineTool,

    /// <summary>
    /// Background service or daemon
    /// </summary>
    SystemService,

    /// <summary>
    /// Built-in OS component
    /// </summary>
    SystemApplication
}

/// <summary>
/// Current state of an application
/// </summary>
public enum ApplicationState
{
    /// <summary>
    /// Application is not running
    /// </summary>
    Stopped,

    /// <summary>
    /// Application is starting up
    /// </summary>
    Starting,

    /// <summary>
    /// Application is running normally
    /// </summary>
    Running,

    /// <summary>
    /// Application is paused
    /// </summary>
    Paused,

    /// <summary>
    /// Application is stopping
    /// </summary>
    Stopping,

    /// <summary>
    /// Application has crashed or encountered an error
    /// </summary>
    Crashed,

    /// <summary>
    /// Application is waiting for user input
    /// </summary>
    WaitingForInput
}
