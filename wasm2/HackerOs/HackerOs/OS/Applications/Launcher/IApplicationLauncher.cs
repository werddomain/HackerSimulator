using HackerOs.OS.Applications.Registry;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.Launcher;

/// <summary>
/// Interface for application launcher service
/// </summary>
public interface IApplicationLauncher
{
    /// <summary>
    /// Launch an application by its ID
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="session">Optional user session (uses current session if null)</param>
    /// <param name="args">Optional launch arguments</param>
    /// <returns>True if launch was successful</returns>
    Task<bool> LaunchApplicationAsync(string applicationId, UserSession? session = null, string[]? args = null);
    
    /// <summary>
    /// Launch an application by its metadata
    /// </summary>
    /// <param name="metadata">Application metadata</param>
    /// <param name="session">Optional user session (uses current session if null)</param>
    /// <param name="args">Optional launch arguments</param>
    /// <returns>True if launch was successful</returns>
    Task<bool> LaunchApplicationAsync(ApplicationMetadata metadata, UserSession? session = null, string[]? args = null);
    
    /// <summary>
    /// Close an application by its ID
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="force">Whether to force closure</param>
    /// <returns>True if closure was successful</returns>
    Task<bool> CloseApplicationAsync(string applicationId, bool force = false);
    
    /// <summary>
    /// Get a list of currently running applications
    /// </summary>
    /// <returns>List of application IDs that are currently running</returns>
    IEnumerable<string> GetRunningApplications();
    
    /// <summary>
    /// Check if an application is running
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <returns>True if the application is running</returns>
    bool IsApplicationRunning(string applicationId);
}
