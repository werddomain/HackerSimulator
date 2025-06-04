using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications;

/// <summary>
/// Interface for searching and filtering applications
/// </summary>
public interface IApplicationFinder
{
    /// <summary>
    /// Searches for applications by name or description
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="limit">Maximum number of results (0 for no limit)</param>
    /// <returns>Matching applications</returns>
    IEnumerable<ApplicationManifest> SearchApplications(string query, int limit = 0);
    
    /// <summary>
    /// Gets applications by category
    /// </summary>
    /// <param name="category">Category name</param>
    /// <returns>Applications in the category</returns>
    IEnumerable<ApplicationManifest> GetApplicationsByCategory(string category);
    
    /// <summary>
    /// Gets all available categories
    /// </summary>
    /// <returns>List of categories with application counts</returns>
    IDictionary<string, int> GetCategories();
    
    /// <summary>
    /// Gets applications that can open a specific file type
    /// </summary>
    /// <param name="extension">File extension (with or without dot)</param>
    /// <returns>Applications that support the file type</returns>
    IEnumerable<ApplicationManifest> GetApplicationsForFileType(string extension);
    
    /// <summary>
    /// Gets recently used applications
    /// </summary>
    /// <param name="limit">Maximum number of results (0 for no limit)</param>
    /// <returns>Recently used applications</returns>
    IEnumerable<ApplicationManifest> GetRecentApplications(int limit = 5);
    
    /// <summary>
    /// Gets popular applications
    /// </summary>
    /// <param name="limit">Maximum number of results (0 for no limit)</param>
    /// <returns>Popular applications</returns>
    IEnumerable<ApplicationManifest> GetPopularApplications(int limit = 5);
}

/// <summary>
/// Implementation of the application finder
/// </summary>
public class ApplicationFinder : IApplicationFinder
{
    private readonly ILogger<ApplicationFinder> _logger;
    private readonly IApplicationManager _applicationManager;
    private readonly IFileTypeRegistry _fileTypeRegistry;
    
    // Cache of recently used applications
    private readonly List<string> _recentApplications = new();
    private readonly Dictionary<string, int> _applicationUsageCounts = new();
    
