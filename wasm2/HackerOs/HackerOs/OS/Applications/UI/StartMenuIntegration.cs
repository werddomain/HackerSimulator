using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.UI;

/// <summary>
/// Interface for start menu application integration
/// </summary>
public interface IStartMenuIntegration
{
    /// <summary>
    /// Gets applications for the start menu
    /// </summary>
    /// <param name="userSession">User session</param>
    /// <returns>Applications grouped by category</returns>
    Task<StartMenuApplications> GetStartMenuApplicationsAsync(UserSession userSession);
    
    /// <summary>
    /// Gets pinned applications for the start menu
    /// </summary>
    /// <param name="userSession">User session</param>
    /// <returns>Pinned applications</returns>
    Task<IEnumerable<StartMenuItem>> GetPinnedApplicationsAsync(UserSession userSession);
    
    /// <summary>
    /// Pins an application to the start menu
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="userSession">User session</param>
    /// <returns>True if successful</returns>
    Task<bool> PinApplicationAsync(string applicationId, UserSession userSession);
    
    /// <summary>
    /// Unpins an application from the start menu
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="userSession">User session</param>
    /// <returns>True if successful</returns>
    Task<bool> UnpinApplicationAsync(string applicationId, UserSession userSession);
    
    /// <summary>
    /// Gets recently used applications
    /// </summary>
    /// <param name="userSession">User session</param>
    /// <param name="limit">Maximum number of applications</param>
    /// <returns>Recently used applications</returns>
    Task<IEnumerable<StartMenuItem>> GetRecentApplicationsAsync(UserSession userSession, int limit = 5);
    
    /// <summary>
    /// Searches for applications in the start menu
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="userSession">User session</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>Matching applications</returns>
    Task<IEnumerable<StartMenuItem>> SearchStartMenuAsync(string query, UserSession userSession, int limit = 10);
}

/// <summary>
/// Represents a start menu application item
/// </summary>
public class StartMenuItem
{
    /// <summary>
    /// Application ID
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Icon path
    /// </summary>
    public string? IconPath { get; set; }
    
    /// <summary>
    /// Category
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the application is pinned to the start menu
    /// </summary>
    public bool IsPinned { get; set; }
}

/// <summary>
/// Contains applications for the start menu
/// </summary>
public class StartMenuApplications
{
    /// <summary>
    /// Applications grouped by category
    /// </summary>
    public Dictionary<string, List<StartMenuItem>> Categories { get; set; } = new();
    
    /// <summary>
    /// All applications flattened into a single list
    /// </summary>
    public List<StartMenuItem> AllApplications { get; set; } = new();
    
    /// <summary>
    /// Pinned applications
    /// </summary>
    public List<StartMenuItem> PinnedApplications { get; set; } = new();
    
    /// <summary>
    /// Recent applications
    /// </summary>
    public List<StartMenuItem> RecentApplications { get; set; } = new();
}

/// <summary>
/// Implementation of the start menu integration
/// </summary>
public class StartMenuIntegration : IStartMenuIntegration
{
    private readonly ILogger<StartMenuIntegration> _logger;
    private readonly IApplicationManager _applicationManager;
    private readonly IApplicationFinder _applicationFinder;
    private readonly IUserSettingsService _userSettingsService;
    
    // User settings keys
    private const string PINNED_APPS_SETTING = "startmenu.pinnedapps";
    
    /// <summary>
    /// Creates a new instance of the StartMenuIntegration
    /// </summary>
    public StartMenuIntegration(
        ILogger<StartMenuIntegration> logger,
        IApplicationManager applicationManager,
        IApplicationFinder applicationFinder,
        IUserSettingsService userSettingsService)
    {
        _logger = logger;
        _applicationManager = applicationManager;
        _applicationFinder = applicationFinder;
        _userSettingsService = userSettingsService;
    }
    
