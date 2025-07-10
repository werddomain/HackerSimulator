using HackerOs.OS.Applications.Icons;
using HackerOs.OS.Applications.Launcher;
using HackerOs.OS.Applications.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace HackerOs.OS.Applications.Extensions;

/// <summary>
/// Extension methods for registering application registry and launcher services
/// </summary>
public static class ApplicationRegistryExtensions
{
    /// <summary>
    /// Add application registry and launcher services to the service collection
    /// </summary>
    public static IServiceCollection AddApplicationRegistry(this IServiceCollection services)
    {
        // Register the icon services first
        services.AddIconServices();
        
        // Register the application registry
        services.AddSingleton<IApplicationRegistry, ApplicationRegistry>();
        
        // Register the application launcher
        services.AddScoped<IApplicationLauncher, ApplicationLauncher>();
        
        return services;
    }
}
