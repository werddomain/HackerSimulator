using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using HackerOs.OS.IO.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HackerOs.OS.Settings;

namespace HackerOs.OS.Applications;

/// <summary>
/// Interface for application version management and updates
/// </summary>
public interface IApplicationUpdater
{
    /// <summary>
    /// Checks if updates are available for an application
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <returns>True if updates are available</returns>
    Task<bool> CheckForUpdatesAsync(string applicationId);
    
    /// <summary>
    /// Gets available updates for all applications
    /// </summary>
    /// <returns>Dictionary of application IDs and available versions</returns>
    Task<IDictionary<string, string>> GetAvailableUpdatesAsync();
    
    /// <summary>
    /// Updates an application to the specified version
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="version">Target version (null for latest)</param>
    /// <returns>True if update was successful</returns>
    Task<bool> UpdateApplicationAsync(string applicationId, string? version = null);
    
    /// <summary>
    /// Gets the update history for an application
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <returns>List of update history entries</returns>
    Task<IEnumerable<ApplicationUpdateRecord>> GetUpdateHistoryAsync(string applicationId);
}

/// <summary>
/// Record of an application update
/// </summary>
public class ApplicationUpdateRecord
{
    /// <summary>
    /// Application ID
    /// </summary>
    public string ApplicationId { get; set; } = string.Empty;
    
    /// <summary>
    /// Previous version
    /// </summary>
    public string PreviousVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// New version
    /// </summary>
    public string NewVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Update timestamp
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Whether the update was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// User who performed the update
    /// </summary>
    public string User { get; set; } = string.Empty;
}

/// <summary>
/// Implementation of the application updater
/// </summary>
public class ApplicationUpdater : IApplicationUpdater
{
    private readonly ILogger<ApplicationUpdater> _logger;
    private readonly IApplicationManager _applicationManager;
    private readonly IVirtualFileSystem _fileSystem;
    
    // Update repository information
    private const string UPDATE_REPOSITORY = "/var/lib/hackeros/updates";
    private const string UPDATE_HISTORY = "/var/log/hackeros/application-updates.log";
    
    /// <summary>
    /// Creates a new instance of the ApplicationUpdater
    /// </summary>
    public ApplicationUpdater(
        ILogger<ApplicationUpdater> logger,
        IApplicationManager applicationManager,
        IVirtualFileSystem fileSystem)
    {
        _logger = logger;
        _applicationManager = applicationManager;
        _fileSystem = fileSystem;
        
        // Initialize directories
        _ = InitializeDirectoriesAsync();
    }
    
