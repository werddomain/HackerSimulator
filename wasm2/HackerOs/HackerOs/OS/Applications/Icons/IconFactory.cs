using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace HackerOs.OS.Applications.Icons;

/// <summary>
/// Factory service for creating icons from various sources
/// </summary>
public class IconFactory
{
    private readonly IEnumerable<IIconProvider> _providers;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, RenderFragment> _renderCache = new();
    
    /// <summary>
    /// Create a new IconFactory with the provided icon providers
    /// </summary>
    /// <param name="providers">Collection of icon providers</param>
    /// <param name="memoryCache">Memory cache for caching rendered icons</param>
    public IconFactory(IEnumerable<IIconProvider> providers, IMemoryCache memoryCache)
    {
        _providers = providers.OrderByDescending(p => p.Priority).ToList();
        _cache = memoryCache;
    }
    
    /// <summary>
    /// Get an icon as a RenderFragment from the provided icon path
    /// </summary>
    /// <param name="iconPath">Path or identifier for the icon</param>
    /// <param name="cssClass">Optional CSS class to apply to the icon</param>
    /// <returns>A RenderFragment representing the icon</returns>
    public RenderFragment GetIcon(string iconPath, string? cssClass = null)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return GetDefaultIcon(cssClass);
        }
        
        // Generate a cache key based on the path and CSS class
        string cacheKey = $"icon_{iconPath}_{cssClass}";
        
        // Try to get from cache first
        if (_renderCache.TryGetValue(cacheKey, out var cachedFragment))
        {
            return cachedFragment;
        }
        
        // Find a provider that can handle this icon
        foreach (var provider in _providers)
        {
            if (provider.CanHandleIcon(iconPath))
            {
                var fragment = provider.GetIcon(iconPath, cssClass);
                
                // Cache the result
                _renderCache[cacheKey] = fragment;
                
                return fragment;
            }
        }
        
        // If we get here, use the default icon
        return GetDefaultIcon(cssClass);
    }
    
    /// <summary>
    /// Get the default icon as a fallback
    /// </summary>
    private RenderFragment GetDefaultIcon(string? cssClass = null)
    {
        // Find the default provider
        var defaultProvider = _providers.FirstOrDefault(p => p is DefaultIconProvider) 
                            ?? _providers.LastOrDefault();
        
        if (defaultProvider != null)
        {
            return defaultProvider.GetIcon("app", cssClass);
        }
        
        // If no provider is available, return an empty fragment
        return builder => { };
    }
    
    /// <summary>
    /// Clear the icon cache
    /// </summary>
    public void ClearCache()
    {
        _renderCache.Clear();
    }
}