    /// <summary>
    /// Creates a new instance of the ApplicationFinder
    /// </summary>
    public ApplicationFinder(
        ILogger<ApplicationFinder> logger,
        IApplicationManager applicationManager,
        IFileTypeRegistry fileTypeRegistry)
    {
        _logger = logger;
        _applicationManager = applicationManager;
        _fileTypeRegistry = fileTypeRegistry;
        
        // Subscribe to application launch events
        _applicationManager.ApplicationLaunched += OnApplicationLaunched;
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationManifest> SearchApplications(string query, int limit = 0)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Enumerable.Empty<ApplicationManifest>();
            
        try
        {
            // Normalize query
            query = query.Trim().ToLowerInvariant();
            
            // Get all applications
            var allApps = _applicationManager.GetAvailableApplications();
            
            // Filter by name, description, categories
            var results = allApps
                .Where(app => 
                    app.Name.ToLowerInvariant().Contains(query) ||
                    app.Description.ToLowerInvariant().Contains(query) ||
                    app.Categories.Any(cat => cat.ToLowerInvariant().Contains(query)))
                .OrderByDescending(app => app.Name.ToLowerInvariant().StartsWith(query) ? 2 : 1)
                .ThenByDescending(app => GetApplicationScore(app.Id))
                .ThenBy(app => app.Name);
                
            // Apply limit if specified
            if (limit > 0)
            {
                results = results.Take(limit);
            }
            
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for applications with query: {Query}", query);
            return Enumerable.Empty<ApplicationManifest>();
        }
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationManifest> GetApplicationsByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return Enumerable.Empty<ApplicationManifest>();
            
        try
        {
            // Normalize category
            category = category.Trim();
            
            // Get all applications
            var allApps = _applicationManager.GetAvailableApplications();
            
            // Filter by category
            var results = allApps
                .Where(app => app.Categories.Any(cat => 
                    string.Equals(cat, category, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(app => GetApplicationScore(app.Id))
                .ThenBy(app => app.Name);
                
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting applications for category: {Category}", category);
            return Enumerable.Empty<ApplicationManifest>();
        }
    }
    
    /// <inheritdoc />
    public IDictionary<string, int> GetCategories()
    {
        try
        {
            var categoryMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            
            // Get all applications
            var allApps = _applicationManager.GetAvailableApplications();
            
            // Group by category
            foreach (var app in allApps)
            {
                foreach (var category in app.Categories)
                {
                    if (!string.IsNullOrWhiteSpace(category))
                    {
                        if (categoryMap.ContainsKey(category))
                        {
                            categoryMap[category]++;
                        }
                        else
                        {
                            categoryMap[category] = 1;
                        }
                    }
                }
            }
            
            return categoryMap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting application categories");
            return new Dictionary<string, int>();
        }
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationManifest> GetApplicationsForFileType(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return Enumerable.Empty<ApplicationManifest>();
            
        try
        {
            // Normalize extension
            extension = extension.TrimStart('.');
            
            // Get registrations for this extension
            var registrations = _fileTypeRegistry.GetHandlersForExtension(extension);
            
            // Get all application manifests
            var allApps = _applicationManager.GetAvailableApplications().ToDictionary(a => a.Id);
            
            // Build result list
            var results = new List<ApplicationManifest>();
            
            // Add applications in order of handler priority
            foreach (var registration in registrations)
            {
                if (allApps.TryGetValue(registration.ApplicationId, out var app))
                {
                    results.Add(app);
                }
            }
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting applications for file type: {Extension}", extension);
            return Enumerable.Empty<ApplicationManifest>();
        }
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationManifest> GetRecentApplications(int limit = 5)
    {
        try
        {
            // Get all applications
            var allApps = _applicationManager.GetAvailableApplications().ToDictionary(a => a.Id);
            
            // Build result list from recent applications
            var results = new List<ApplicationManifest>();
            
            // Process recently used applications in reverse order (newest first)
            foreach (var appId in _recentApplications)
            {
                if (allApps.TryGetValue(appId, out var app) && !results.Contains(app))
                {
                    results.Add(app);
                    
                    // Apply limit if specified
                    if (limit > 0 && results.Count >= limit)
                    {
                        break;
                    }
                }
            }
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent applications");
            return Enumerable.Empty<ApplicationManifest>();
        }
    }
    
    /// <inheritdoc />
    public IEnumerable<ApplicationManifest> GetPopularApplications(int limit = 5)
    {
        try
        {
            // Get all applications
            var allApps = _applicationManager.GetAvailableApplications();
            
            // Sort by usage count
            var results = allApps
                .OrderByDescending(app => _applicationUsageCounts.TryGetValue(app.Id, out var count) ? count : 0)
                .ThenBy(app => app.Name);
                
            // Apply limit if specified
            if (limit > 0)
            {
                results = results.Take(limit);
            }
            
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting popular applications");
            return Enumerable.Empty<ApplicationManifest>();
        }
    }
    
    /// <summary>
    /// Handles application launch events
    /// </summary>
    private void OnApplicationLaunched(object? sender, ApplicationLaunchedEventArgs e)
    {
        try
        {
            var appId = e.Application.Id;
            
            // Update recent applications list
            _recentApplications.Remove(appId); // Remove if already exists
            _recentApplications.Insert(0, appId); // Add to beginning
            
            // Keep list at reasonable size
            if (_recentApplications.Count > 100)
            {
                _recentApplications.RemoveAt(_recentApplications.Count - 1);
            }
            
            // Update usage count
            if (_applicationUsageCounts.ContainsKey(appId))
            {
                _applicationUsageCounts[appId]++;
            }
            else
            {
                _applicationUsageCounts[appId] = 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating application usage statistics");
        }
    }
    
    /// <summary>
    /// Gets a score for an application based on usage
    /// </summary>
    private int GetApplicationScore(string applicationId)
    {
        // Recent applications get higher scores
        int recentScore = _recentApplications.IndexOf(applicationId);
        if (recentScore >= 0)
        {
            recentScore = _recentApplications.Count - recentScore; // Reverse order so newer is higher
        }
        
        // Popular applications get higher scores
        int popularityScore = _applicationUsageCounts.TryGetValue(applicationId, out var count) ? count : 0;
        
        // Combine scores
        return (recentScore * 10) + popularityScore;
    }
}
