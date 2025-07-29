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
        UserManager CreateStandard();
        
        /// <summary>
        /// Creates an enhanced user manager
        /// </summary>
        EnhancedUserManager CreateEnhanced();
    }
    
    /// <summary>
    /// Implementation of IUserManagerFactory
    /// </summary>
    public class DefaultUserManagerFactory : IUserManagerFactory
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILogger _logger;
        
        /// <summary>
        /// Creates a new instance of DefaultUserManagerFactory
        /// </summary>
        public DefaultUserManagerFactory(IVirtualFileSystem fileSystem, ILogger logger)
        {
            _fileSystem = fileSystem;
            _logger = logger;
        }
        
        /// <summary>
        /// Creates a standard user manager
        /// </summary>
        public UserManager CreateStandard()
        {
            return new UserManager(_fileSystem, _logger);
        }
        
        /// <summary>
        /// Creates an enhanced user manager
        /// </summary>
        public EnhancedUserManager CreateEnhanced()
        {
            var baseManager = new UserManager(_fileSystem, _logger);
            return new EnhancedUserManager(baseManager, _fileSystem, _logger);
        }
    }
}
