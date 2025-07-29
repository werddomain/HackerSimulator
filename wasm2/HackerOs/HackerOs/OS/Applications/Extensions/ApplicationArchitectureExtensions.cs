using HackerOs.OS.Applications.Core;
using HackerOs.OS.Applications.Interfaces;
using HackerOs.OS.Applications.UI;
using Microsoft.Extensions.DependencyInjection;

namespace HackerOs.OS.Applications.Extensions
{
    /// <summary>
    /// Extension methods for registering application architecture services
    /// </summary>
    public static class ApplicationArchitectureExtensions
    {
        /// <summary>
        /// Registers the application architecture services with the dependency injection container
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddApplicationArchitecture(this IServiceCollection services)
        {
            // Register the ApplicationBridge as a scoped service
            // Scoped is appropriate since it connects individual applications to the system
            services.AddScoped<IApplicationBridge, ApplicationBridge>();
            
            // Register the window application manager integration
            // Singleton is appropriate since it coordinates between global services
            services.AddSingleton<WindowApplicationManagerIntegration>();
            
            return services;
        }
    }
}
