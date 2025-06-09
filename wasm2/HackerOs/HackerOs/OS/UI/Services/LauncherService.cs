using HackerOs.OS.Applications;
using HackerOs.OS.Core.Settings;
using HackerOs.OS.UI.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HackerOs.OS.UI;

namespace HackerOs.OS.UI.Services
{
    /// <summary>
    /// Service responsible for managing the application launcher
    /// </summary>
    public class LauncherService
    {
        private readonly IApplicationManager _applicationManager;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<LauncherService> _logger;
        
        // Settings keys
        private const string RECENT_APPS_KEY = "launcher.recentApps";
        private const string PINNED_APPS_KEY = "launcher.pinnedApps";
        private const string LAUNCHER_SETTINGS_KEY = "launcher.settings";
        
        // Default categories
        private readonly List<string> _defaultCategories = new List<string>
        {
            "Productivity",
            "System",
            "Utilities",
            "Development",
            "Graphics",
            "Internet",
            "Games",
            "Other"
        };
        
        // Maximum number of recent applications to track
        private const int MAX_RECENT_APPS = 10;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public LauncherService(
            IApplicationManager applicationManager,
            ISettingsService settingsService,
            ILogger<LauncherService> logger)
        {
            _applicationManager = applicationManager ?? throw new ArgumentNullException(nameof(applicationManager));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
          /// <summary>
        /// Gets all applications organized by category
        /// </summary>        
        public async Task<List<AppCategoryModel>> GetApplicationCategoriesAsync()
        {
            try
            {
                // Get all applications
                var apps = _applicationManager.GetAllApplications();
                
                // Get pinned applications
                var pinnedAppIds = await GetPinnedApplicationIdsAsync();
                
                // Create launcher app models using extension method
                var launcherApps = apps.Select(app => app.ToLauncherAppModel(pinnedAppIds.Contains(app.Id))).ToList();
                  // Group by category
                var categories = _defaultCategories.Select(category => new AppCategoryModel
                {
                    Id = category.ToLower(),
                    Name = category,
                    Icon = $"/images/icons/category-{category.ToLower()}.png",
                    Applications = launcherApps.Where(a => a.CategoryId == category).ToList(),
                    IsVisible = true
                }).ToList();                
                // Add "Other" category for applications without a category
                var otherCategory = categories.FirstOrDefault(c => c.Id == "other");
                if (otherCategory != null)
                {
                    var uncategorized = launcherApps.Where(a => !_defaultCategories.Contains(a.CategoryId)).ToList();
                    otherCategory.Applications.AddRange(uncategorized);
                }
                
                // Remove empty categories
                categories = categories.Where(c => c.Applications.Any()).ToList();
                
                return categories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting application categories");
                return new List<AppCategoryModel>();
            }
        }
        
        /// <summary>
        /// Gets recent applications
        /// </summary>
        public async Task<List<LauncherAppModel>> GetRecentApplicationsAsync()
        {
            try
            {
                // Get recent application IDs and timestamps
                var recentAppsJson = await _settingsService.GetSettingAsync(RECENT_APPS_KEY);
                var recentAppDictionary = string.IsNullOrEmpty(recentAppsJson)
                    ? new Dictionary<string, DateTime>()
                    : JsonSerializer.Deserialize<Dictionary<string, DateTime>>(recentAppsJson);
                
                // Get all applications
                var apps = _applicationManager.GetAllApplications();
                
                // Create launcher app models for recent apps
                var recentApps = new List<LauncherAppModel>();
                
                foreach (var appEntry in recentAppDictionary.OrderByDescending(a => a.Value))
                {
                    var app = apps.FirstOrDefault(a => a.Id == appEntry.Key);
                    if (app != null)
                    {
                        recentApps.Add(new LauncherAppModel
                        {
                            Id = app.Id,
                            DisplayName = app.Name,
                            IconPath = app.IconPath ?? "/images/icons/default-app.png",
                            Description = app.Description ?? string.Empty,
                            Category = GetApplicationCategory(app),
                            LastLaunched = appEntry.Value
                        });
                    }
                    
                    if (recentApps.Count >= MAX_RECENT_APPS)
                    {
                        break;
                    }
                }
                
                return recentApps;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent applications");
                return new List<LauncherAppModel>();
            }
        }
        
        /// <summary>
        /// Gets pinned applications
        /// </summary>
        public async Task<List<LauncherAppModel>> GetPinnedApplicationsAsync()
        {
            try
            {
                // Get pinned application IDs
                var pinnedAppIds = await GetPinnedApplicationIdsAsync();
                
                // Get all applications
                var apps = _applicationManager.GetAllApplications();
                
                // Create launcher app models for pinned apps
                var pinnedApps = new List<LauncherAppModel>();
                
                foreach (var appId in pinnedAppIds)
                {
                    var app = apps.FirstOrDefault(a => a.Id == appId);
                    if (app != null)
                    {
                        pinnedApps.Add(new LauncherAppModel
                        {
                            Id = app.Id,
                            DisplayName = app.Name,
                            IconPath = app.IconPath ?? "/images/icons/default-app.png",
                            Description = app.Description ?? string.Empty,
                            Category = GetApplicationCategory(app),
                            IsPinned = true
                        });
                    }
                }
                
                return pinnedApps;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pinned applications");
                return new List<LauncherAppModel>();
            }
        }
        
        /// <summary>
        /// Records an application launch in the recent applications list
        /// </summary>
        public async Task RecordApplicationLaunchAsync(string applicationId)
        {
            try
            {
                // Get recent application IDs and timestamps
                var recentAppsJson = await _settingsService.GetSettingAsync(RECENT_APPS_KEY);
                var recentAppDictionary = string.IsNullOrEmpty(recentAppsJson)
                    ? new Dictionary<string, DateTime>()
                    : JsonSerializer.Deserialize<Dictionary<string, DateTime>>(recentAppsJson);
                
                // Update or add the application
                recentAppDictionary[applicationId] = DateTime.Now;
                
                // Trim the list if needed
                if (recentAppDictionary.Count > MAX_RECENT_APPS)
                {
                    var oldest = recentAppDictionary.OrderBy(a => a.Value).First();
                    recentAppDictionary.Remove(oldest.Key);
                }
                
                // Save the updated list
                var updatedJson = JsonSerializer.Serialize(recentAppDictionary);
                await _settingsService.SaveSettingAsync(RECENT_APPS_KEY, updatedJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recording application launch for {applicationId}");
            }
        }
        
        /// <summary>
        /// Pins or unpins an application
        /// </summary>
        public async Task ToggleApplicationPinAsync(string applicationId)
        {
            try
            {
                // Get pinned application IDs
                var pinnedAppIds = await GetPinnedApplicationIdsAsync();
                
                // Toggle the pin status
                if (pinnedAppIds.Contains(applicationId))
                {
                    pinnedAppIds.Remove(applicationId);
                }
                else
                {
                    pinnedAppIds.Add(applicationId);
                }
                
                // Save the updated list
                var updatedJson = JsonSerializer.Serialize(pinnedAppIds);
                await _settingsService.SaveSettingAsync(PINNED_APPS_KEY, updatedJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error toggling application pin for {applicationId}");
            }
        }
        
        /// <summary>
        /// Searches for applications based on a query
        /// </summary>
        public async Task<List<LauncherAppModel>> SearchApplicationsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<LauncherAppModel>();
            }
            
            try
            {
                // Get all applications
                var apps = _applicationManager.GetAllApplications();
                
                // Get pinned applications
                var pinnedAppIds = await GetPinnedApplicationIdsAsync();
                
                // Create launcher app models
                var launcherApps = apps.Select(app => new LauncherAppModel
                {
                    Id = app.Id,
                    DisplayName = app.Name,
                    IconPath = app.IconPath ?? "/images/icons/default-app.png",
                    Description = app.Description ?? string.Empty,
                    Category = GetApplicationCategory(app),
                    Keywords = GetApplicationKeywords(app),
                    IsPinned = pinnedAppIds.Contains(app.Id)
                }).ToList();
                
                // Normalize query
                query = query.ToLowerInvariant().Trim();
                
                // Search for matches
                var exactMatches = new List<LauncherAppModel>();
                var nameMatches = new List<LauncherAppModel>();
                var descriptionMatches = new List<LauncherAppModel>();
                var keywordMatches = new List<LauncherAppModel>();
                
                foreach (var app in launcherApps)
                {
                    // Check for exact match on name
                    if (app.DisplayName.ToLowerInvariant() == query)
                    {
                        exactMatches.Add(app);
                        continue;
                    }
                    
                    // Check for partial match on name
                    if (app.DisplayName.ToLowerInvariant().Contains(query))
                    {
                        nameMatches.Add(app);
                        continue;
                    }
                    
                    // Check for match on description
                    if (!string.IsNullOrEmpty(app.Description) && 
                        app.Description.ToLowerInvariant().Contains(query))
                    {
                        descriptionMatches.Add(app);
                        continue;
                    }
                    
                    // Check for match on keywords
                    if (app.Keywords.Any(k => k.ToLowerInvariant().Contains(query)))
                    {
                        keywordMatches.Add(app);
                    }
                }
                
                // Combine all matches with priority
                var results = new List<LauncherAppModel>();
                results.AddRange(exactMatches);
                results.AddRange(nameMatches);
                results.AddRange(descriptionMatches);
                results.AddRange(keywordMatches);
                
                return results.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching applications for '{query}'");
                return new List<LauncherAppModel>();
            }
        }
        
        /// <summary>
        /// Gets the list of pinned application IDs
        /// </summary>
        private async Task<List<string>> GetPinnedApplicationIdsAsync()
        {
            var pinnedAppsJson = await _settingsService.GetSettingAsync(PINNED_APPS_KEY);
            return string.IsNullOrEmpty(pinnedAppsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(pinnedAppsJson);
        }
        
        /// <summary>
        /// Gets the category for an application
        /// </summary>
        private string GetApplicationCategory(IApplication app)
        {
            return GetApplicationCategory(app.Manifest);
        }

        /// <summary>
        /// Gets the category for an application manifest
        /// </summary>
        private string GetApplicationCategory(ApplicationManifest manifest)
        {
            var category = manifest.Categories.FirstOrDefault() ?? "Other";
            return _defaultCategories.Contains(category) ? category : "Other";
        }
        
        /// <summary>
        /// Gets keywords for an application to use in search
        /// </summary>
        private List<string> GetApplicationKeywords(IApplication app)
        {
            return GetApplicationKeywords(app.Manifest);
        }

        /// <summary>
        /// Gets keywords for an application manifest to use in search
        /// </summary>
        private List<string> GetApplicationKeywords(ApplicationManifest manifest)
        {
            var keywords = new List<string> { manifest.Name };

            if (!string.IsNullOrEmpty(manifest.Description))
            {
                keywords.AddRange(manifest.Description.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }

            keywords.AddRange(manifest.Categories);

            return keywords.Distinct().ToList();
        }
        
        /// <summary>
        /// Convert an ApplicationManifest to a LauncherAppModel
        /// </summary>
        private LauncherAppModel ConvertToLauncherAppModel(ApplicationManifest app, bool isPinned = false)
        {
            return new LauncherAppModel
            {
                Id = app.Id,
                Name = app.Name,
                Icon = app.IconPath ?? "/images/icons/default-app.png",
                Description = app.Description ?? string.Empty,
                CategoryId = GetApplicationCategory(app),
                Tags = GetApplicationKeywords(app),
                IsPinned = isPinned,
                ComponentType = app.EntryPoint,
                Version = app.Version ?? "1.0.0"
            };
        }
    }
}
