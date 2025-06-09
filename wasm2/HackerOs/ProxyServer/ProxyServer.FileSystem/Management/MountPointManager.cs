using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProxyServer.FileSystem.Models;

namespace ProxyServer.FileSystem.Management
{
    /// <summary>
    /// Manages mount points that map shared folders to virtual paths in the HackerOS environment
    /// </summary>
    public class MountPointManager
    {
        private readonly ILogger _logger;
        private readonly SharedFolderManager _sharedFolderManager;
        private readonly string _configFilePath;
        private readonly List<MountPoint> _mountPoints = new List<MountPoint>();

        /// <summary>
        /// Event that triggers when mount points configuration changes
        /// </summary>
        public event EventHandler<EventArgs>? MountPointsChanged;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">Logger for recording operations</param>
        /// <param name="sharedFolderManager">Manager for shared folders</param>
        /// <param name="configDirectory">Directory for configuration files (optional)</param>
        public MountPointManager(ILogger logger, SharedFolderManager sharedFolderManager, string configDirectory = "")
        {
            _logger = logger;
            _sharedFolderManager = sharedFolderManager;
            
            // Register for shared folder changes to update mount points
            _sharedFolderManager.SharedFoldersChanged += OnSharedFoldersChanged;

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
            
            _configFilePath = Path.Combine(configDirectory, "mountpoints.json");
            
            // Load existing configuration
            LoadConfiguration();
        }