    /// <inheritdoc />
    public async Task<StartMenuApplications> GetStartMenuApplicationsAsync(UserSession userSession)
    {
        var result = new StartMenuApplications();
        
        try
        {
            // Get all applications
            var allApps = _applicationManager.GetAvailableApplications();
            
            // Convert to start menu items
            foreach (var app in allApps)
            {
                var item = new StartMenuItem
                {
                    Id = app.Id,
                    Name = app.Name,
                    Description = app.Description,
                    IconPath = app.IconPath
                };
                
                // Check if the application is pinned
                item.IsPinned = await IsApplicationPinnedAsync(app.Id, userSession);
                
                // Add to the flattened list
                result.AllApplications.Add(item);
                
                // Add to the pinned list if applicable
                if (item.IsPinned)
                {
                    result.PinnedApplications.Add(item);
                }
                
                // Add to categories
                foreach (var category in app.Categories)
                {
                    if (!result.Categories.TryGetValue(category, out var categoryApps))
                    {
                        categoryApps = new List<StartMenuItem>();
                        result.Categories[category] = categoryApps;
                    }
                    
                    // Set the category on the item
                    var categoryItem = new StartMenuItem
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        IconPath = item.IconPath,
                        IsPinned = item.IsPinned,
                        Category = category
                    };
                    
                    categoryApps.Add(categoryItem);
                }
            }
            
            // Get recent applications
            var recentApps = await GetRecentApplicationsAsync(userSession);
            result.RecentApplications.AddRange(recentApps);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting start menu applications");
            return result;
        }
    }
    
    /// <inheritdoc />
    public async Task<IEnumerable<StartMenuItem>> GetPinnedApplicationsAsync(UserSession userSession)
    {
        try
        {
            // Get pinned application IDs
            var pinnedIds = await GetPinnedApplicationIdsAsync(userSession);
            
            // Get application manifests
            var allApps = _applicationManager.GetAvailableApplications().ToDictionary(a => a.Id);
            
            // Convert to start menu items
            var result = new List<StartMenuItem>();
            
            foreach (var id in pinnedIds)
            {
                if (allApps.TryGetValue(id, out var app))
                {
                    result.Add(new StartMenuItem
                    {
                        Id = app.Id,
                        Name = app.Name,
                        Description = app.Description,
                        IconPath = app.IconPath,
                        IsPinned = true
                    });
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pinned applications");
            return Enumerable.Empty<StartMenuItem>();
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> PinApplicationAsync(string applicationId, UserSession userSession)
    {
        try
        {
            // Check if the application exists
            var app = _applicationManager.GetApplication(applicationId);
            if (app == null)
            {
                _logger.LogWarning("Cannot pin application {AppId}: not found", applicationId);
                return false;
            }
            
            // Get current pinned applications
            var pinnedIds = await GetPinnedApplicationIdsAsync(userSession);
            
            // Check if already pinned
            if (pinnedIds.Contains(applicationId))
            {
                // Already pinned
                return true;
            }
            
            // Add to pinned list
            pinnedIds.Add(applicationId);
            
            // Save back to settings
            await SavePinnedApplicationIdsAsync(pinnedIds, userSession);
            
            _logger.LogInformation("Application {AppId} pinned to start menu for user {User}",
                applicationId, userSession.User.Username);
                
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pinning application {AppId}", applicationId);
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> UnpinApplicationAsync(string applicationId, UserSession userSession)
    {
        try
        {
            // Get current pinned applications
            var pinnedIds = await GetPinnedApplicationIdsAsync(userSession);
            
            // Check if pinned
            if (!pinnedIds.Contains(applicationId))
            {
                // Not pinned
                return true;
            }
            
            // Remove from pinned list
            pinnedIds.Remove(applicationId);
            
            // Save back to settings
            await SavePinnedApplicationIdsAsync(pinnedIds, userSession);
            
            _logger.LogInformation("Application {AppId} unpinned from start menu for user {User}",
                applicationId, userSession.User.Username);
                
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unpinning application {AppId}", applicationId);
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<IEnumerable<StartMenuItem>> GetRecentApplicationsAsync(UserSession userSession, int limit = 5)
    {
        try
        {
            // Get recent applications from the finder
            var recentApps = _applicationFinder.GetRecentApplications(limit);
            
            // Convert to start menu items
            var result = new List<StartMenuItem>();
            
            foreach (var app in recentApps)
            {
                var isPinned = await IsApplicationPinnedAsync(app.Id, userSession);
                
                result.Add(new StartMenuItem
                {
                    Id = app.Id,
                    Name = app.Name,
                    Description = app.Description,
                    IconPath = app.IconPath,
                    IsPinned = isPinned
                });
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent applications");
            return Enumerable.Empty<StartMenuItem>();
        }
    }
    
    /// <inheritdoc />
    public async Task<IEnumerable<StartMenuItem>> SearchStartMenuAsync(string query, UserSession userSession, int limit = 10)
    {
        try
        {
            // Search for applications
            var searchResults = _applicationFinder.SearchApplications(query, limit);
            
            // Convert to start menu items
            var result = new List<StartMenuItem>();
            
            foreach (var app in searchResults)
            {
                var isPinned = await IsApplicationPinnedAsync(app.Id, userSession);
                
                result.Add(new StartMenuItem
                {
                    Id = app.Id,
                    Name = app.Name,
                    Description = app.Description,
                    IconPath = app.IconPath,
                    IsPinned = isPinned
                });
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching start menu with query: {Query}", query);
            return Enumerable.Empty<StartMenuItem>();
        }
    }
    
    /// <summary>
    /// Gets the list of pinned application IDs for a user
    /// </summary>
    private async Task<List<string>> GetPinnedApplicationIdsAsync(UserSession userSession)
    {
        try
        {
            // Get pinned applications from user settings
            var setting = await _userSettingsService.GetSettingAsync(userSession.User.Username, PINNED_APPS_SETTING);
            
            if (string.IsNullOrEmpty(setting))
            {
                return new List<string>();
            }
            
            // Parse comma-separated list
            return setting.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pinned application IDs for user {User}", userSession.User.Username);
            return new List<string>();
        }
    }
    
    /// <summary>
    /// Saves the list of pinned application IDs for a user
    /// </summary>
    private async Task SavePinnedApplicationIdsAsync(List<string> pinnedIds, UserSession userSession)
    {
        try
        {
            // Join IDs into comma-separated list
            string setting = string.Join(",", pinnedIds);
            
            // Save to user settings
            await _userSettingsService.SaveSettingAsync(userSession.User.Username, PINNED_APPS_SETTING, setting);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving pinned application IDs for user {User}", userSession.User.Username);
        }
    }
    
    /// <summary>
    /// Checks if an application is pinned for a user
    /// </summary>
    private async Task<bool> IsApplicationPinnedAsync(string applicationId, UserSession userSession)
    {
        try
        {
            var pinnedIds = await GetPinnedApplicationIdsAsync(userSession);
            return pinnedIds.Contains(applicationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if application {AppId} is pinned for user {User}", 
                applicationId, userSession.User.Username);
            return false;
        }
    }
}

/// <summary>
/// Interface for user settings service (simplified for this implementation)
/// </summary>
public interface IUserSettingsService
{
    /// <summary>
    /// Gets a user setting
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="key">Setting key</param>
    /// <returns>Setting value</returns>
    Task<string> GetSettingAsync(string username, string key);
    
    /// <summary>
    /// Saves a user setting
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="key">Setting key</param>
    /// <param name="value">Setting value</param>
    /// <returns>True if successful</returns>
    Task<bool> SaveSettingAsync(string username, string key, string value);
}

/// <summary>
/// Simple implementation of the user settings service that uses an in-memory cache
/// </summary>
public class UserSettingsService : IUserSettingsService
{
    private readonly Dictionary<string, Dictionary<string, string>> _userSettings = new();
    
    /// <inheritdoc />
    public Task<string> GetSettingAsync(string username, string key)
    {
        if (_userSettings.TryGetValue(username, out var settings) && 
            settings.TryGetValue(key, out var value))
        {
            return Task.FromResult(value);
        }
        
        return Task.FromResult(string.Empty);
    }
    
    /// <inheritdoc />
    public Task<bool> SaveSettingAsync(string username, string key, string value)
    {
        if (!_userSettings.TryGetValue(username, out var settings))
        {
            settings = new Dictionary<string, string>();
            _userSettings[username] = settings;
        }
        
        settings[key] = value;
        return Task.FromResult(true);
    }
}
