using System.Reflection;
using BlazorWindowManager.Extensions;
using BlazorWindowManager.Services;
using HackerOs.AppFramework.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace HackerOs.AppFramework.Extensions;

/// <summary>
/// Dependency injection helpers for wiring up the HackerOS application framework.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the window manager and the <see cref="AppRegistry"/>, then
    /// discovers every self-registering application in the supplied module
    /// assemblies. When no assemblies are supplied the calling assembly (the host
    /// application) is scanned automatically.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="moduleAssemblies">
    /// Assemblies to scan for components decorated with
    /// <see cref="Abstractions.AppAttribute"/>.
    /// </param>
    public static IServiceCollection AddHackerOsAppFramework(
        this IServiceCollection services,
        params Assembly[] moduleAssemblies)
    {
        // Bring in the underlying window management stack.
        services.AddBlazorWindowManager();

        // Default to the host assembly that invoked this method.
        var assemblies = moduleAssemblies is { Length: > 0 }
            ? moduleAssemblies
            : new[] { Assembly.GetCallingAssembly() };

        services.AddSingleton(sp =>
        {
            var registry = new AppRegistry(sp.GetRequiredService<WindowManagerService>());
            registry.DiscoverFrom(assemblies);
            return registry;
        });

        return services;
    }
}
