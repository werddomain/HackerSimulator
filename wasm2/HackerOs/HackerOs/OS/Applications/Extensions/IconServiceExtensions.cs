using HackerOs.OS.Applications.Icons;
using Microsoft.Extensions.DependencyInjection;

namespace HackerOs.OS.Applications.Extensions;

/// <summary>
/// Extension methods for registering icon-related services
/// </summary>
public static class IconServiceExtensions
{
    /// <summary>
    /// Add icon services to the service collection
    /// </summary>
    public static IServiceCollection AddIconServices(this IServiceCollection services)
    {
        // Register all icon providers
        services.AddSingleton<IIconProvider, FilePathIconProvider>();
        services.AddSingleton<IIconProvider, FontAwesomeIconProvider>();
        services.AddSingleton<IIconProvider, MaterialIconProvider>();
        services.AddSingleton<IIconProvider, DefaultIconProvider>();
        
        // Register the icon factory
        services.AddSingleton<IconFactory>();
        
        return services;
    }
}
