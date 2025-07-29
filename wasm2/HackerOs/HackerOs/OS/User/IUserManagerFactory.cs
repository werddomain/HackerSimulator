using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.User
{
    /// <summary>
    /// Factory interface for creating user managers
    /// </summary>
    public interface IUserManagerFactory
    {
        /// <summary>
        /// Creates a standard user manager
        /// </summary>
        IUserManager CreateStandard();
        
        /// <summary>
        /// Creates an enhanced user manager
        /// </summary>
        IUserManager CreateEnhanced();
    }
    
    /// <summary>
    /// Implementation of IUserManagerFactory
    /// </summary>
    public class DefaultUserManagerFactory : IUserManagerFactory
    {
        private readonly ILogger<UserManager> _logger;
        
        /// <summary>
        /// Creates a new instance of DefaultUserManagerFactory
        /// </summary>
        public DefaultUserManagerFactory(ILogger<UserManager> logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Creates a standard user manager
        /// </summary>
        public IUserManager CreateStandard()
        {
            // Create a basic user manager without file system dependencies for now
            return new UserManager(_logger);
        }
        
        /// <summary>
        /// Creates an enhanced user manager
        /// </summary>
        public IUserManager CreateEnhanced()
        {
            // Create an enhanced user manager without file system dependencies for now
            var baseManager = CreateStandard();
            return baseManager;
        }
    }
}
