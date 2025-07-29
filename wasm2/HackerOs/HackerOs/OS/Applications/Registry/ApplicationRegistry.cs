using HackerOs.OS.Applications.Attributes;
using HackerOs.OS.Applications.Icons;
using HackerOs.OS.IO;
using HackerOs.OS.User;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace HackerOs.OS.Applications.Registry;

/// <summary>
/// Implementation of the application registry that integrates with the icon system
/// </summary>
public class ApplicationRegistry : IApplicationRegistry
{
    private readonly ILogger<ApplicationRegistry> _logger;
    private readonly IApplicationManager _applicationManager;
    private readonly IApplicationDiscoveryService _discoveryService;
    private readonly IconFactory _iconFactory;
    private readonly IMemoryCache _cache;
    private readonly IVirtualFileSystem _fileSystem;
    private readonly IUserManager _userManager;
    
    // In-memory cache of application metadata
    private Dictionary<string, ApplicationMetadata> _applications = new();
    
    // Paths for application data
    private const string APP_STATS_DIR = "/var/lib/applications";
    private const string USER_APP_SETTINGS_DIR = "/.config/applications";
    
    /// <summary>
    /// Create a new ApplicationRegistry
    /// </summary>
    public ApplicationRegistry(
        ILogger<ApplicationRegistry> logger,
        IApplicationManager applicationManager,
        IApplicationDiscoveryService discoveryService,
        IconFactory iconFactory,
        IMemoryCache cache,
        IVirtualFileSystem fileSystem,
        IUserManager userManager)
    {
        _logger = logger;
        _applicationManager = applicationManager;
        _discoveryService = discoveryService;
        _iconFactory = iconFactory;
        _cache = cache;
        _fileSystem = fileSystem;
        _userManager = userManager;
        
        // Hook up to application events
        _applicationManager.ApplicationLaunched += OnApplicationLaunched;
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationMetadata> GetApplications()
    {
        // If no applications are loaded, load them
        if (_applications.Count == 0)
        {
            LoadApplications();
        }
        
        return _applications.Values;
    }
    
    /// <inheritdoc />
    public ApplicationMetadata? GetApplicationById(string applicationId)
    {
        // If no applications are loaded, load them
        if (_applications.Count == 0)
        {
            LoadApplications();
        }
        
        return _applications.TryGetValue(applicationId, out var metadata) ? metadata : null;
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationMetadata> GetApplicationsByCategory(string category)
    {
        return GetApplications().Where(a => a.Categories.Contains(category, StringComparer.OrdinalIgnoreCase));
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationMetadata> SearchApplications(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Array.Empty<ApplicationMetadata>();
            
        searchTerm = searchTerm.ToLowerInvariant();
        
        return GetApplications().Where(a =>
            a.Name.ToLowerInvariant().Contains(searchTerm) ||
            a.Description.ToLowerInvariant().Contains(searchTerm) ||
            a.Id.ToLowerInvariant().Contains(searchTerm));
    }
    
    /// <inheritdoc />
    public RenderFragment GetApplicationIcon(string applicationId, string? cssClass = null)
    {
        var app = GetApplicationById(applicationId);
        
        if (app == null || string.IsNullOrWhiteSpace(app.IconPath))
            return _iconFactory.GetIcon("app", cssClass);
            
        return _iconFactory.GetIcon(app.IconPath, cssClass);
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationMetadata> GetRecentlyLaunchedApplications(int count = 5)
    {
        return GetApplications()
            .Where(a => a.LastLaunched != default)
            .OrderByDescending(a => a.LastLaunched)
            .Take(count);
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationMetadata> GetFrequentlyUsedApplications(int count = 5)
    {
        return GetApplications()
            .Where(a => a.LaunchCount > 0)
            .OrderByDescending(a => a.LaunchCount)
            .Take(count);
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationMetadata> GetPinnedApplications()
    {
        return GetApplications().Where(a => a.IsPinned);
    }
    
    /// <inheritdoc />
    public async Task<bool> PinApplicationAsync(string applicationId)
    {
        var app = GetApplicationById(applicationId);
        if (app == null)
            return false;
            
        app.IsPinned = true;
        
        // Save user preference
        await SaveUserPreferenceAsync(applicationId, "pinned", true);
        
        return true;
    }
    
    /// <inheritdoc />
    public async Task<bool> UnpinApplicationAsync(string applicationId)
    {
        var app = GetApplicationById(applicationId);
        if (app == null)
            return false;
            
        app.IsPinned = false;
        
        // Save user preference
        await SaveUserPreferenceAsync(applicationId, "pinned", false);
        
        return true;
    }
    
    /// <inheritdoc />
    public async Task RecordApplicationLaunchAsync(string applicationId)
    {
        var app = GetApplicationById(applicationId);
        if (app == null)
            return;
            
        app.LaunchCount++;
        app.LastLaunched = DateTime.UtcNow;
        
        // Save the updated statistics
        await SaveApplicationStatsAsync(app);
    }
    
    /// <inheritdoc />
    public async Task<int> RefreshApplicationsAsync()
    {
        // Clear the cache
        _applications.Clear();
        
        // Rediscover applications
        var discoveredCount = await _discoveryService.DiscoverApplicationsAsync();
        
        // Load applications into cache
        LoadApplications();
        
        return discoveredCount;
    }
    
    /// <summary>
    /// Load applications from the application manager and discovery service
    /// </summary>
    private void LoadApplications()
    {
        try
        {
            // Clear existing cache
            _applications.Clear();
            
            // Get all applications from the application manager
            var manifests = _applicationManager.GetAllApplications();
            
            foreach (var manifest in manifests)
            {
                var metadata = ApplicationMetadata.FromManifest(manifest);
                
                // Load application statistics and user preferences
                LoadApplicationStats(metadata);
                LoadUserPreferences(metadata);
                
                _applications[metadata.Id] = metadata;
            }
            
            _logger.LogInformation("Loaded {Count} applications into registry", _applications.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load applications into registry");
        }
    }
    
    /// <summary>
    /// Load statistics for an application
    /// </summary>
    private void LoadApplicationStats(ApplicationMetadata metadata)
    {
        try
        {
            var statsPath = $"{APP_STATS_DIR}/{metadata.Id}.stats.json";
            
            if (_fileSystem.FileExistsAsync(statsPath).Result)
            {
                var json = _fileSystem.ReadAllTextAsync(statsPath).Result;
                var stats = JsonSerializer.Deserialize<ApplicationStatsDto>(json);
                
                if (stats != null)
                {
                    metadata.LaunchCount = stats.LaunchCount;
                    metadata.LastLaunched = stats.LastLaunched;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load statistics for application {AppId}", metadata.Id);
        }
    }
    
    /// <summary>
    /// Load user preferences for an application
    /// </summary>
    private void LoadUserPreferences(ApplicationMetadata metadata)
    {
        try
        {
            // Get current user
            var currentUser = _userManager.GetUserAsync("current").Result;
            if (currentUser == null)
                return;
                
            var prefsPath = $"/home/{currentUser.Username}{USER_APP_SETTINGS_DIR}/{metadata.Id}.prefs.json";
            
            if (_fileSystem.FileExistsAsync(prefsPath).Result)
            {
                var json = _fileSystem.ReadAllTextAsync(prefsPath).Result;
                var prefs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                
                if (prefs != null && prefs.TryGetValue("pinned", out var pinnedElement))
                {
                    if (pinnedElement.ValueKind == JsonValueKind.True)
                    {
                        metadata.IsPinned = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load user preferences for application {AppId}", metadata.Id);
        }
    }
    
    /// <summary>
    /// Save application statistics
    /// </summary>
    private async Task SaveApplicationStatsAsync(ApplicationMetadata metadata)
    {
        try
        {
            // Get the root user for system operations
            var rootUser = await _userManager.GetUserAsync("root");
            if (rootUser == null) 
            {
                _logger.LogError("Cannot save application stats - root user not found");
                return;
            }
            
            // Ensure the stats directory exists
            if (!await _fileSystem.DirectoryExistsAsync(APP_STATS_DIR, rootUser))
            {
                await _fileSystem.CreateDirectoryAsync(APP_STATS_DIR);
            }
            
            // Create stats DTO
            var stats = new ApplicationStatsDto
            {
                LaunchCount = metadata.LaunchCount,
                LastLaunched = metadata.LastLaunched
            };
            
            // Serialize to JSON
            var json = JsonSerializer.Serialize(stats);
            
            // Write to file
            var statsPath = $"{APP_STATS_DIR}/{metadata.Id}.stats.json";
            await _fileSystem.WriteAllTextAsync(statsPath, json, rootUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save statistics for application {AppId}", metadata.Id);
        }
    }
    
    /// <summary>
    /// Save user preference for an application
    /// </summary>
    private async Task SaveUserPreferenceAsync(string applicationId, string key, object value)
    {
        try
        {
            // Get current user
            var currentUser = _userManager.GetUserAsync("current").Result;
            if (currentUser == null)
                return;
                
            // Build paths
            var userConfigDir = $"/home/{currentUser.Username}/.config";
            var appConfigDir = $"{userConfigDir}/applications";
            var prefsPath = $"{appConfigDir}/{applicationId}.prefs.json";
            
            // Ensure directories exist
            if (!await _fileSystem.DirectoryExistsAsync(userConfigDir, currentUser))
            {
                await _fileSystem.CreateDirectoryAsync(userConfigDir, currentUser);
            }
            
            if (!await _fileSystem.DirectoryExistsAsync(appConfigDir, currentUser))
            {
                await _fileSystem.CreateDirectoryAsync(appConfigDir, currentUser);
            }
            
            // Load existing preferences or create new
            Dictionary<string, object> prefs;
            
            if (await _fileSystem.FileExistsAsync(prefsPath, currentUser))
            {
                var json = await _fileSystem.ReadAllTextAsync(prefsPath, currentUser);
                prefs = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
            }
            else
            {
                prefs = new Dictionary<string, object>();
            }
            
            // Update preference
            prefs[key] = value;
            
            // Save to file
            var updatedJson = JsonSerializer.Serialize(prefs);
            await _fileSystem.WriteAllTextAsync(prefsPath, updatedJson, currentUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save user preference {Key} for application {AppId}", key, applicationId);
        }
    }
    
    /// <summary>
    /// Handle application launched event
    /// </summary>
    private void OnApplicationLaunched(object? sender, ApplicationLaunchedEventArgs e)
    {
        // Record the launch
        _ = RecordApplicationLaunchAsync(e.Application.Id);
    }
    
    /// <summary>
    /// Class to store application statistics
    /// </summary>
    private class ApplicationStatsDto
    {
        public int LaunchCount { get; set; }
        public DateTime LastLaunched { get; set; }
    }

    /// <inheritdoc />
    public IEnumerable<ApplicationMetadata> GetApplicationsByType(ApplicationType applicationType)
    {
        return GetApplications().Where(a => a.Type == applicationType);
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationMetadata> GetWindowedApplications()
    {
        return GetApplicationsByType(ApplicationType.WindowedApplication);
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationMetadata> GetServiceApplications()
    {
        return GetApplicationsByType(ApplicationType.SystemService);
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationMetadata> GetCommandLineApplications()
    {
        return GetApplicationsByType(ApplicationType.CommandLineTool);
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationMetadata> GetSystemApplications()
    {
        return GetApplicationsByType(ApplicationType.SystemApplication);
    }
}
