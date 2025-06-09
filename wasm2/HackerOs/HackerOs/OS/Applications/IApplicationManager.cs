using HackerOs.OS.User;

namespace HackerOs.OS.Applications;

/// <summary>
/// Interface for managing applications in HackerOS
/// </summary>
public interface IApplicationManager
{
    /// <summary>
    /// Event raised when an application is launched
    /// </summary>
    event EventHandler<ApplicationLaunchedEventArgs>? ApplicationLaunched;

    /// <summary>
    /// Event raised when an application is terminated
    /// </summary>
    event EventHandler<ApplicationTerminatedEventArgs>? ApplicationTerminated;

    /// <summary>
    /// Event raised when an application state changes
    /// </summary>
    event EventHandler<ApplicationStateChangedEventArgs>? ApplicationStateChanged;

    /// <summary>
    /// Launch an application by its ID
    /// </summary>
    /// <param name="applicationId">Unique application identifier</param>
    /// <param name="context">Launch context with user session and parameters</param>
    /// <returns>The launched application instance, or null if launch failed</returns>
    Task<IApplication?> LaunchApplicationAsync(string applicationId, ApplicationLaunchContext context);

    /// <summary>
    /// Launch an application by its manifest
    /// </summary>
    /// <param name="manifest">Application manifest</param>
    /// <param name="context">Launch context</param>
    /// <returns>The launched application instance, or null if launch failed</returns>
    Task<IApplication?> LaunchApplicationAsync(ApplicationManifest manifest, ApplicationLaunchContext context);

    /// <summary>
    /// Register a new application with the system
    /// </summary>
    /// <param name="manifest">Application manifest with metadata</param>
    /// <returns>True if registration successful</returns>
    Task<bool> RegisterApplicationAsync(ApplicationManifest manifest);

    /// <summary>
    /// Unregister an application from the system
    /// </summary>
    /// <param name="applicationId">Application ID to unregister</param>
    /// <returns>True if unregistration successful</returns>
    Task<bool> UnregisterApplicationAsync(string applicationId);

    /// <summary>
    /// Get all available applications
    /// </summary>
    /// <returns>List of available application manifests</returns>
    IReadOnlyList<ApplicationManifest> GetAvailableApplications();

    /// <summary>
    /// Get all currently running applications
    /// </summary>
    /// <returns>List of running application instances</returns>
    IReadOnlyList<IApplication> GetRunningApplications();

    /// <summary>
    /// Get running applications for a specific user session
    /// </summary>
    /// <param name="session">User session</param>
    /// <returns>List of applications running for the session</returns>
    IReadOnlyList<IApplication> GetRunningApplications(UserSession session);

    /// <summary>
    /// Get a specific application by its ID
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <returns>Application manifest if found, null otherwise</returns>
    ApplicationManifest? GetApplication(string applicationId);

    /// <summary>
    /// Get a running application instance by its process ID
    /// </summary>
    /// <param name="processId">Process ID</param>
    /// <returns>Application instance if found, null otherwise</returns>
    IApplication? GetRunningApplication(int processId);

    /// <summary>
    /// Terminate a specific application
    /// </summary>
    /// <param name="applicationId">Application ID to terminate</param>
    /// <param name="force">Whether to force termination</param>
    /// <returns>True if termination successful</returns>
    Task<bool> TerminateApplicationAsync(string applicationId, bool force = false);

    /// <summary>
    /// Terminate an application by process ID
    /// </summary>
    /// <param name="processId">Process ID to terminate</param>
    /// <param name="force">Whether to force termination</param>
    /// <returns>True if termination successful</returns>
    Task<bool> TerminateApplicationAsync(int processId, bool force = false);

    /// <summary>
    /// Terminate all applications for a user session
    /// </summary>
    /// <param name="session">User session</param>
    /// <param name="force">Whether to force termination</param>
    /// <returns>Number of applications terminated</returns>
    Task<int> TerminateSessionApplicationsAsync(UserSession session, bool force = false);

    /// <summary>
    /// Check if an application has the required permissions
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="permissions">Required permissions</param>
    /// <param name="session">User session context</param>
    /// <returns>True if all permissions are granted</returns>
    Task<bool> CheckApplicationPermissionsAsync(string applicationId, IEnumerable<string> permissions, UserSession session);

    /// <summary>
    /// Get system statistics for application management
    /// </summary>
    /// <returns>Application manager statistics</returns>
    ApplicationManagerStatistics GetStatistics();

    /// <summary>
    /// Get all registered applications in the system
    /// </summary>
    /// <returns>A list of all application manifests</returns>
    IReadOnlyList<ApplicationManifest> GetAllApplications();
}
