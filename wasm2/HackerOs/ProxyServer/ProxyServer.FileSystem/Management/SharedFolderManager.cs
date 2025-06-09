using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProxyServer.FileSystem.Models;

namespace ProxyServer.FileSystem.Management
{
    /// <summary>
    /// Manages shared folder configuration and operations
    /// </summary>
    public class SharedFolderManager
    {
        private readonly ILogger _logger;
        private readonly string _configFilePath;
        private readonly List<SharedFolderInfo> _sharedFolders = new List<SharedFolderInfo>();

        /// <summary>
        /// Event that triggers when shared folders configuration changes
        /// </summary>
        public event EventHandler<EventArgs>? SharedFoldersChanged;

        public SharedFolderManager(ILogger logger, string configDirectory = "")
        {
            _logger = logger;
            
            // Set up configuration directory
            if (string.IsNullOrEmpty(configDirectory))
            {
                configDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ProxyServer");
            }
            
            // Ensure directory exists
            if (!Directory.Exists(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }
            
            _configFilePath = Path.Combine(configDirectory, "sharedfolders.json");
            
            // Load existing configuration
            LoadConfiguration();
        }

        /// <summary>
        /// Gets all configured shared folders
        /// </summary>
        public IReadOnlyList<SharedFolderInfo> GetSharedFolders()
        {
            return _sharedFolders.AsReadOnly();
        }

        /// <summary>
        /// Gets a shared folder by its ID
        /// </summary>
        /// <param name="id">ID of the shared folder</param>
        /// <returns>The shared folder or null if not found</returns>
        public SharedFolderInfo? GetSharedFolder(string id)
        {
            return _sharedFolders.FirstOrDefault(f => f.Id == id);
        }

        /// <summary>
        /// Gets a shared folder by its alias
        /// </summary>
        /// <param name="alias">Alias of the shared folder</param>
        /// <returns>The shared folder or null if not found</returns>
        public SharedFolderInfo? GetSharedFolderByAlias(string alias)
        {
            return _sharedFolders.FirstOrDefault(f => f.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Adds a new shared folder configuration
        /// </summary>
        /// <param name="hostPath">Path to the folder on the host system</param>
        /// <param name="alias">Alias to use for the folder</param>
        /// <param name="permission">Permission level for the folder</param>
        /// <param name="allowedExtensions">Optional list of allowed file extensions</param>
        /// <param name="blockedExtensions">Optional list of blocked file extensions</param>
        /// <returns>The created shared folder information</returns>
        public SharedFolderInfo AddSharedFolder(
            string hostPath, 
            string alias, 
            SharedFolderPermission permission = SharedFolderPermission.ReadOnly,
            List<string>? allowedExtensions = null,
            List<string>? blockedExtensions = null)
        {
            // Validate the path exists
            if (!Directory.Exists(hostPath))
            {
                throw new DirectoryNotFoundException($"The directory {hostPath} does not exist.");
            }
            
            // Verify the alias is unique
            if (_sharedFolders.Any(f => f.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"A shared folder with alias '{alias}' already exists.");
            }
            
            // Create the shared folder record
            var sharedFolder = new SharedFolderInfo
            {
                HostPath = Path.GetFullPath(hostPath),
                Alias = alias,
                Permission = permission,
                AllowedExtensions = allowedExtensions,
                BlockedExtensions = blockedExtensions,
                CreatedAt = DateTime.Now,
                LastAccessed = DateTime.Now
            };
            
            // Add to the collection
            _sharedFolders.Add(sharedFolder);
            
            // Save configuration
            SaveConfiguration();
            
            // Trigger event
            OnSharedFoldersChanged();
            
            _logger.LogInformation("Added new shared folder: {alias} at {hostPath}", alias, hostPath);
            
            return sharedFolder;
        }

        /// <summary>
        /// Updates an existing shared folder configuration
        /// </summary>
        /// <param name="id">ID of the shared folder to update</param>
        /// <param name="alias">New alias (or null to keep existing)</param>
        /// <param name="permission">New permission (or null to keep existing)</param>
        /// <param name="allowedExtensions">New allowed extensions (or null to keep existing)</param>
        /// <param name="blockedExtensions">New blocked extensions (or null to keep existing)</param>
        /// <returns>True if update was successful</returns>
        public bool UpdateSharedFolder(
            string id,
            string? alias = null,
            SharedFolderPermission? permission = null,
            List<string>? allowedExtensions = null,
            List<string>? blockedExtensions = null)
        {
            var sharedFolder = GetSharedFolder(id);
            if (sharedFolder == null)
            {
                _logger.LogWarning("SharedFolderManager", $"Attempted to update non-existent shared folder: {id}");
                return false;
            }
            
            bool changed = false;
            
            // Update alias if specified and unique
            if (!string.IsNullOrEmpty(alias) && alias != sharedFolder.Alias)
            {
                // Check uniqueness
                if (_sharedFolders.Any(f => f.Id != id && f.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException($"A shared folder with alias '{alias}' already exists.");
                }
                
                sharedFolder.Alias = alias;
                changed = true;
            }
            
            // Update permission if specified
            if (permission.HasValue && permission.Value != sharedFolder.Permission)
            {
                sharedFolder.Permission = permission.Value;
                changed = true;
            }
            
            // Update allowed extensions if specified
            if (allowedExtensions != null)
            {
                sharedFolder.AllowedExtensions = allowedExtensions;
                changed = true;
            }
            
            // Update blocked extensions if specified
            if (blockedExtensions != null)
            {
                sharedFolder.BlockedExtensions = blockedExtensions;
                changed = true;
            }
            
            if (changed)
            {
                // Save changes
                SaveConfiguration();
                
                // Trigger event
                OnSharedFoldersChanged();
                
                _logger.LogInformation("Updated shared folder: {id}", id);
            }
            
            return changed;
        }

        /// <summary>
        /// Removes a shared folder configuration
        /// </summary>
        /// <param name="id">ID of the shared folder to remove</param>
        /// <returns>True if removal was successful</returns>
        public bool RemoveSharedFolder(string id)
        {
            var sharedFolder = GetSharedFolder(id);
            if (sharedFolder == null)
            {
                _logger.LogWarning("SharedFolderManager", $"Attempted to remove non-existent shared folder: {id}");
                return false;
            }
            
            // Remove from collection
            bool removed = _sharedFolders.Remove(sharedFolder);
            
            if (removed)
            {
                // Save configuration
                SaveConfiguration();
                
                // Trigger event
                OnSharedFoldersChanged();
                
                _logger.LogInformation("Removed shared folder: {alias} ({id})", sharedFolder.Alias, id);
            }
            
            return removed;
        }

        /// <summary>
        /// Updates the last accessed time for a shared folder
        /// </summary>
        public void UpdateLastAccessed(string id)
        {
            var sharedFolder = GetSharedFolder(id);
            if (sharedFolder != null)
            {
                sharedFolder.LastAccessed = DateTime.Now;
                SaveConfiguration();
            }
        }

        /// <summary>
        /// Saves the shared folders configuration to file
        /// </summary>
        private void SaveConfiguration()
        {
            try
            {
                var json = JsonSerializer.Serialize(_sharedFolders, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                File.WriteAllText(_configFilePath, json);
                
                _logger.LogDebug("SharedFolderManager", "Saved shared folder configuration");
            }
            catch (Exception ex)
            {
                _logger.LogError("SharedFolderManager", 
                    $"Error saving shared folder configuration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads the shared folders configuration from file
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var folders = JsonSerializer.Deserialize<List<SharedFolderInfo>>(json);
                    
                    if (folders != null)
                    {
                        _sharedFolders.Clear();
                        
                        // Only add folders that still exist
                        foreach (var folder in folders.Where(f => Directory.Exists(f.HostPath)))
                        {
                            _sharedFolders.Add(folder);
                        }
                          _logger.LogInformation("Loaded {count} shared folder configurations", _sharedFolders.Count);
                    }
                }
                else
                {
                    _logger.LogInformation("No shared folder configuration found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("SharedFolderManager", 
                    $"Error loading shared folder configuration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Triggers the SharedFoldersChanged event
        /// </summary>
        protected virtual void OnSharedFoldersChanged()
        {
            SharedFoldersChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
