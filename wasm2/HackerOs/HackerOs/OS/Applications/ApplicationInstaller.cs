using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications;

/// <summary>
/// Interface for installing and uninstalling applications
/// </summary>
public interface IApplicationInstaller
{
    /// <summary>
    /// Event fired when an application is installed
    /// </summary>
    event EventHandler<ApplicationInstalledEventArgs>? ApplicationInstalled;
    
    /// <summary>
    /// Event fired when an application is uninstalled
    /// </summary>
    event EventHandler<ApplicationUninstalledEventArgs>? ApplicationUninstalled;
    
    /// <summary>
    /// Install an application from a package
    /// </summary>
    /// <param name="packagePath">Path to the application package</param>
    /// <param name="userSession">User session performing the installation</param>
    /// <returns>The installed application manifest, or null if installation failed</returns>
    Task<ApplicationManifest?> InstallApplicationAsync(string packagePath, UserSession userSession);
    
    /// <summary>
    /// Install an application directly from a manifest
    /// </summary>
    /// <param name="manifest">Application manifest</param>
    /// <param name="sourceFiles">Dictionary of source files (relative path -> content)</param>
    /// <param name="userSession">User session performing the installation</param>
    /// <returns>True if installation was successful</returns>
    Task<bool> InstallApplicationAsync(ApplicationManifest manifest, Dictionary<string, string> sourceFiles, UserSession userSession);
    
    /// <summary>
    /// Uninstall an application
    /// </summary>
    /// <param name="applicationId">Application ID to uninstall</param>
    /// <param name="userSession">User session performing the uninstallation</param>
    /// <param name="keepUserData">Whether to keep user data</param>
    /// <returns>True if uninstallation was successful</returns>
    Task<bool> UninstallApplicationAsync(string applicationId, UserSession userSession, bool keepUserData = true);
    
    /// <summary>
    /// Get installed applications for a specific user
    /// </summary>
    /// <param name="username">Username to filter by</param>
    /// <returns>List of applications installed by the user</returns>
    Task<IEnumerable<ApplicationManifest>> GetUserInstalledApplicationsAsync(string username);
}

/// <summary>
/// Event arguments for application installation
/// </summary>
public class ApplicationInstalledEventArgs : EventArgs
{
    /// <summary>
    /// Installed application manifest
    /// </summary>
    public ApplicationManifest Manifest { get; }
    
    /// <summary>
    /// User who performed the installation
    /// </summary>
    public string Username { get; }
    
    /// <summary>
    /// Installation timestamp
    /// </summary>
    public DateTime Timestamp { get; }
    
