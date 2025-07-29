using Microsoft.Extensions.DependencyInjection;
using System;
using HackerOs.OS.UI.Services;

namespace HackerOs.OS.UI
{
    /// <summary>
    /// Extension methods for registering UI services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers UI services with the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddHackerOsUI(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            
            // Register the application window manager
            services.AddSingleton<ApplicationWindowManager>();
            
            // Register desktop-related services
            services.AddSingleton<DesktopSettingsService>();
            services.AddSingleton<DesktopIconService>();
            
            // Register launcher-related services
            services.AddSingleton<LauncherService>();
            
            // Register taskbar and notification services
            services.AddSingleton<NotificationService>();
            
            return services;
        }
    }
}
