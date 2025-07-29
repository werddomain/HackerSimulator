using System;
using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.User
{
    /// <summary>
    /// Factory for creating EnhancedUserManager instances
    /// </summary>
    public static class UserManagerFactory
    {
        /// <summary>
        /// Creates a standard UserManager instance
        /// </summary>
        /// <param name="fileSystem">The file system to use</param>
        /// <param name="logger">The logger to use</param>
        /// <returns>A standard UserManager instance</returns>
        public static UserManager CreateStandard(IVirtualFileSystem fileSystem, ILogger logger)
        {
            return new UserManager(fileSystem, logger);
        }
        
        /// <summary>
        /// Creates an enhanced UserManager with home directory management capabilities
        /// </summary>
        /// <param name="fileSystem">The file system to use</param>
        /// <param name="logger">The logger to use</param>
        /// <returns>An EnhancedUserManager instance</returns>
        public static EnhancedUserManager CreateEnhanced(IVirtualFileSystem fileSystem, ILogger logger)
        {
            var baseLogger = logger.IsGenericLogger() 
                ? logger 
                : LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("UserManager");
                
            var baseManager = new UserManager(fileSystem, baseLogger);
            
            var enhancedLogger = logger.IsGenericLogger() 
                ? logger 
                : LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<EnhancedUserManager>();
                
            return new EnhancedUserManager(baseManager, fileSystem, enhancedLogger);
        }
        
        /// <summary>
        /// Determines if a logger is a generic ILogger that can be used for any type
        /// </summary>
        private static bool IsGenericLogger(this ILogger logger)
        {
            return logger.GetType().Name == "Logger`1" || 
                   logger.GetType().Name == "Logger" || 
                   logger.GetType().IsAssignableFrom(typeof(ILogger<>));
        }
    }
}