    /// <summary>
    /// Creates new event args for application installation
    /// </summary>
    public ApplicationInstalledEventArgs(ApplicationManifest manifest, string username)
    {
        Manifest = manifest;
        Username = username;
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// Event arguments for application uninstallation
/// </summary>
public class ApplicationUninstalledEventArgs : EventArgs
{
    /// <summary>
    /// Uninstalled application ID
    /// </summary>
    public string ApplicationId { get; }
    
    /// <summary>
    /// User who performed the uninstallation
    /// </summary>
    public string Username { get; }
    
    /// <summary>
    /// Uninstallation timestamp
    /// </summary>
    public DateTime Timestamp { get; }
    
    /// <summary>
    /// Whether user data was kept
    /// </summary>
    public bool KeptUserData { get; }
    
    /// <summary>
    /// Creates new event args for application uninstallation
    /// </summary>
    public ApplicationUninstalledEventArgs(string applicationId, string username, bool keptUserData)
    {
        ApplicationId = applicationId;
        Username = username;
        Timestamp = DateTime.Now;
        KeptUserData = keptUserData;
    }
}

/// <summary>
/// Implementation of the application installer
/// </summary>
public class ApplicationInstaller : IApplicationInstaller
{
    private readonly ILogger<ApplicationInstaller> _logger;
    private readonly IApplicationManager _applicationManager;
    private readonly IFileTypeRegistry _fileTypeRegistry;
    private readonly IVirtualFileSystem _fileSystem;
    private readonly IUserManager _userManager;
    
    // Installation directories
    private const string SYSTEM_APPS_DIR = "/usr/share/applications";
    private const string USER_APPS_DIR = "/home/{0}/Applications";
    private const string APP_DATA_DIR = "/usr/share/hackeros/applications/{0}";
    private const string USER_APP_DATA_DIR = "/home/{0}/.local/share/applications/{1}";
    private const string INSTALL_LOG_FILE = "/var/log/hackeros/installations.json";
    
    /// <inheritdoc />
    public event EventHandler<ApplicationInstalledEventArgs>? ApplicationInstalled;
    
    /// <inheritdoc />
    public event EventHandler<ApplicationUninstalledEventArgs>? ApplicationUninstalled;
    
    /// <summary>
    /// Creates a new instance of the ApplicationInstaller
    /// </summary>
    public ApplicationInstaller(
        ILogger<ApplicationInstaller> logger,
        IApplicationManager applicationManager,
        IFileTypeRegistry fileTypeRegistry,
        IVirtualFileSystem fileSystem,
        IUserManager userManager)
    {
        _logger = logger;
        _applicationManager = applicationManager;
        _fileTypeRegistry = fileTypeRegistry;
        _fileSystem = fileSystem;
        _userManager = userManager;
        
        // Initialize directories
        _ = InitializeDirectoriesAsync();
    }
    
    /// <summary>
    /// Initialize required directories
    /// </summary>
    private async Task InitializeDirectoriesAsync()
    {
        try
        {
            // Ensure system app directory exists
            if (!await _fileSystem.DirectoryExistsAsync(SYSTEM_APPS_DIR))
            {
                await _fileSystem.CreateDirectoryAsync(SYSTEM_APPS_DIR, true);
                _logger.LogInformation("Created system applications directory: {Dir}", SYSTEM_APPS_DIR);
            }
            
            // Ensure log directory exists
            string logDir = Path.GetDirectoryName(INSTALL_LOG_FILE) ?? "/var/log/hackeros";
            if (!await _fileSystem.DirectoryExistsAsync(logDir))
            {
                await _fileSystem.CreateDirectoryAsync(logDir, true);
                _logger.LogInformation("Created installation log directory: {Dir}", logDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize application installer directories");
        }
    }
    
    /// <inheritdoc />
    public async Task<ApplicationManifest?> InstallApplicationAsync(string packagePath, UserSession userSession)
    {
        try
        {
            // Check if package exists
            if (!await _fileSystem.FileExistsAsync(packagePath))
            {
                _logger.LogWarning("Package does not exist: {Path}", packagePath);
                return null;
            }
            
            // Check package extension
            if (!packagePath.EndsWith(".hapkg") && !packagePath.EndsWith(".zip"))
            {
                _logger.LogWarning("Unsupported package format: {Path}", packagePath);
                return null;
            }
            
            // Read package content
            var packageContent = await _fileSystem.ReadAllBytesAsync(packagePath);
            
            // Extract package (simulated for now)
            var extractedFiles = await ExtractPackageAsync(packageContent);
            if (!extractedFiles.TryGetValue("manifest.json", out var manifestContent))
            {
                _logger.LogWarning("No manifest.json found in package: {Path}", packagePath);
                return null;
            }
            
            // Parse manifest
            var manifest = JsonSerializer.Deserialize<ApplicationManifest>(manifestContent);
            if (manifest == null)
            {
                _logger.LogWarning("Failed to parse manifest from package: {Path}", packagePath);
                return null;
            }
            
            // Remove manifest from files to install
            extractedFiles.Remove("manifest.json");
            
            // Validate manifest
            var validationResult = manifest.Validate();
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Invalid manifest in package {Path}: {Errors}", 
                    packagePath, string.Join(", ", validationResult.Errors));
                return null;
            }
            
            // Install the application
            bool installed = await InstallApplicationAsync(manifest, extractedFiles, userSession);
            if (!installed)
            {
                _logger.LogWarning("Failed to install application from package: {Path}", packagePath);
                return null;
            }
            
            _logger.LogInformation("Successfully installed application {AppId} from package {Path}", 
                manifest.Id, packagePath);
            
            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install application from package: {Path}", packagePath);
            return null;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> InstallApplicationAsync(ApplicationManifest manifest, Dictionary<string, string> sourceFiles, UserSession userSession)
    {
        try
        {
            // Validate manifest
            var validationResult = manifest.Validate();
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Invalid manifest for application {AppId}: {Errors}", 
                    manifest.Id, string.Join(", ", validationResult.Errors));
                return false;
            }
            
            // Check for existing application
            var existingApp = _applicationManager.GetApplication(manifest.Id);
            if (existingApp != null)
            {
                // Could handle update instead of failing
                _logger.LogWarning("Application {AppId} is already installed", manifest.Id);
                return false;
            }
            
            // Determine installation paths
            string appDir;
            string manifestDir;
            bool isSystemInstall = userSession.User.Username == "root" || 
                (await _userManager.IsUserInGroupAsync(userSession.User.Username, "admin"));
            
            if (isSystemInstall)
            {
                // System-wide installation
                appDir = string.Format(APP_DATA_DIR, manifest.Id);
                manifestDir = SYSTEM_APPS_DIR;
                manifest.IsSystemApplication = true;
            }
            else
            {
                // User-specific installation
                appDir = string.Format(USER_APP_DATA_DIR, userSession.User.Username, manifest.Id);
                manifestDir = string.Format(USER_APPS_DIR, userSession.User.Username);
                manifest.IsSystemApplication = false;
            }
            
            // Ensure directories exist
            if (!await _fileSystem.DirectoryExistsAsync(appDir))
            {
                await _fileSystem.CreateDirectoryAsync(appDir, true);
            }
            
            if (!await _fileSystem.DirectoryExistsAsync(manifestDir))
            {
                await _fileSystem.CreateDirectoryAsync(manifestDir, true);
            }
            
            // Install application files
            foreach (var entry in sourceFiles)
            {
                string destPath = Path.Combine(appDir, entry.Key);
                string? destDir = Path.GetDirectoryName(destPath);
                
                // Create parent directories if needed
                if (!string.IsNullOrEmpty(destDir) && !await _fileSystem.DirectoryExistsAsync(destDir))
                {
                    await _fileSystem.CreateDirectoryAsync(destDir, true);
                }
                
                // Write the file
                await _fileSystem.WriteAllTextAsync(destPath, entry.Value);
            }
            
            // Save the manifest
            string manifestPath = Path.Combine(manifestDir, $"{manifest.Id}.app.json");
            string json = JsonSerializer.Serialize(manifest);
            await _fileSystem.WriteAllTextAsync(manifestPath, json);
            
            // Register the application
            bool registered = await _applicationManager.RegisterApplicationAsync(manifest);
            if (!registered)
            {
                _logger.LogError("Failed to register application {AppId} with application manager", manifest.Id);
                return false;
            }
            
            // Register file type associations
            await RegisterFileTypeAssociationsAsync(manifest);
            
            // Log the installation
            await LogInstallationAsync(manifest, userSession.User.Username, isSystemInstall);
            
            // Raise event
            ApplicationInstalled?.Invoke(this, new ApplicationInstalledEventArgs(manifest, userSession.User.Username));
            
            _logger.LogInformation("Successfully installed application {AppId} by user {Username}", 
                manifest.Id, userSession.User.Username);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install application {AppId}", manifest.Id);
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> UninstallApplicationAsync(string applicationId, UserSession userSession, bool keepUserData = true)
    {
        try
        {
            // Get application manifest
            var app = _applicationManager.GetApplication(applicationId);
            if (app == null)
            {
                _logger.LogWarning("Cannot uninstall: Application {AppId} not found", applicationId);
                return false;
            }
            
            // Check permissions
            bool canUninstall = await CheckUninstallPermissionsAsync(app, userSession);
            if (!canUninstall)
            {
                _logger.LogWarning("User {Username} does not have permission to uninstall application {AppId}", 
                    userSession.User.Username, applicationId);
                return false;
            }
            
            // Stop any running instances
            await _applicationManager.TerminateApplicationAsync(applicationId, true);
            
            // Unregister the application
            bool unregistered = await _applicationManager.UnregisterApplicationAsync(applicationId);
            if (!unregistered)
            {
                _logger.LogError("Failed to unregister application {AppId}", applicationId);
                return false;
            }
            
            // Determine installation paths
            string appDir;
            string manifestDir;
            string manifestPath;
            
            if (app.IsSystemApplication)
            {
                // System-wide installation
                appDir = string.Format(APP_DATA_DIR, app.Id);
                manifestDir = SYSTEM_APPS_DIR;
                manifestPath = Path.Combine(manifestDir, $"{app.Id}.app.json");
            }
            else
            {
                // Find the user who installed it
                var installInfo = await GetInstallationInfoAsync(applicationId);
                string username = installInfo?.Username ?? userSession.User.Username;
                
                // User-specific installation
                appDir = string.Format(USER_APP_DATA_DIR, username, app.Id);
                manifestDir = string.Format(USER_APPS_DIR, username);
                manifestPath = Path.Combine(manifestDir, $"{app.Id}.app.json");
            }
            
            // Delete application files
            if (await _fileSystem.DirectoryExistsAsync(appDir))
            {
                await _fileSystem.DeleteDirectoryAsync(appDir, true);
            }
            
            // Delete manifest file
            if (await _fileSystem.FileExistsAsync(manifestPath))
            {
                await _fileSystem.DeleteFileAsync(manifestPath);
            }
            
            // Delete user data if requested
            if (!keepUserData)
            {
                // For each user
                var users = await _userManager.GetAllUsersAsync();
                foreach (var user in users)
                {
                    string userDataDir = string.Format(USER_APP_DATA_DIR, user.Username, app.Id);
                    if (await _fileSystem.DirectoryExistsAsync(userDataDir))
                    {
                        await _fileSystem.DeleteDirectoryAsync(userDataDir, true);
                    }
                }
            }
            
            // Log the uninstallation
            await LogUninstallationAsync(applicationId, userSession.User.Username, keepUserData);
            
            // Raise event
            ApplicationUninstalled?.Invoke(this, new ApplicationUninstalledEventArgs(applicationId, userSession.User.Username, keepUserData));
            
            _logger.LogInformation("Successfully uninstalled application {AppId} by user {Username}", 
                applicationId, userSession.User.Username);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to uninstall application {AppId}", applicationId);
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<IEnumerable<ApplicationManifest>> GetUserInstalledApplicationsAsync(string username)
    {
        var applications = new List<ApplicationManifest>();
        
        try
        {
            // Get user application manifest directory
            string userAppsDir = string.Format(USER_APPS_DIR, username);
            
            // Check if directory exists
            if (!await _fileSystem.DirectoryExistsAsync(userAppsDir))
            {
                return applications;
            }
            
            // Find all manifest files
            var manifestFiles = await _fileSystem.GetFilesAsync(userAppsDir, "*.app.json");
            
            // Load each manifest
            foreach (var manifestFile in manifestFiles)
            {
                try
                {
                    string json = await _fileSystem.ReadAllTextAsync(manifestFile);
                    var manifest = JsonSerializer.Deserialize<ApplicationManifest>(json);
                    
                    if (manifest != null)
                    {
                        applications.Add(manifest);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing manifest file: {Path}", manifestFile);
                }
            }
            
            return applications;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get installed applications for user {Username}", username);
            return applications;
        }
    }
    
    /// <summary>
    /// Simulates extracting a package file
    /// </summary>
    private Task<Dictionary<string, string>> ExtractPackageAsync(byte[] packageContent)
    {
        // In a real implementation, this would extract the package contents
        // For this simplified version, we'll simulate it with a simple manifest and files
        
        var result = new Dictionary<string, string>();
        
        // Create a simple manifest
        var manifest = new ApplicationManifest
        {
            Id = "com.example.demoapp",
            Name = "Demo Application",
            Description = "This is a demo application",
            Version = "1.0.0",
            EntryPoint = "DemoApp.dll",
            Author = "HackerOS User"
        };
        
        result["manifest.json"] = JsonSerializer.Serialize(manifest);
        result["DemoApp.dll"] = "// This would be the compiled application code";
        result["README.md"] = "# Demo Application\nThis is a sample application for testing installation.";
        
        return Task.FromResult(result);
    }
    
    /// <summary>
    /// Register file type associations for an application
    /// </summary>
    private async Task RegisterFileTypeAssociationsAsync(ApplicationManifest manifest)
    {
        try
        {
            foreach (var fileType in manifest.SupportedFileTypes)
            {
                // Create file type registration
                var registration = new FileTypeRegistration
                {
                    FileExtension = fileType,
                    ApplicationId = manifest.Id,
                    ApplicationName = manifest.Name,
                    Description = $"{manifest.Name} file",
                    IconPath = manifest.IconPath
                };
                
                // Register with the file type registry
                await _fileTypeRegistry.RegisterFileTypeAsync(registration);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register file type associations for application {AppId}", manifest.Id);
        }
    }
    
    /// <summary>
    /// Check if a user has permission to uninstall an application
    /// </summary>
    private async Task<bool> CheckUninstallPermissionsAsync(ApplicationManifest app, UserSession session)
    {
        // Root can uninstall anything
        if (session.User.Username == "root")
        {
            return true;
        }
        
        // Admin users can uninstall any application
        if (await _userManager.IsUserInGroupAsync(session.User.Username, "admin"))
        {
            return true;
        }
        
        // System applications can only be uninstalled by root or admin
        if (app.IsSystemApplication)
        {
            return false;
        }
        
        // Users can uninstall their own applications
        var installInfo = await GetInstallationInfoAsync(app.Id);
        return installInfo?.Username == session.User.Username;
    }
    
    /// <summary>
    /// Log an application installation
    /// </summary>
    private async Task LogInstallationAsync(ApplicationManifest manifest, string username, bool isSystemInstall)
    {
        try
        {
            // Installation record
            var record = new ApplicationInstallationRecord
            {
                ApplicationId = manifest.Id,
                Version = manifest.Version,
                InstallDate = DateTime.Now,
                Username = username,
                IsSystemInstall = isSystemInstall
            };
            
            // Get existing records
            List<ApplicationInstallationRecord> records;
            
            if (await _fileSystem.FileExistsAsync(INSTALL_LOG_FILE))
            {
                string content = await _fileSystem.ReadAllTextAsync(INSTALL_LOG_FILE);
                records = JsonSerializer.Deserialize<List<ApplicationInstallationRecord>>(content) ?? new List<ApplicationInstallationRecord>();
            }
            else
            {
                records = new List<ApplicationInstallationRecord>();
            }
            
            // Add new record
            records.Add(record);
            
            // Save records
            string json = JsonSerializer.Serialize(records);
            await _fileSystem.WriteAllTextAsync(INSTALL_LOG_FILE, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log installation for application {AppId}", manifest.Id);
        }
    }
    
    /// <summary>
    /// Log an application uninstallation
    /// </summary>
    private async Task LogUninstallationAsync(string applicationId, string username, bool keptUserData)
    {
        try
        {
            // Uninstallation record
            var record = new ApplicationUninstallationRecord
            {
                ApplicationId = applicationId,
                UninstallDate = DateTime.Now,
                Username = username,
                KeptUserData = keptUserData
            };
            
            // Get existing records
            List<object> records;
            
            if (await _fileSystem.FileExistsAsync(INSTALL_LOG_FILE))
            {
                string content = await _fileSystem.ReadAllTextAsync(INSTALL_LOG_FILE);
                records = JsonSerializer.Deserialize<List<object>>(content) ?? new List<object>();
            }
            else
            {
                records = new List<object>();
            }
            
            // Add new record
            records.Add(record);
            
            // Save records
            string json = JsonSerializer.Serialize(records);
            await _fileSystem.WriteAllTextAsync(INSTALL_LOG_FILE, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log uninstallation for application {AppId}", applicationId);
        }
    }
    
    /// <summary>
    /// Get installation info for an application
    /// </summary>
    private async Task<ApplicationInstallationRecord?> GetInstallationInfoAsync(string applicationId)
    {
        try
        {
            // Check if log file exists
            if (!await _fileSystem.FileExistsAsync(INSTALL_LOG_FILE))
            {
                return null;
            }
            
            // Read log file
            string content = await _fileSystem.ReadAllTextAsync(INSTALL_LOG_FILE);
            var records = JsonSerializer.Deserialize<List<ApplicationInstallationRecord>>(content);
            
            // Find installation record for the application
            return records?.FirstOrDefault(r => r.ApplicationId == applicationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get installation info for application {AppId}", applicationId);
            return null;
        }
    }
}

/// <summary>
/// Record of an application installation
/// </summary>
public class ApplicationInstallationRecord
{
    /// <summary>
    /// Application ID
    /// </summary>
    public string ApplicationId { get; set; } = string.Empty;
    
    /// <summary>
    /// Application version
    /// </summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// Installation timestamp
    /// </summary>
    public DateTime InstallDate { get; set; }
    
    /// <summary>
    /// User who performed the installation
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this is a system-wide installation
    /// </summary>
    public bool IsSystemInstall { get; set; }
}

/// <summary>
/// Record of an application uninstallation
/// </summary>
public class ApplicationUninstallationRecord
{
    /// <summary>
    /// Application ID
    /// </summary>
    public string ApplicationId { get; set; } = string.Empty;
    
    /// <summary>
    /// Uninstallation timestamp
    /// </summary>
    public DateTime UninstallDate { get; set; }
    
    /// <summary>
    /// User who performed the uninstallation
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether user data was kept
    /// </summary>
    public bool KeptUserData { get; set; }
}
