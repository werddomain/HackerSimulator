using Microsoft.Extensions.DependencyInjection;
using HackerOs.OS.Applications.Core;
using HackerOs.OS.Applications.Interfaces;
using HackerOs.OS.Applications.UI;

namespace HackerOs.OS.Applications.Extensions
{
    /// <summary>
    /// Service collection extensions for registering application services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the application architecture related services
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection RegisterApplicationArchitectureServices(this IServiceCollection services)
        {
            // Register the ApplicationBridge
            services.AddScoped<IApplicationBridge, ApplicationBridge>();
            
            // Register Window Manager Integration
            services.AddSingleton<WindowApplicationManagerIntegration>();
            
            return services;
        }
    }
}
