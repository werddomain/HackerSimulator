using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Security
{
    /// <summary>
    /// Extension methods for registering enhanced user management services
    /// </summary>
    public static class EnhancedUserManagementExtensions
    {
        /// <summary>
        /// Adds enhanced user management services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddEnhancedUserManagement(this IServiceCollection services)
        {
            // Register the factory
            services.AddSingleton<IUserManagerFactory>(serviceProvider =>
            {
                // Create a factory that can produce EnhancedUserManager instances
                return new DefaultUserManagerFactory(
                    serviceProvider.GetRequiredService<IVirtualFileSystem>(),
                    serviceProvider.GetRequiredService<ILoggerFactory>());
            });
            
            // Register the enhanced user manager as the implementation for IUserManager
            services.AddSingleton<IUserManager>(serviceProvider =>
            {
                var factory = serviceProvider.GetRequiredService<IUserManagerFactory>();
                return factory.CreateEnhanced();
            });
            
            return services;
        }
    }
    
    /// <summary>
    /// Factory interface for creating user managers
    /// </summary>
    public interface IUserManagerFactory
    {
        /// <summary>
        /// Creates a standard user manager
        /// </summary>
        UserManager CreateStandard();
        
        /// <summary>
        /// Creates an enhanced user manager
        /// </summary>
        EnhancedUserManager CreateEnhanced();
    }
    
    /// <summary>
    /// Default implementation of IUserManagerFactory
    /// </summary>
    internal class DefaultUserManagerFactory : IUserManagerFactory
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILoggerFactory _loggerFactory;
        
        /// <summary>
        /// Creates a new instance of DefaultUserManagerFactory
        /// </summary>
        public DefaultUserManagerFactory(IVirtualFileSystem fileSystem, ILoggerFactory loggerFactory)
        {
            _fileSystem = fileSystem;
            _loggerFactory = loggerFactory;
        }
        
        /// <summary>
        /// Creates a standard user manager
        /// </summary>
        public UserManager CreateStandard()
        {
            var logger = _loggerFactory.CreateLogger<UserManager>();
            return new UserManager(_fileSystem, logger);
        }
        
        /// <summary>
        /// Creates an enhanced user manager
        /// </summary>
        public EnhancedUserManager CreateEnhanced()
        {
            var baseLogger = _loggerFactory.CreateLogger<UserManager>();
            var baseManager = new UserManager(_fileSystem, baseLogger);
            
            var enhancedLogger = _loggerFactory.CreateLogger<EnhancedUserManager>();
            return new EnhancedUserManager(baseManager, _fileSystem, enhancedLogger);
        }
    }
}
