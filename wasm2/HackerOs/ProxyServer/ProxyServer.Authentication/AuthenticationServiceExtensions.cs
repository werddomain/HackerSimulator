using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ProxyServer.Authentication
{
    /// <summary>
    /// Extension methods for registering authentication services.
    /// </summary>
    public static class AuthenticationServiceExtensions
    {
        /// <summary>
        /// Adds authentication services to the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            // Register authentication configuration
            var authConfig = new AuthenticationConfiguration();
            configuration.GetSection(AuthenticationConfiguration.SectionName).Bind(authConfig);
            services.AddSingleton(authConfig);

            // Register authentication service
            services.AddSingleton<IAuthenticationService, AuthenticationService>();

            return services;
        }

        /// <summary>
        /// Adds authentication services to the dependency injection container with custom configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">Action to configure authentication options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAuthentication(this IServiceCollection services, Action<AuthenticationConfiguration> configureOptions)
        {
            var authConfig = new AuthenticationConfiguration();
            configureOptions(authConfig);
            services.AddSingleton(authConfig);

            // Register authentication service
            services.AddSingleton<IAuthenticationService, AuthenticationService>();

            return services;
        }

        /// <summary>
        /// Adds default authentication configuration for development/testing.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddDefaultAuthentication(this IServiceCollection services)
        {
            return services.AddAuthentication(config =>
            {
                config.ApiKeys = new List<ApiKeyConfiguration>
                {
                    new ApiKeyConfiguration
                    {
                        Key = "dev-admin-key-12345678901234567890",
                        UserId = "dev-admin",
                        Role = "admin",
                        DisplayName = "Development Admin",
                        IsEnabled = true
                    },
                    new ApiKeyConfiguration
                    {
                        Key = "dev-user-key-12345678901234567890",
                        UserId = "dev-user",
                        Role = "user",
                        DisplayName = "Development User",
                        IsEnabled = true
                    },
                    new ApiKeyConfiguration
                    {
                        Key = "dev-power-key-12345678901234567890",
                        UserId = "dev-power",
                        Role = "power_user",
                        DisplayName = "Development Power User",
                        IsEnabled = true
                    }
                };
                config.SessionTimeoutMinutes = 30;
                config.SessionCleanupIntervalMinutes = 5;
                config.MaxSessionsPerClient = 3;
                config.RequireStrongApiKeys = false; // Relaxed for development
                config.MinApiKeyLength = 16;
            });
        }
    }
}