    /// <summary>
    /// Initializes the required directories
    /// </summary>
    private async Task InitializeDirectoriesAsync()
    {
        try
        {
            // Ensure update repository directory exists
            if (!await _fileSystem.DirectoryExistsAsync(UPDATE_REPOSITORY, UserManager.SystemUser))
            {
                await _fileSystem.CreateDirectoryAsync(UPDATE_REPOSITORY, UserManager.SystemUser);
                _logger.LogInformation("Created update repository directory: {Dir}", UPDATE_REPOSITORY);
            }
            
            // Ensure update history directory exists
            string historyDir = System.IO.Path.GetDirectoryName(UPDATE_HISTORY) ?? "/var/log/hackeros";
            if (!await _fileSystem.DirectoryExistsAsync(historyDir, UserManager.SystemUser))
            {
                await _fileSystem.CreateDirectoryAsync(historyDir, UserManager.SystemUser);
                _logger.LogInformation("Created update history directory: {Dir}", historyDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize application updater directories");
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> CheckForUpdatesAsync(string applicationId)
    {
        try
        {
            // Get current application
            var app = _applicationManager.GetApplication(applicationId);
            if (app == null)
            {
                _logger.LogWarning("Cannot check for updates: Application {AppId} not found", applicationId);
                return false;
            }
            
            // Get available versions
            var availableVersions = await GetAvailableVersionsAsync(applicationId);
            if (availableVersions.Count == 0)
            {
                return false;
            }
            
            // Parse current version
            if (!Version.TryParse(app.Version, out var currentVersion))
            {
                _logger.LogWarning("Cannot parse current version {Version} for application {AppId}", app.Version, applicationId);
                return false;
            }
            
            // Check if any available version is newer
            foreach (var version in availableVersions)
            {
                if (!Version.TryParse(version, out var availableVersion))
                {
                    continue;
                }
                
                if (availableVersion > currentVersion)
                {
                    return true;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates for application {AppId}", applicationId);
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<IDictionary<string, string>> GetAvailableUpdatesAsync()
    {
        var updates = new Dictionary<string, string>();
        
        try
        {
            // Get all applications
            var apps = _applicationManager.GetAvailableApplications();
            
            // Check each application for updates
            foreach (var app in apps)
            {
                // Parse current version
                if (!Version.TryParse(app.Version, out var currentVersion))
                {
                    continue;
                }
                
                // Get available versions
                var availableVersions = await GetAvailableVersionsAsync(app.Id);
                
                // Find the latest version
                Version? latestVersion = null;
                string? latestVersionString = null;
                
                foreach (var version in availableVersions)
                {
                    if (!Version.TryParse(version, out var availableVersion))
                    {
                        continue;
                    }
                    
                    if (availableVersion > currentVersion && (latestVersion == null || availableVersion > latestVersion))
                    {
                        latestVersion = availableVersion;
                        latestVersionString = version;
                    }
                }
                
                // Add to updates if newer version is available
                if (latestVersion != null && latestVersionString != null)
                {
                    updates[app.Id] = latestVersionString;
                }
            }
            
            return updates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available updates for applications");
            return updates;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> UpdateApplicationAsync(string applicationId, string? version = null)
    {
        try
        {
            // Get current application
            var app = _applicationManager.GetApplication(applicationId);
            if (app == null)
            {
                _logger.LogWarning("Cannot update: Application {AppId} not found", applicationId);
                return false;
            }
            
            // Get available versions
            var availableVersions = await GetAvailableVersionsAsync(applicationId);
            if (availableVersions.Count == 0)
            {
                _logger.LogWarning("No updates available for application {AppId}", applicationId);
                return false;
            }
            
            // Determine target version
            string targetVersion;
            
            if (version != null)
            {
                // Use specified version
                if (!availableVersions.Contains(version))
                {
                    _logger.LogWarning("Specified version {Version} not available for application {AppId}", version, applicationId);
                    return false;
                }
                
                targetVersion = version;
            }
            else
            {
                // Use latest version
                Version? latestVersion = null;
                string? latestVersionString = null;
                
                foreach (var v in availableVersions)
                {
                    if (!Version.TryParse(v, out var parsedVersion))
                    {
                        continue;
                    }
                    
                    if (latestVersion == null || parsedVersion > latestVersion)
                    {
                        latestVersion = parsedVersion;
                        latestVersionString = v;
                    }
                }
                
                if (latestVersionString == null)
                {
                    _logger.LogWarning("No valid versions available for application {AppId}", applicationId);
                    return false;
                }
                
                targetVersion = latestVersionString;
            }
            
            // Ensure the target version is newer than current version
            if (Version.TryParse(app.Version, out var currentVersion) && 
                Version.TryParse(targetVersion, out var parsedTargetVersion))
            {
                if (parsedTargetVersion <= currentVersion)
                {
                    _logger.LogWarning("Target version {TargetVersion} is not newer than current version {CurrentVersion} for application {AppId}",
                        targetVersion, app.Version, applicationId);
                    return false;
                }
            }
            
            // In a real implementation, we would:
            // 1. Download the update package
            // 2. Verify package integrity
            // 3. Stop running instances of the application
            // 4. Install the update
            // 5. Update the application manifest
            // 6. Restart application instances if needed
            
            // For this simplified implementation, we'll just update the version
            var updatedManifest = new ApplicationManifest
            {
                Id = app.Id,
                Name = app.Name,
                Description = app.Description,
                Version = targetVersion,
                EntryPoint = app.EntryPoint,
                // Copy other properties...
                Type = app.Type,
                Author = app.Author,
                IconPath = app.IconPath,
                WorkingDirectory = app.WorkingDirectory,
                AllowMultipleInstances = app.AllowMultipleInstances,
                IsSystemApplication = app.IsSystemApplication,
                AutoStart = app.AutoStart
            };
            
            foreach (var perm in app.RequiredPermissions)
            {
                updatedManifest.RequiredPermissions.Add(perm);
            }
            
            foreach (var arg in app.DefaultArguments)
            {
                updatedManifest.DefaultArguments.Add(arg);
            }
            
            foreach (var cat in app.Categories)
            {
                updatedManifest.Categories.Add(cat);
            }
            
            foreach (var ft in app.SupportedFileTypes)
            {
                updatedManifest.SupportedFileTypes.Add(ft);
            }
            
            foreach (var kvp in app.EnvironmentVariables)
            {
                updatedManifest.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
            
            // Register the updated manifest
            bool success = await _applicationManager.RegisterApplicationAsync(updatedManifest);
            
            // Log the update
            await LogUpdateAsync(applicationId, app.Version, targetVersion, success, "system");
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating application {AppId}", applicationId);
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<IEnumerable<ApplicationUpdateRecord>> GetUpdateHistoryAsync(string applicationId)
    {
        var history = new List<ApplicationUpdateRecord>();
        
        try
        {
            // Check if history file exists
            if (!await _fileSystem.FileExistsAsync(UPDATE_HISTORY, UserManager.SystemUser))
            {
                return history;
            }
            
            // Read history file
            string? content = await _fileSystem.ReadAllTextAsync(UPDATE_HISTORY, UserManager.SystemUser);
            var allRecords = (string.IsNullOrEmpty(content) ? null : JsonSerializer.Deserialize<List<ApplicationUpdateRecord>>(content)) ?? new List<ApplicationUpdateRecord>();
            
            // Filter by application ID
            history = allRecords.Where(r => r.ApplicationId == applicationId).ToList();
            
            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting update history for application {AppId}", applicationId);
            return history;
        }
    }
    
    /// <summary>
    /// Gets available versions for an application
    /// </summary>
    private async Task<List<string>> GetAvailableVersionsAsync(string applicationId)
    {
        var versions = new List<string>();
        
        try
        {
            // Check if application directory exists
            string appUpdateDir = $"{UPDATE_REPOSITORY}/{applicationId}";
            if (!await _fileSystem.DirectoryExistsAsync(appUpdateDir, UserManager.SystemUser))
            {
                return versions;
            }
            
            // Get version directories
            var directories = await HackerOs.OS.IO.Utilities.Directory.GetDirectoriesAsync(appUpdateDir, _fileSystem);
            versions.AddRange(directories.Select(System.IO.Path.GetFileName));
            
            return versions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available versions for application {AppId}", applicationId);
            return versions;
        }
    }
    
    /// <summary>
    /// Logs an application update
    /// </summary>
    private async Task LogUpdateAsync(string applicationId, string previousVersion, string newVersion, bool success, string user)
    {
        try
        {
            // Create a record
            var record = new ApplicationUpdateRecord
            {
                ApplicationId = applicationId,
                PreviousVersion = previousVersion,
                NewVersion = newVersion,
                Timestamp = DateTime.Now,
                Success = success,
                User = user
            };
            
            // Get existing records
            List<ApplicationUpdateRecord> records;
            
            if (await _fileSystem.FileExistsAsync(UPDATE_HISTORY, UserManager.SystemUser))
            {
                string content = await _fileSystem.ReadAllTextAsync(UPDATE_HISTORY, UserManager.SystemUser);
                records = JsonSerializer.Deserialize<List<ApplicationUpdateRecord>>(content) ?? new List<ApplicationUpdateRecord>();
            }
            else
            {
                records = new List<ApplicationUpdateRecord>();
            }
            
            // Add new record
            records.Add(record);
            
            // Save records
            string json = JsonSerializer.Serialize(records);
            await _fileSystem.WriteAllTextAsync(UPDATE_HISTORY, json, UserManager.SystemUser);
            
            _logger.LogInformation("Logged update for application {AppId} from {PrevVersion} to {NewVersion}",
                applicationId, previousVersion, newVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log update for application {AppId}", applicationId);
        }
    }
}
