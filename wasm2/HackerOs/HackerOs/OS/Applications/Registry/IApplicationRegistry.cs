using HackerOs.OS.Applications.Icons;
using Microsoft.AspNetCore.Components;

namespace HackerOs.OS.Applications.Registry;

/// <summary>
/// Interface for the enhanced application registry that integrates with the icon system
/// </summary>
public interface IApplicationRegistry
{
    /// <summary>
    /// Get all registered applications with their metadata
    /// </summary>
    /// <returns>Collection of application metadata</returns>
    IEnumerable<ApplicationMetadata> GetApplications();
    
    /// <summary>
    /// Get a specific application by its ID
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <returns>Application metadata if found, null otherwise</returns>
    ApplicationMetadata? GetApplicationById(string applicationId);
    
    /// <summary>
    /// Get applications by category
    /// </summary>
    /// <param name="category">Category name</param>
    /// <returns>Collection of applications in the category</returns>
    IEnumerable<ApplicationMetadata> GetApplicationsByCategory(string category);
    
    /// <summary>
    /// Search for applications by name or description
    /// </summary>
    /// <param name="searchTerm">Search term</param>
    /// <returns>Collection of matching applications</returns>
    IEnumerable<ApplicationMetadata> SearchApplications(string searchTerm);
    
    /// <summary>
    /// Get the render fragment for an application's icon
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="cssClass">Optional CSS class</param>
    /// <returns>Render fragment for the icon</returns>
    RenderFragment GetApplicationIcon(string applicationId, string? cssClass = null);
    
    /// <summary>
    /// Get recently launched applications
    /// </summary>
    /// <param name="count">Maximum number of applications to return</param>
    /// <returns>Collection of recently launched applications</returns>
    IEnumerable<ApplicationMetadata> GetRecentlyLaunchedApplications(int count = 5);
    
    /// <summary>
    /// Get frequently used applications
    /// </summary>
    /// <param name="count">Maximum number of applications to return</param>
    /// <returns>Collection of frequently used applications</returns>
    IEnumerable<ApplicationMetadata> GetFrequentlyUsedApplications(int count = 5);
    
    /// <summary>
    /// Get pinned applications
    /// </summary>
    /// <returns>Collection of pinned applications</returns>
    IEnumerable<ApplicationMetadata> GetPinnedApplications();
    
    /// <summary>
    /// Pin an application
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <returns>True if pinning was successful</returns>
    Task<bool> PinApplicationAsync(string applicationId);
    
    /// <summary>
    /// Unpin an application
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <returns>True if unpinning was successful</returns>
    Task<bool> UnpinApplicationAsync(string applicationId);
    
    /// <summary>
    /// Record an application launch
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <returns>Task representing the operation</returns>
    Task RecordApplicationLaunchAsync(string applicationId);
    
    /// <summary>
    /// Refresh the application registry
    /// </summary>
    /// <returns>Number of applications discovered/updated</returns>
    Task<int> RefreshApplicationsAsync();
    
    /// <summary>
    /// Get applications by type
    /// </summary>
    /// <param name="applicationType">Application type</param>
    /// <returns>Collection of applications of the specified type</returns>
    IEnumerable<ApplicationMetadata> GetApplicationsByType(ApplicationType applicationType);
    
    /// <summary>
    /// Get all windowed applications
    /// </summary>
    /// <returns>Collection of windowed applications</returns>
    IEnumerable<ApplicationMetadata> GetWindowedApplications();
    
    /// <summary>
    /// Get all service applications
    /// </summary>
    /// <returns>Collection of service applications</returns>
    IEnumerable<ApplicationMetadata> GetServiceApplications();
    
    /// <summary>
    /// Get all command-line applications
    /// </summary>
    /// <returns>Collection of command-line applications</returns>
    IEnumerable<ApplicationMetadata> GetCommandLineApplications();
    
    /// <summary>
    /// Get all system applications
    /// </summary>
    /// <returns>Collection of system applications</returns>
    IEnumerable<ApplicationMetadata> GetSystemApplications();
}