        /// <summary>
        /// Gets all active mount points
        /// </summary>
        public IReadOnlyList<MountPoint> GetMountPoints()
        {
            return _mountPoints.Where(mp => mp.IsActive).ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets a mount point by its ID
        /// </summary>
        /// <param name="id">ID of the mount point</param>
        /// <returns>The mount point or null if not found</returns>
        public MountPoint? GetMountPoint(string id)
        {
            return _mountPoints.FirstOrDefault(mp => mp.Id == id && mp.IsActive);
        }

        /// <summary>
        /// Gets a mount point by its virtual path
        /// </summary>
        /// <param name="virtualPath">Virtual path of the mount point</param>
        /// <returns>The mount point or null if not found</returns>
        public MountPoint? GetMountPointByPath(string virtualPath)
        {
            // Normalize the path for comparison
            virtualPath = NormalizeVirtualPath(virtualPath);
            
            return _mountPoints.FirstOrDefault(mp => 
                mp.IsActive && 
                NormalizeVirtualPath(mp.VirtualPath) == virtualPath);
        }

        /// <summary>
        /// Finds the best matching mount point for a given virtual path
        /// </summary>
        /// <param name="virtualPath">The virtual path to find a mount point for</param>
        /// <returns>The best matching mount point or null if none found</returns>
        public MountPoint? FindBestMountPointForPath(string virtualPath)
        {
            // Normalize the path
            virtualPath = NormalizeVirtualPath(virtualPath);
            
            // Get all active mount points, sorted by path length (longest first for most specific match)
            var sortedMountPoints = _mountPoints
                .Where(mp => mp.IsActive)
                .OrderByDescending(mp => mp.VirtualPath.Length);
            
            foreach (var mountPoint in sortedMountPoints)
            {
                var normalizedMountPath = NormalizeVirtualPath(mountPoint.VirtualPath);
                
                if (virtualPath == normalizedMountPath || 
                    virtualPath.StartsWith(normalizedMountPath + "/"))
                {
                    return mountPoint;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Creates a new mount point
        /// </summary>
        /// <param name="sharedFolderId">ID of the shared folder to mount</param>
        /// <param name="virtualPath">Virtual path where the shared folder will be mounted</param>
        /// <param name="options">Mount options (optional)</param>
        /// <returns>The created mount point</returns>
        public MountPoint CreateMountPoint(
            string sharedFolderId,
            string virtualPath,
            MountOptions? options = null)
        {
            // Get the shared folder
            var sharedFolder = _sharedFolderManager.GetSharedFolder(sharedFolderId);
            if (sharedFolder == null)
            {
                throw new ArgumentException($"Shared folder with ID '{sharedFolderId}' does not exist.");
            }
            
            // Normalize the virtual path
            virtualPath = NormalizeVirtualPath(virtualPath);
            
            // Check if there's already a mount point with the same virtual path
            if (_mountPoints.Any(mp => 
                mp.IsActive && 
                NormalizeVirtualPath(mp.VirtualPath) == virtualPath))
            {
                throw new ArgumentException($"A mount point already exists at virtual path '{virtualPath}'.");
            }
            
            // Create the mount point
            var mountPoint = new MountPoint
            {
                VirtualPath = virtualPath,
                SharedFolderId = sharedFolderId,
                Options = options ?? new MountOptions(),
                CreatedAt = DateTime.Now,
                LastAccessed = DateTime.Now,
                SharedFolder = sharedFolder
            };
            
            // Add to collection
            _mountPoints.Add(mountPoint);
            
            // Save configuration
            SaveConfiguration();
            
            // Trigger event
            OnMountPointsChanged();
            
            _logger.LogInformation("Created mount point: {virtualPath} -> {hostPath}", virtualPath, sharedFolder.HostPath);
            
            return mountPoint;
        }

        /// <summary>
        /// Updates an existing mount point
        /// </summary>
        /// <param name="id">ID of the mount point to update</param>
        /// <param name="virtualPath">New virtual path (or null to keep existing)</param>
        /// <param name="options">New mount options (or null to keep existing)</param>
        /// <returns>True if update was successful</returns>
        public bool UpdateMountPoint(
            string id,
            string? virtualPath = null,
            MountOptions? options = null)
        {
            // Get the mount point
            var mountPoint = GetMountPoint(id);
            if (mountPoint == null)
            {
                _logger.LogWarning("MountPointManager", $"Attempted to update non-existent mount point: {id}");
                return false;
            }
            
            bool changed = false;
            
            // Update virtual path if specified
            if (!string.IsNullOrEmpty(virtualPath) && virtualPath != mountPoint.VirtualPath)
            {
                // Normalize the path
                virtualPath = NormalizeVirtualPath(virtualPath);
                
                // Check uniqueness
                if (_mountPoints.Any(mp => 
                    mp.IsActive && 
                    mp.Id != id && 
                    NormalizeVirtualPath(mp.VirtualPath) == virtualPath))
                {
                    throw new ArgumentException($"A mount point already exists at virtual path '{virtualPath}'.");
                }
                
                mountPoint.VirtualPath = virtualPath;
                changed = true;
            }
            
            // Update options if specified
            if (options != null)
            {
                mountPoint.Options = options;
                changed = true;
            }
            
            if (changed)
            {
                // Update last accessed time
                mountPoint.LastAccessed = DateTime.Now;
                
                // Save changes
                SaveConfiguration();
                
                // Trigger event
                OnMountPointsChanged();
                
                _logger.LogInformation("Updated mount point: {id}", id);
            }
            
            return changed;
        }

        /// <summary>
        /// Removes a mount point
        /// </summary>
        /// <param name="id">ID of the mount point to remove</param>
        /// <param name="permanently">If true, permanently removes the mount point; otherwise just deactivates it</param>
        /// <returns>True if removal was successful</returns>
        public bool RemoveMountPoint(string id, bool permanently = false)
        {
            // Get the mount point
            var mountPoint = _mountPoints.FirstOrDefault(mp => mp.Id == id);
            if (mountPoint == null)
            {
                _logger.LogWarning("MountPointManager", $"Attempted to remove non-existent mount point: {id}");
                return false;
            }
            
            bool removed = false;
            
            if (permanently)
            {
                // Remove from collection
                removed = _mountPoints.Remove(mountPoint);
            }
            else
            {
                // Just deactivate
                if (mountPoint.IsActive)
                {
                    mountPoint.IsActive = false;
                    removed = true;
                }
            }
            
            if (removed)
            {
                // Save configuration
                SaveConfiguration();
                
                // Trigger event
                OnMountPointsChanged();
                  _logger.LogInformation(permanently 
                    ? "Permanently removed mount point: {virtualPath} ({id})"
                    : "Deactivated mount point: {virtualPath} ({id})", 
                    mountPoint.VirtualPath, id);
            }
            
            return removed;
        }

        /// <summary>
        /// Resolves a virtual path to a real host path
        /// </summary>
        /// <param name="virtualPath">Virtual path to resolve</param>
        /// <param name="sharedFolder">Out parameter that will contain the shared folder info</param>
        /// <param name="mountPoint">Out parameter that will contain the mount point info</param>
        /// <returns>The resolved path on the host system, or null if it couldn't be resolved</returns>
        public string? ResolveVirtualPath(
            string virtualPath, 
            out SharedFolderInfo? sharedFolder,
            out MountPoint? mountPoint)
        {
            sharedFolder = null;
            mountPoint = null;
            
            // Normalize the path
            virtualPath = NormalizeVirtualPath(virtualPath);
            
            // Find the best matching mount point
            var foundMountPoint = FindBestMountPointForPath(virtualPath);
            if (foundMountPoint == null)
            {
                return null;
            }
            
            // Get the shared folder
            var foundSharedFolder = _sharedFolderManager.GetSharedFolder(foundMountPoint.SharedFolderId);
            if (foundSharedFolder == null || !foundSharedFolder.Exists)
            {
                return null;
            }
            
            // Set the output parameters
            sharedFolder = foundSharedFolder;
            mountPoint = foundMountPoint;
            
            // Calculate the relative path within the mount
            var normalizedMountPath = NormalizeVirtualPath(foundMountPoint.VirtualPath);
            string relativePath = virtualPath == normalizedMountPath 
                ? "" 
                : virtualPath.Substring(normalizedMountPath.Length + 1);  // +1 for the trailing slash
            
            // Combine with host path
            string hostPath = Path.Combine(foundSharedFolder.HostPath, relativePath);
            
            // Update last accessed time
            UpdateLastAccessed(foundMountPoint.Id);
            
            return hostPath;
        }

        /// <summary>
        /// Updates the last accessed time for a mount point
        /// </summary>
        public void UpdateLastAccessed(string id)
        {
            var mountPoint = _mountPoints.FirstOrDefault(mp => mp.Id == id);
            if (mountPoint != null)
            {
                mountPoint.LastAccessed = DateTime.Now;
                SaveConfiguration();
                
                // Also update the associated shared folder
                _sharedFolderManager.UpdateLastAccessed(mountPoint.SharedFolderId);
            }
        }
        
        /// <summary>
        /// Normalizes a virtual path for consistent comparison
        /// </summary>
        /// <param name="path">The path to normalize</param>
        /// <returns>Normalized path</returns>
        private string NormalizeVirtualPath(string path)
        {
            // Trim whitespace
            path = path.Trim();
            
            // Ensure path starts with /
            if (!path.StartsWith("/"))
            {
                path = "/" + path;
            }
            
            // Remove trailing slash if present (unless root path)
            if (path.Length > 1 && path.EndsWith("/"))
            {
                path = path.TrimEnd('/');
            }
            
            return path;
        }
        
        /// <summary>
        /// Refreshes all mount points based on current shared folder configuration
        /// </summary>
        private void RefreshMountPoints()
        {
            var sharedFolders = _sharedFolderManager.GetSharedFolders();
            var sharedFolderIds = sharedFolders.Select(sf => sf.Id).ToHashSet();
            
            foreach (var mountPoint in _mountPoints)
            {
                // Update shared folder reference
                mountPoint.SharedFolder = sharedFolderIds.Contains(mountPoint.SharedFolderId) 
                    ? _sharedFolderManager.GetSharedFolder(mountPoint.SharedFolderId)
                    : null;
                
                // Deactivate mount points with missing shared folders
                if (mountPoint.SharedFolder == null && mountPoint.IsActive)
                {
                    mountPoint.IsActive = false;
                    _logger.LogWarning("MountPointManager", 
                        $"Deactivated mount point {mountPoint.VirtualPath} because shared folder {mountPoint.SharedFolderId} no longer exists");
                }
            }
            
            SaveConfiguration();
        }

        /// <summary>
        /// Handler for shared folder changes
        /// </summary>
        private void OnSharedFoldersChanged(object? sender, EventArgs e)
        {
            RefreshMountPoints();
            OnMountPointsChanged();
        }

        /// <summary>
        /// Saves the mount points configuration to file
        /// </summary>
        private void SaveConfiguration()
        {
            try
            {
                var json = JsonSerializer.Serialize(_mountPoints, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                File.WriteAllText(_configFilePath, json);
                
                _logger.LogDebug("MountPointManager", "Saved mount point configuration");
            }
            catch (Exception ex)
            {
                _logger.LogError("MountPointManager", 
                    $"Error saving mount point configuration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads the mount points configuration from file
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var mountPoints = JsonSerializer.Deserialize<List<MountPoint>>(json);
                    
                    if (mountPoints != null)
                    {
                        _mountPoints.Clear();
                        _mountPoints.AddRange(mountPoints);
                        
                        // Refresh mount points to update shared folder references
                        RefreshMountPoints();
                          _logger.LogInformation("Loaded {count} active mount points", _mountPoints.Count(mp => mp.IsActive));
                    }
                }
                else
                {
                    _logger.LogInformation("No mount point configuration found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("MountPointManager", 
                    $"Error loading mount point configuration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Triggers the MountPointsChanged event
        /// </summary>
        protected virtual void OnMountPointsChanged()
        {
            MountPointsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
