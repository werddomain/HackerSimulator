using System;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.IO
{
    /// <summary>
    /// Provides umask support for file permission inheritance and creation
    /// </summary>
    public class UmaskManager
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILogger<UmaskManager> _logger;
        private readonly string _umaskFilePath = "/etc/umask";
        private readonly object _lock = new object();
        
        // Default umask (022 - removes write permission for group and others)
        private const int DefaultUmask = 0022;
        
        // Dictionary of user-specific umasks
        private readonly Dictionary<string, int> _userUmasks = new Dictionary<string, int>();
        
        /// <summary>
        /// Initializes a new instance of the UmaskManager
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="logger">The logger</param>
        public UmaskManager(IVirtualFileSystem fileSystem, ILogger<UmaskManager> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Initializes the umask manager by loading umask data
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing UmaskManager");
                await LoadUmasksAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing UmaskManager");
            }
        }
        
        /// <summary>
        /// Gets the umask for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <returns>The umask value</returns>
        public int GetUmask(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            lock (_lock)
            {
                return _userUmasks.TryGetValue(username, out var umask) ? umask : DefaultUmask;
            }
        }
        
        /// <summary>
        /// Sets the umask for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="umaskValue">The umask value</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SetUmaskAsync(string username, int umaskValue)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            if (umaskValue < 0 || umaskValue > 0777)
            {
                throw new ArgumentException("Invalid umask value", nameof(umaskValue));
            }
            
            try
            {
                _logger.LogInformation("Setting umask for user {Username} to {Umask:000}", username, umaskValue);
                
                lock (_lock)
                {
                    _userUmasks[username] = umaskValue;
                }
                
                await SaveUmasksAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting umask for user {Username}", username);
                return false;
            }
        }
        
        /// <summary>
        /// Applies the umask to a permission value
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="permissions">The original permissions</param>
        /// <returns>The permissions after applying umask</returns>
        public int ApplyUmask(string username, int permissions)
        {
            int umask = GetUmask(username);
            
            // Apply umask by inverting bits and ANDing
            return permissions & ~umask;
        }
        
        /// <summary>
        /// Applies the umask to a file permission object
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="permissions">The original permissions</param>
        /// <returns>The permissions after applying umask</returns>
        public FilePermissions ApplyUmask(string username, FilePermissions permissions)
        {
            int umask = GetUmask(username);
            int originalOctal = permissions.ToOctal();
            int maskedOctal = originalOctal & ~umask;
            
            return FilePermissions.FromOctal(maskedOctal);
        }
        
        /// <summary>
        /// Calculates effective permissions for new file creation
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="isDirectory">Whether this is a directory or file</param>
        /// <returns>The effective permissions</returns>
        public int GetEffectivePermissionsForCreation(string username, bool isDirectory)
        {
            // Base permissions: files 666 (rw-rw-rw-), directories 777 (rwxrwxrwx)
            int basePermissions = isDirectory ? 0777 : 0666;
            
            // Apply umask
            return ApplyUmask(username, basePermissions);
        }
        
        /// <summary>
        /// Resets a user's umask to the default value
        /// </summary>
        /// <param name="username">The username</param>
        /// <returns>True if successful</returns>
        public async Task<bool> ResetUmaskAsync(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            try
            {
                _logger.LogInformation("Resetting umask for user {Username} to default", username);
                
                lock (_lock)
                {
                    _userUmasks.Remove(username);
                }
                
                await SaveUmasksAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting umask for user {Username}", username);
                return false;
            }
        }
        
        /// <summary>
        /// Sets the default umask for all new users
        /// </summary>
        /// <param name="umaskValue">The umask value</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SetDefaultUmaskAsync(int umaskValue)
        {
            if (umaskValue < 0 || umaskValue > 0777)
            {
                throw new ArgumentException("Invalid umask value", nameof(umaskValue));
            }
            
            try
            {
                _logger.LogInformation("Setting default umask to {Umask:000}", umaskValue);
                
                // Set for special "default" user
                lock (_lock)
                {
                    _userUmasks["default"] = umaskValue;
                }
                
                await SaveUmasksAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default umask");
                return false;
            }
        }
        
        /// <summary>
        /// Gets the default umask for new users
        /// </summary>
        /// <returns>The default umask value</returns>
        public int GetDefaultUmask()
        {
            lock (_lock)
            {
                return _userUmasks.TryGetValue("default", out var umask) ? umask : DefaultUmask;
            }
        }
        
        /// <summary>
        /// Loads umask data from the umask file
        /// </summary>
        private async Task LoadUmasksAsync()
        {
            try
            {
                _logger.LogDebug("Loading umasks from {UmaskFilePath}", _umaskFilePath);
                
                // Clear existing umasks
                lock (_lock)
                {
                    _userUmasks.Clear();
                }
                
                // Check if file exists
                if (!await _fileSystem.FileExistsAsync(_umaskFilePath))
                {
                    _logger.LogInformation("Umask file not found, creating default");
                    
                    // Set default umask
                    lock (_lock)
                    {
                        _userUmasks["default"] = DefaultUmask;
                    }
                    
                    await SaveUmasksAsync();
                    return;
                }
                
                // Read file content
                string content = await _fileSystem.ReadFileAsync(_umaskFilePath);
                
                // Parse lines
                string[] lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    // Skip comments
                    if (line.StartsWith("#"))
                    {
                        continue;
                    }
                    
                    // Parse umask entry: username:umask
                    string[] parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        string username = parts[0];
                        
                        if (int.TryParse(parts[1], out int umaskValue))
                        {
                            lock (_lock)
                            {
                                _userUmasks[username] = umaskValue;
                            }
                        }
                    }
                }
                
                // Ensure default umask exists
                lock (_lock)
                {
                    if (!_userUmasks.ContainsKey("default"))
                    {
                        _userUmasks["default"] = DefaultUmask;
                    }
                }
                
                _logger.LogInformation("Loaded {Count} umask entries", _userUmasks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading umasks");
            }
        }
        
        /// <summary>
        /// Saves umask data to the umask file
        /// </summary>
        private async Task SaveUmasksAsync()
        {
            try
            {
                _logger.LogDebug("Saving umasks to {UmaskFilePath}", _umaskFilePath);
                
                // Build file content
                string content = "# User umask file - username:umask\n";
                content += "# Generated on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n";
                
                lock (_lock)
                {
                    foreach (var (username, umask) in _userUmasks)
                    {
                        content += $"{username}:{umask:000}\n";
                    }
                }
                
                // Ensure /etc directory exists
                await _fileSystem.CreateDirectoryAsync("/etc");
                
                // Write to file
                await _fileSystem.WriteFileAsync(_umaskFilePath, content);
                
                _logger.LogInformation("Saved umask entries");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving umasks");
            }
        }
        
        /// <summary>
        /// Converts an umask value to a string representation
        /// </summary>
        /// <param name="umask">The umask value</param>
        /// <returns>The string representation</returns>
        public static string UmaskToString(int umask)
        {
            return $"{umask:000}";
        }
        
        /// <summary>
        /// Gets a human-readable description of an umask value
        /// </summary>
        /// <param name="umask">The umask value</param>
        /// <returns>A human-readable description</returns>
        public static string GetUmaskDescription(int umask)
        {
            string result = "Removes ";
            
            // Owner permissions
            if ((umask & 0400) != 0) result += "owner read, ";
            if ((umask & 0200) != 0) result += "owner write, ";
            if ((umask & 0100) != 0) result += "owner execute, ";
            
            // Group permissions
            if ((umask & 0040) != 0) result += "group read, ";
            if ((umask & 0020) != 0) result += "group write, ";
            if ((umask & 0010) != 0) result += "group execute, ";
            
            // Other permissions
            if ((umask & 0004) != 0) result += "other read, ";
            if ((umask & 0002) != 0) result += "other write, ";
            if ((umask & 0001) != 0) result += "other execute, ";
            
            // Trim trailing comma and space
            if (result.EndsWith(", "))
            {
                result = result.Substring(0, result.Length - 2);
            }
            
            // Check if anything is being removed
            if (result == "Removes ")
            {
                result = "No permissions removed";
            }
            
            return result;
        }
    }
}
