using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using HackerOs.OS.System.IO;
using HackerOs.OS.User;
using Path = HackerOs.OS.System.IO.Path;
using UserEntity = HackerOs.OS.User.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Main implementation of the virtual file system.
    /// Provides a complete Linux-like file system simulation with persistence.
    /// </summary>    
    public partial class VirtualFileSystem : IVirtualFileSystem
    {
        private readonly ILogger<VirtualFileSystem>? _logger;
        private VirtualDirectory _rootDirectory;
        private readonly Dictionary<string, VirtualFileSystemNode> _pathCache;
        private readonly object _lockObject = new object();
        private bool _isInitialized = false;
        
        // Linux-style current working directory and user context
        private string _currentWorkingDirectory = "/";
        private string _currentUser = "root";
        private readonly Dictionary<string, string> _environmentVariables = new();

        // Persistence layer
        private IndexedDBStorage? _storage;
        private bool _persistenceEnabled = false;

        // Mount manager for handling mount points
        private readonly MountManager _mountManager;

        /// <summary>
        /// Event fired when file system operations occur.
        /// </summary>
        public event EventHandler<FileSystemEvent>? OnFileSystemEvent;

        /// <summary>
        /// Event triggered when the file system changes (file created, deleted, modified, etc.)
        /// </summary>
        public event EventHandler<FileSystemEventArgs>? FileSystemChanged;

        /// <summary>
        /// Gets the root directory of the file system.
        /// </summary>
        public VirtualDirectory RootDirectory => _rootDirectory;

        /// <summary>
        /// Gets or sets the current working directory for relative path resolution.
        /// </summary>
        public string CurrentWorkingDirectory
        {
            get => _currentWorkingDirectory;
            set => _currentWorkingDirectory = NormalizePath(value ?? "/");
        }        /// <summary>
        /// Gets or sets the current user for tilde expansion and permissions.
        /// </summary>
        public string CurrentUser
        {
            get => _currentUser;
            set => _currentUser = value ?? "root";
        }

        /// <summary>
        /// Gets the mount manager for handling mount operations.
        /// </summary>
        public MountManager MountManager => _mountManager;/// <summary>
        /// Initializes a new instance of the VirtualFileSystem class.
        /// </summary>
        public VirtualFileSystem()
        {
            _pathCache = new Dictionary<string, VirtualFileSystemNode>();
            _rootDirectory = new VirtualDirectory("", "/");
            _pathCache["/"] = _rootDirectory;
            _currentWorkingDirectory = "/";
            _currentUser = "root";
            _mountManager = new MountManager();
        }

        /// <summary>
        /// Initializes the virtual file system and creates the standard Unix directory structure.
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            if (_isInitialized)
                return true;

            try
            {
                lock (_lockObject)
                {
                    // Create standard Unix directory structure
                    CreateStandardDirectoryStructure();
                    
                    // Add root to cache
                    _pathCache["/"] = _rootDirectory;
                    
                    _isInitialized = true;
                }

                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.SystemInitialized,
                    Path = "/",
                    Timestamp = DateTime.UtcNow
                });

                return true;
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = "/",
                    Message = $"Failed to initialize file system: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
                return false;
            }
        }

        /// <summary>
        /// Gets a file system node at the specified path.
        /// </summary>
        /// <param name="path">The absolute or relative path to the node.</param>
        /// <returns>The file system node if found; otherwise, null.</returns>
        public async Task<VirtualFileSystemNode?> GetNodeAsync(string path)
        {
            await Task.CompletedTask; // Make it async for interface compliance
            return GetNode(path);
        }

        /// <summary>
        /// Creates a new file at the specified path.
        /// </summary>
        /// <param name="path">The path where the file should be created.</param>
        /// <param name="content">Optional initial content for the file.</param>
        /// <returns>True if the file was created successfully; otherwise, false.</returns>
        public async Task<bool> CreateFileAsync(string path, byte[]? content = null)
        {
            await Task.CompletedTask; // Make it async for interface compliance
            
            try
            {                var normalizedPath = NormalizePath(path);
                var parentPath = HackerOs.OS.System.IO.Path.GetDirectoryName(normalizedPath);
                
                if (string.IsNullOrEmpty(parentPath))
                    parentPath = "/";
                
                // Check if parent directory exists
                var parentNode = GetNode(parentPath);
                if (parentNode == null || !parentNode.IsDirectory)
                {
                    return false;
                }
                  // Check if file already exists
                if (GetNode(normalizedPath) != null)
                {
                    return false; // File already exists
                }                
                var fileName = HackerOs.OS.System.IO.Path.GetFileName(normalizedPath);
                var file = new VirtualFile(fileName, normalizedPath);
                
                if (content != null)
                {
                    file.SetContent(content);
                }
                
                var parentDir = (VirtualDirectory)parentNode;
                parentDir.AddChild(file);
                
                // Add to cache
                _pathCache[normalizedPath] = file;
                
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.FileCreated,
                    Path = normalizedPath,
                    Timestamp = DateTime.UtcNow
                });
                
                FileSystemChanged?.Invoke(this, CreateFileSystemEventArgs(FileSystemEventType.FileCreated, normalizedPath));

                return true;
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = path,
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
                return false;
            }
        }

        /// <summary>
        /// Creates a new directory at the specified path.
        /// </summary>
        /// <param name="path">The path where the directory should be created.</param>
        /// <returns>True if the directory was created successfully; otherwise, false.</returns>
        public async Task<bool> CreateDirectoryAsync(string path)
        {
            await Task.CompletedTask; // Make it async for interface compliance
            return CreateDirectory(path);
        }

        /// <summary>
        /// Deletes a file or directory at the specified path.
        /// </summary>
        /// <param name="path">The path to the item to delete.</param>
        /// <param name="recursive">Whether to delete directories recursively.</param>
        /// <returns>True if the item was deleted successfully; otherwise, false.</returns>
        public async Task<bool> DeleteAsync(string path, bool recursive = false)
        {
            await Task.CompletedTask; // Make it async for interface compliance
            return Delete(path, recursive);
        }

        /// <summary>
        /// Checks if a file or directory exists at the specified path.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>True if the item exists; otherwise, false.</returns>
        public async Task<bool> ExistsAsync(string path)
        {
            await Task.CompletedTask; // Make it async for interface compliance
            return GetNode(path) != null;
        }

        /// <summary>
        /// Lists the contents of a directory.
        /// </summary>
        /// <param name="path">The path to the directory.</param>
        /// <returns>A collection of file system nodes in the directory.</returns>
        public async Task<IEnumerable<VirtualFileSystemNode>> ListDirectoryAsync(string path)
        {
            await Task.CompletedTask; // Make it async for interface compliance
            
            var node = GetNode(path);
            if (node == null || !node.IsDirectory)
            {
                return new List<VirtualFileSystemNode>();
            }
            
            var directory = (VirtualDirectory)node;
            return directory.Children.Values;
        }        /// <summary>
        /// Reads the content of a file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>The file content as bytes; null if the file doesn't exist or is not a file.</returns>
        public async Task<byte[]?> ReadFileAsync(string path)
        {
            await Task.CompletedTask; // Make it async for interface compliance
            
            var node = GetNode(path);
            if (node == null || node.IsDirectory)
            {
                return null;
            }
            
            var file = (VirtualFile)node;
            
            // Handle symbolic links by resolving to target
            if (file.IsSymbolicLink && !string.IsNullOrEmpty(file.SymbolicLinkTarget))
            {
                // Resolve symbolic link to target file
                var targetNode = GetNode(file.SymbolicLinkTarget);
                if (targetNode != null && !targetNode.IsDirectory)
                {
                    var targetFile = (VirtualFile)targetNode;
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.FileRead,
                        Path = path,
                        TargetPath = file.SymbolicLinkTarget,
                        Timestamp = DateTime.UtcNow
                    });
                    return targetFile.GetContent();
                }
                return null; // Broken symbolic link
            }
            
            FireFileSystemEvent(new FileSystemEvent
            {
                EventType = FileSystemEventType.FileRead,
                Path = path,
                Timestamp = DateTime.UtcNow
            });
            
            return file.GetContent();
        }

        /// <summary>
        /// Reads the content of a file with user permission checking.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="user">The user reading the file.</param>
        /// <returns>The file content as bytes; null if the file doesn't exist, is not a file, or user lacks permission.</returns>
        public async Task<byte[]?> ReadFileAsync(string path, HackerOs.OS.User.User user)
        {
            try
            {
                var absolutePath = GetAbsolutePath(path, _currentWorkingDirectory);
                
                // Check if the file exists
                var node = GetNode(absolutePath);
                if (node == null)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"File not found: {absolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return null;
                }
                
                // Ensure it's a file
                if (node.IsDirectory)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"Cannot read a directory as a file: {absolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return null;
                }
                
                // Check read permission using our comprehensive permission check
                bool hasAccess = await CheckAccessAsync(absolutePath, user, FileAccessMode.Read);
                if (!hasAccess)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.PermissionDenied,
                        Path = absolutePath,
                        Message = $"Permission denied: {user.Username} cannot read {absolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return null;
                }
                
                // User has permission, read the file content
                var file = (VirtualFile)node;
                
                // Handle symbolic links by resolving to target
                if (file.IsSymbolicLink && file.SymbolicLinkTarget != null)
                {
                    return await ReadFileAsync(file.SymbolicLinkTarget, user);
                }
                
                // Update access time
                file.UpdateAccessTime();
                
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.FileRead,
                    Path = absolutePath,
                    Message = $"File read by {user.Username}",
                    Timestamp = DateTime.UtcNow
                });
                
                return file.Content;
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = path,
                    Message = $"Error reading file: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
                
                return null;
            }
        }

        /// <summary>
        /// Writes content to a file, creating it if it doesn't exist.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="content">The content to write.</param>
        /// <returns>True if the content was written successfully; otherwise, false.</returns>
        public async Task<bool> WriteFileAsync(string path, byte[] content)
        {
            await Task.CompletedTask; // Make it async for interface compliance
            
            try
            {
                var normalizedPath = NormalizePath(path);
                var node = GetNode(normalizedPath);
                
                if (node != null)
                {
                    // File exists, update content
                    if (node.IsDirectory)
                    {
                        return false; // Cannot write to directory
                    }
                    
                    var file = (VirtualFile)node;
                    file.SetContent(content);
                    
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.FileWritten,
                        Path = normalizedPath,
                        Timestamp = DateTime.UtcNow
                    });
                    
                    return true;
                }
                else
                {
                    // File doesn't exist, create it
                    return await CreateFileAsync(normalizedPath, content);
                }
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = path,
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
                return false;
            }
        }

        /// <summary>
        /// Moves a file or directory from source to destination.
        /// </summary>
        /// <param name="sourcePath">The current path of the item.</param>
        /// <param name="destinationPath">The new path for the item.</param>
        /// <returns>True if the item was moved successfully; otherwise, false.</returns>
        public async Task<bool> MoveAsync(string sourcePath, string destinationPath)
        {
            // Call the user-aware version with root user (which bypasses permission checks)
            var rootUser = new HackerOs.OS.User.User { UserId = 0, Username = "root", PrimaryGroupId = 0 };
            return await MoveAsync(sourcePath, destinationPath, rootUser);
        }

        /// <summary>
        /// Copies a file or directory from source to destination.
        /// </summary>
        /// <param name="sourcePath">The path of the item to copy.</param>
        /// <param name="destinationPath">The destination path for the copy.</param>
        /// <returns>True if the item was copied successfully; otherwise, false.</returns>
        public async Task<bool> CopyAsync(string sourcePath, string destinationPath)
        {
            await Task.CompletedTask; // Make it async for interface compliance
            return Copy(sourcePath, destinationPath);
        }        /// <summary>
        /// Lists the contents of a directory with extended information asynchronously.
        /// </summary>
        /// <param name="path">The path to the directory.</param>
        /// <returns>A collection of file system nodes in the directory.</returns>
        public async Task<IEnumerable<VirtualFileSystemNode>> GetDirectoryContentsAsync(string path)
        {
            // Simply delegate to the existing ListDirectoryAsync method
            return await ListDirectoryAsync(path);
        }

        /// <summary>
        /// Creates a symbolic link at the specified path pointing to the target.
        /// </summary>
        /// <param name="linkPath">The path where the symbolic link should be created.</param>
        /// <param name="targetPath">The path the symbolic link should point to.</param>
        /// <returns>True if the symbolic link was created successfully; otherwise, false.</returns>
        public async Task<bool> CreateSymbolicLinkAsync(string linkPath, string targetPath)
        {
            await Task.CompletedTask; // Make it async for interface compliance
            return CreateSymbolicLink(linkPath, targetPath);
        }

        /// <summary>
        /// Changes the current working directory.
        /// </summary>
        /// <param name="path">The new working directory path</param>
        /// <returns>True if the directory change was successful, false otherwise</returns>
        public async Task<bool> ChangeDirectoryAsync(string path)
        {
            await Task.CompletedTask; // Make it async for interface compliance
            
            var normalizedPath = NormalizePath(path);
            var node = GetNode(normalizedPath);
            
            if (node != null && node.IsDirectory)
            {
                CurrentWorkingDirectory = normalizedPath;
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Gets the current working directory path.
        /// </summary>
        /// <returns>The current working directory path</returns>
        public string GetCurrentDirectory()
        {
            return CurrentWorkingDirectory;
        }        /// <summary>
        /// Saves the current file system state to persistent storage.
        /// </summary>
        /// <returns>True if save was successful, false otherwise</returns>
        public async Task<bool> SaveToPersistentStorageAsync()
        {
            if (_storage != null && _persistenceEnabled)
            {
                return await _storage.SaveFileSystemAsync(_rootDirectory);
            }
            return true; // Succeed if persistence not enabled
        }

        /// <summary>
        /// Loads the file system state from persistent storage.
        /// </summary>
        /// <returns>True if load was successful, false otherwise</returns>
        public async Task<bool> LoadFromPersistentStorageAsync()
        {
            if (_storage != null && _persistenceEnabled)
            {
                var loadedRoot = await _storage.LoadFileSystemAsync();
                if (loadedRoot != null)
                {
                    _rootDirectory = loadedRoot;
                    // Rebuild cache
                    _pathCache.Clear();
                    RebuildCache(_rootDirectory, "/");
                    return true;
                }
                return false;
            }
            return true; // Succeed if persistence not enabled
        }

        /// <summary>
        /// Mounts a file system at the specified path.
        /// </summary>
        /// <param name="source">The source or device to mount</param>
        /// <param name="mountPath">The path where the file system should be mounted</param>
        /// <param name="fileSystemType">The type of the file system</param>
        /// <param name="options">Mount options</param>
        /// <returns>True if the mount was successful; otherwise, false</returns>
        public async Task<bool> MountAsync(string source, string mountPath, string fileSystemType, MountOptions? options = null)
        {
            await Task.CompletedTask; // Make it async for interface compliance
            return Mount(source, mountPath, fileSystemType, options);
        }

        /// <summary>
        /// Unmounts a file system from the specified path.
        /// </summary>
        /// <param name="mountPath">The path to unmount</param>
        /// <param name="force">Whether to force unmount even if busy</param>
        /// <returns>True if the unmount was successful; otherwise, false</returns>
        public bool Unmount(string mountPath, bool force = false)
        {
            return _mountManager.Unmount(mountPath, force);
        }

        /// <summary>
        /// Gets information about all mount points.
        /// </summary>
        /// <returns>A string containing mount information similar to /proc/mounts</returns>
        public string GetMountInfo()
        {
            return _mountManager.GetMountInfo();
        }

        #region Helper Methods

        /// <summary>
        /// Normalizes a path to ensure consistent formatting and resolves relative paths.
        /// </summary>
        /// <param name="path">The path to normalize</param>
        /// <returns>The normalized absolute path</returns>
        private string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "/";

            // Handle tilde expansion
            if (path.StartsWith("~"))
            {
                var homeDir = $"/home/{_currentUser}";
                path = path.Length == 1 ? homeDir : path.Substring(1).TrimStart('/');
                path = homeDir + "/" + path;
            }            // Convert relative paths to absolute
            if (!path.StartsWith("/"))            {
                path = HackerOs.OS.System.IO.Path.Combine(_currentWorkingDirectory, path).Replace('\\', '/');
            }

            // Clean up the path
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var normalizedParts = new List<string>();

            foreach (var part in parts)
            {
                if (part == ".")
                {
                    // Current directory - ignore
                    continue;
                }
                else if (part == "..")
                {
                    // Parent directory
                    if (normalizedParts.Count > 0)
                    {
                        normalizedParts.RemoveAt(normalizedParts.Count - 1);
                    }
                }
                else
                {
                    normalizedParts.Add(part);
                }
            }

            var result = "/" + string.Join("/", normalizedParts);
            return result == "" ? "/" : result;
        }

        /// <summary>
        /// Gets a file system node at the specified path (internal synchronous version).
        /// </summary>
        /// <param name="path">The path to the node</param>
        /// <returns>The node if found, null otherwise</returns>
        private VirtualFileSystemNode? GetNode(string path)
        {
            var normalizedPath = NormalizePath(path);
            
            // Check cache first
            if (_pathCache.TryGetValue(normalizedPath, out var cachedNode))
            {
                return cachedNode;
            }

            // Walk the directory tree
            var parts = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            VirtualFileSystemNode current = _rootDirectory;

            foreach (var part in parts)
            {
                if (current is VirtualDirectory directory)
                {
                    if (directory.Children.TryGetValue(part, out var child))
                    {
                        current = child;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null; // Cannot traverse through a file
                }
            }

            // Update cache
            _pathCache[normalizedPath] = current;
            return current;
        }

        /// <summary>
        /// Fires a file system event.
        /// </summary>
        /// <param name="eventArgs">The event arguments</param>
        private void FireFileSystemEvent(FileSystemEvent eventArgs)
        {
            OnFileSystemEvent?.Invoke(this, eventArgs);
        }        /// <summary>
        /// Creates the standard Unix directory structure.
        /// </summary>
        private void CreateStandardDirectoryStructure()
        {
            // Create standard directories synchronously
            var standardDirectories = new[]
            {
                "/bin", "/boot", "/dev", "/etc", "/etc/systemd", "/home", "/lib", "/lib64",
                "/media", "/mnt", "/opt", "/proc", "/root", "/run", "/sbin", "/srv", "/sys",
                "/tmp", "/usr", "/usr/bin", "/usr/include", "/usr/lib", "/usr/lib64",
                "/usr/local", "/usr/local/bin", "/usr/local/lib", "/usr/sbin", "/usr/share",
                "/usr/src", "/var", "/var/cache", "/var/lib", "/var/lock", "/var/log",
                "/var/mail", "/var/run", "/var/spool", "/var/tmp", "/var/www", "/var/www/html"
            };

            foreach (var dir in standardDirectories)
            {
                CreateDirectory(dir);
            }
        }        /// <summary>
        /// Creates a directory at the specified path (internal synchronous version).
        /// </summary>
        /// <param name="path">The path where the directory should be created</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool CreateDirectory(string path)
        {
            try
            {
                var normalizedPath = NormalizePath(path);
                
                // Check if directory already exists
                if (GetNode(normalizedPath) != null)
                {
                    return true; // Directory already exists, consider it success
                }                // Create parent directories recursively if they don't exist
                var parentPath = HackerOs.OS.System.IO.Path.GetDirectoryName(normalizedPath);
                if (!string.IsNullOrEmpty(parentPath) && parentPath != "/")
                {
                    // Ensure parent directory exists
                    if (!CreateDirectory(parentPath))
                    {
                        return false;
                    }
                }

                var parentNode = GetNode(parentPath ?? "/");
                if (parentNode == null || !parentNode.IsDirectory)                {
                    return false;
                }

                var directoryName = HackerOs.OS.System.IO.Path.GetFileName(normalizedPath);
                var directory = new VirtualDirectory(directoryName, normalizedPath);

                var parentDir = (VirtualDirectory)parentNode;
                parentDir.AddChild(directory);

                // Add to cache
                _pathCache[normalizedPath] = directory;

                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.DirectoryCreated,
                    Path = normalizedPath,
                    Timestamp = DateTime.UtcNow
                });

                FileSystemChanged?.Invoke(this, CreateFileSystemEventArgs(FileSystemEventType.DirectoryCreated, normalizedPath));

                return true;
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = path,
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
                return false;
            }
        }

        /// <summary>
        /// Deletes a file or directory (internal synchronous version).
        /// </summary>
        /// <param name="path">The path to delete</param>
        /// <param name="recursive">Whether to delete recursively</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool Delete(string path, bool recursive = false)
        {
            try
            {
                var normalizedPath = NormalizePath(path);
                var node = GetNode(normalizedPath);

                if (node == null)
                {
                    return false; // Node doesn't exist
                }                // Cannot delete root directory
                if (normalizedPath == "/")
                {
                    return false;                }

                var parentPath = HackerOs.OS.System.IO.Path.GetDirectoryName(normalizedPath);
                if (string.IsNullOrEmpty(parentPath))
                    parentPath = "/";

                var parentNode = GetNode(parentPath);
                if (parentNode == null || !parentNode.IsDirectory)
                {
                    return false;
                }

                var parentDir = (VirtualDirectory)parentNode;

                // Check if it's a directory with children and not recursive
                if (node.IsDirectory && !recursive)
                {
                    var directory = (VirtualDirectory)node;
                    if (directory.Children.Count > 0)
                    {
                        return false; // Directory not empty and not recursive
                    }
                }

                // Remove from parent
                parentDir.RemoveChild(node.Name);

                // Remove from cache (and all children if directory)
                RemoveFromCache(normalizedPath, node);

                var eventType = node.IsDirectory ? FileSystemEventType.DirectoryDeleted : FileSystemEventType.FileDeleted;
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = eventType,
                    Path = normalizedPath,
                    Timestamp = DateTime.UtcNow
                });

                FileSystemChanged?.Invoke(this, CreateFileSystemEventArgs(eventType, normalizedPath));

                return true;
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = path,
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
                return false;
            }
        }

        /// <summary>
        /// Removes a node and all its children from the cache.
        /// </summary>
        /// <param name="path">The path to remove</param>
        /// <param name="node">The node to remove</param>
        private void RemoveFromCache(string path, VirtualFileSystemNode node)
        {
            _pathCache.Remove(path);

            if (node.IsDirectory)
            {                var directory = (VirtualDirectory)node;
                foreach (var child in directory.Children.Values)                {
                    var childPath = HackerOs.OS.System.IO.Path.Combine(path, child.Name).Replace('\\', '/');
                    RemoveFromCache(childPath, child);
                }
            }
        }

        /// <summary>
        /// Copies a file or directory (internal synchronous version).
        /// </summary>
        /// <param name="sourcePath">The source path</param>
        /// <param name="destinationPath">The destination path</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool Copy(string sourcePath, string destinationPath)
        {
            try
            {
                var normalizedSourcePath = NormalizePath(sourcePath);
                var normalizedDestPath = NormalizePath(destinationPath);

                var sourceNode = GetNode(normalizedSourcePath);
                if (sourceNode == null)
                {
                    return false; // Source doesn't exist
                }

                // Check if destination already exists
                if (GetNode(normalizedDestPath) != null)
                {
                    return false; // Destination already exists
                }

                if (sourceNode.IsDirectory)
                {
                    return CopyDirectory(normalizedSourcePath, normalizedDestPath);
                }
                else
                {
                    return CopyFile(normalizedSourcePath, normalizedDestPath);
                }
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = sourcePath,
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
                return false;
            }
        }

        /// <summary>
        /// Copies a file.
        /// </summary>
        /// <param name="sourcePath">The source file path</param>
        /// <param name="destinationPath">The destination file path</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool CopyFile(string sourcePath, string destinationPath)
        {
            var sourceFile = (VirtualFile)GetNode(sourcePath)!;
            var content = sourceFile.GetContent();

            if (CreateFileAsync(destinationPath, content).Result)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.FileCopied,
                    Path = destinationPath,
                    SourcePath = sourcePath,
                    Timestamp = DateTime.UtcNow
                });
                return true;
            }

            return false;
        }

        /// <summary>
        /// Copies a directory recursively.
        /// </summary>
        /// <param name="sourcePath">The source directory path</param>
        /// <param name="destinationPath">The destination directory path</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool CopyDirectory(string sourcePath, string destinationPath)
        {
            if (!CreateDirectory(destinationPath))
            {
                return false;
            }            var sourceDir = (VirtualDirectory)GetNode(sourcePath)!;

            foreach (var child in sourceDir.Children.Values)            {
                var childSourcePath = HackerOs.OS.System.IO.Path.Combine(sourcePath, child.Name).Replace('\\', '/');
                var childDestPath = HackerOs.OS.System.IO.Path.Combine(destinationPath, child.Name).Replace('\\', '/');

                if (!Copy(childSourcePath, childDestPath))
                {
                    return false;
                }
            }

            FireFileSystemEvent(new FileSystemEvent
            {
                EventType = FileSystemEventType.DirectoryCopied,
                Path = destinationPath,
                SourcePath = sourcePath,
                Timestamp = DateTime.UtcNow
            });

            return true;
        }        /// <summary>
        /// Creates a symbolic link (internal synchronous version).
        /// </summary>
        /// <param name="linkPath">The path for the symbolic link</param>
        /// <param name="targetPath">The target path</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool CreateSymbolicLink(string linkPath, string targetPath)
        {
            try
            {
                var normalizedLinkPath = NormalizePath(linkPath);
                var normalizedTargetPath = NormalizePath(targetPath);
                
                // Check if link already exists
                if (GetNode(normalizedLinkPath) != null)
                {
                    return false; // Link already exists
                }
                
                // Create the symbolic link file
                var parentPath = System.IO.Path.GetDirectoryName(normalizedLinkPath);                if (string.IsNullOrEmpty(parentPath))                    parentPath = "/";
                
                var parentNode = GetNode(parentPath);
                if (parentNode == null || !parentNode.IsDirectory)
                {
                    return false;
                }
                
                var fileName = HackerOs.OS.System.IO.Path.GetFileName(normalizedLinkPath);
                var linkFile = new VirtualFile(fileName, normalizedLinkPath)
                {
                    IsSymbolicLink = true,
                    SymbolicLinkTarget = normalizedTargetPath
                };
                
                var parentDir = (VirtualDirectory)parentNode;
                parentDir.AddChild(linkFile);
                
                // Add to cache
                _pathCache[normalizedLinkPath] = linkFile;
                
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.SymbolicLinkCreated,
                    Path = normalizedLinkPath,
                    TargetPath = normalizedTargetPath,
                    Timestamp = DateTime.UtcNow
                });
                
                return true;
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = linkPath,
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
                return false;
            }
        }

        /// <summary>
        /// Mounts a file system (internal synchronous version).
        /// </summary>
        /// <param name="source">The source</param>
        /// <param name="mountPath">The mount path</param>
        /// <param name="fileSystemType">The file system type</param>
        /// <param name="options">Mount options</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool Mount(string source, string mountPath, string fileSystemType, MountOptions? options = null)
        {
            return _mountManager.Mount(source, mountPath, fileSystemType, options);
        }

        /// <summary>
        /// Rebuilds the path cache from the file system tree.
        /// </summary>
        /// <param name="node">The node to start rebuilding from</param>
        /// <param name="path">The current path</param>
        private void RebuildCache(VirtualFileSystemNode node, string path)
        {
            _pathCache[path] = node;

            if (node.IsDirectory)
            {
                var directory = (VirtualDirectory)node;
                foreach (var child in directory.Children.Values)
                {
                    var childPath = path == "/" ? "/" + child.Name : path + "/" + child.Name;
                    RebuildCache(child, childPath);                }
            }
        }

        #endregion
        
        #region User-Specific Methods (Phase 2 Interface Extensions)
        
        /// <summary>
        /// Gets absolute path from relative path and current directory
        /// </summary>
        /// <param name="path">Path to resolve</param>
        /// <param name="currentDirectory">Current directory context</param>
        /// <returns>Absolute path</returns>
        public string GetAbsolutePath(string path, string currentDirectory)
        {
            if (string.IsNullOrEmpty(path))
                return currentDirectory ?? "/";
                
            if (path.StartsWith("/"))
                return NormalizePath(path);
                
            var baseDir = string.IsNullOrEmpty(currentDirectory) ? "/" : currentDirectory;
            var combinedPath = baseDir.TrimEnd('/') + "/" + path.TrimStart('/');
            return NormalizePath(combinedPath);
        }
          /// <summary>
        /// Checks if directory exists (user-specific)
        /// </summary>
        /// <param name="path">Directory path</param>
        /// <param name="user">User context</param>
        /// <returns>True if directory exists and user has access</returns>
        public async Task<bool> DirectoryExistsAsync(string path, HackerOs.OS.User.User user)
        {
            var absolutePath = GetAbsolutePath(path, _currentWorkingDirectory);
            var node = await GetNodeAsync(absolutePath);
            return node != null && node.IsDirectory;
        }
          /// <summary>
        /// Checks if file exists (user-specific)
        /// </summary>
        /// <param name="path">File path</param>
        /// <param name="user">User context</param>
        /// <returns>True if file exists and user has access</returns>
        public async Task<bool> FileExistsAsync(string path, HackerOs.OS.User.User user)
        {
            var absolutePath = GetAbsolutePath(path, _currentWorkingDirectory);
            var node = await GetNodeAsync(absolutePath);
            return node != null && node.IsFile;
        }
        
        /// <summary>
        /// Gets file system node (user-specific)
        /// </summary>
        /// <param name="path">Node path</param>
        /// <param name="user">User context</param>
        /// <returns>File system node or null</returns>
        public async Task<VirtualFileSystemNode?> GetNodeAsync(string path, HackerOs.OS.User.User user)
        {
            var absolutePath = GetAbsolutePath(path, _currentWorkingDirectory);
            return await GetNodeAsync(absolutePath);
        }
        
        /// <summary>
        /// Reads file content (user-specific)
        /// </summary>
        /// <param name="path">File path</param>
        /// <param name="user">User context</param>
        /// <returns>File content or null</returns>
        public async Task<string?> ReadAllTextAsync(string path, HackerOs.OS.User.User user)
        {
            var absolutePath = GetAbsolutePath(path, _currentWorkingDirectory);
            var content = await ReadFileAsync(absolutePath);
            return content != null ? Encoding.UTF8.GetString(content) : null;
        }

        /// <summary>
        /// Reads all bytes from a file with user permission checking.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <param name="user">The user reading the file.</param>
        /// <returns>The file content as bytes, or null if the file doesn't exist or user lacks permission.</returns>
        public async Task<byte[]> ReadAllBytesAsync(string filePath, HackerOs.OS.User.User user)
        {
            var absolutePath = GetAbsolutePath(filePath, _currentWorkingDirectory);
            var bytes = await ReadFileAsync(absolutePath, user);
            return bytes ?? Array.Empty<byte>();
        }
        
        /// <summary>
        /// Writes file content (user-specific)
        /// </summary>
        /// <param name="path">File path</param>
        /// <param name="content">Content to write</param>
        /// <param name="user">User context</param>
        /// <returns>True if successful</returns>
        public async Task<bool> WriteFileAsync(string path, string content, HackerOs.OS.User.User user)
        {
            try
            {
                var absolutePath = GetAbsolutePath(path, _currentWorkingDirectory);
                var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
                
                // Check if the file exists
                var node = GetNode(absolutePath);
                
                if (node != null)
                {
                    // File exists - check write permission
                    if (node.IsDirectory)
                    {
                        FireFileSystemEvent(new FileSystemEvent
                        {
                            EventType = FileSystemEventType.Error,
                            Path = absolutePath,
                            Message = $"Cannot write to a directory: {absolutePath}",
                            Timestamp = DateTime.UtcNow
                        });
                        return false;
                    }
                    
                    // Check write permission
                    bool hasAccess = await CheckAccessAsync(absolutePath, user, FileAccessMode.Write);
                    if (!hasAccess)
                    {
                        FireFileSystemEvent(new FileSystemEvent
                        {
                            EventType = FileSystemEventType.PermissionDenied,
                            Path = absolutePath,
                            Message = $"Permission denied: {user.Username} cannot write to {absolutePath}",
                            Timestamp = DateTime.UtcNow
                        });
                        return false;
                    }
                    
                    // Update the file content
                    var file = (VirtualFile)node;
                    file.SetContent(bytes);
                    file.ModifiedAt = DateTime.UtcNow;
                    
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.FileWritten,
                        Path = absolutePath,
                        Message = $"File modified by {user.Username}",
                        Timestamp = DateTime.UtcNow
                    });
                    
                    FileSystemChanged?.Invoke(this, CreateFileSystemEventArgs(FileSystemEventType.FileWritten, absolutePath));
                    
                    return true;
                }
                else
                {
                    // File doesn't exist - create it
                    return await CreateFileAsync(absolutePath, user, content);
                }
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = path,
                    Message = $"Error writing to file: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
                
                return false;
            }
        }
        
        /// <summary>
        /// Creates directory (user-specific)
        /// </summary>
        /// <param name="path">Directory path</param>
        /// <param name="user">User context</param>
        /// <returns>True if successful</returns>
        public async Task<bool> CreateDirectoryAsync(string path, HackerOs.OS.User.User user)
        {
            try
            {
                var absolutePath = GetAbsolutePath(path, _currentWorkingDirectory);
                var parentPath = HackerOs.OS.System.IO.Path.GetDirectoryName(absolutePath);
                
                if (string.IsNullOrEmpty(parentPath))
                    parentPath = "/";
                
                // First check if the user has permission to write to the parent directory
                bool hasAccess = await CheckAccessAsync(parentPath, user, FileAccessMode.Write);
                if (!hasAccess)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.PermissionDenied,
                        Path = absolutePath,
                        Message = $"Permission denied: {user.Username} cannot create directory in {parentPath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                // Check if parent directory exists
                var parentNode = GetNode(parentPath);
                if (parentNode == null || !parentNode.IsDirectory)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"Parent directory does not exist: {parentPath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                // Check if directory already exists
                if (GetNode(absolutePath) != null)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"Directory already exists: {absolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                var dirName = HackerOs.OS.System.IO.Path.GetFileName(absolutePath);
                var parentDir = (VirtualDirectory)parentNode;
                
                // Calculate effective permissions using umask and parent directory inheritance
                int umask = EffectivePermissions.GetUserUmask(user);
                var basePermissions = new FilePermissions(493); // Default for directories: 0755 (rwxr-xr-x)
                var effectivePermissions = EffectivePermissions.CalculateEffectivePermissions(
                    basePermissions, umask, parentDir, true);
                
                // Create the directory with proper ownership and permissions
                var newDir = new VirtualDirectory(dirName, absolutePath);
                newDir.Owner = user.UserId.ToString();
                newDir.Group = user.PrimaryGroupId.ToString();
                newDir.Permissions = effectivePermissions;
                
                // If parent has SetGID bit set, inherit the group ID
                if (parentDir.Permissions.SetGID)
                {
                    newDir.Group = parentDir.Group;
                    
                    // Also inherit SetGID for the new directory
                    var newPermissions = new FilePermissions(effectivePermissions.ToOctal());
                    newPermissions.SetGID = true;
                    newDir.Permissions = newPermissions;
                }
                
                // Add to parent directory
                parentDir.AddChild(newDir);
                
                // Add to cache
                _pathCache[absolutePath] = newDir;
                
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.DirectoryCreated,
                    Path = absolutePath,
                    Message = $"Directory created by {user.Username}",
                    Timestamp = DateTime.UtcNow
                });
                
                FileSystemChanged?.Invoke(this, CreateFileSystemEventArgs(FileSystemEventType.DirectoryCreated, absolutePath));
                
                return true;
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = path,
                    Message = $"Error creating directory: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
                
                return false;
            }
        }
        
        /// <summary>
        /// Creates file (user-specific)
        /// </summary>
        /// <param name="path">File path</param>
        /// <param name="user">User context</param>
        /// <param name="content">Optional initial content</param>
        /// <returns>True if successful</returns>
        public async Task<bool> CreateFileAsync(string path, HackerOs.OS.User.User user, string? content = null)
        {
            try
            {
                var absolutePath = GetAbsolutePath(path, _currentWorkingDirectory);
                var parentPath = HackerOs.OS.System.IO.Path.GetDirectoryName(absolutePath);
                
                if (string.IsNullOrEmpty(parentPath))
                    parentPath = "/";
                
                // First check if the user has permission to write to the parent directory
                bool hasAccess = await CheckAccessAsync(parentPath, user, FileAccessMode.Write);
                if (!hasAccess)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.PermissionDenied,
                        Path = absolutePath,
                        Message = $"Permission denied: {user.Username} cannot create file in {parentPath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                // Check if parent directory exists
                var parentNode = GetNode(parentPath);
                if (parentNode == null || !parentNode.IsDirectory)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"Parent directory does not exist: {parentPath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                // Check if file already exists
                if (GetNode(absolutePath) != null)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"File already exists: {absolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                var fileName = HackerOs.OS.System.IO.Path.GetFileName(absolutePath);
                var parentDir = (VirtualDirectory)parentNode;
                
                // Calculate effective permissions using umask and parent directory inheritance
                int umask = EffectivePermissions.GetUserUmask(user);
                var basePermissions = new FilePermissions(438); // Default for regular files: 0666 (rw-rw-rw-)
                var effectivePermissions = EffectivePermissions.CalculateEffectivePermissions(
                    basePermissions, umask, parentDir, false);
                
                // Create the file with proper ownership and permissions
                var file = new VirtualFile(fileName, absolutePath);
                file.Owner = user.UserId.ToString();
                
                // Check if parent directory has SetGID bit set
                // If so, new file inherits the group of the parent directory
                if (parentDir.Permissions.SetGID)
                {
                    file.Group = parentDir.Group;
                }
                else
                {
                    // Otherwise, use the user's primary group
                    file.Group = user.PrimaryGroupId.ToString();
                }
                
                file.Permissions = effectivePermissions;
                
                // Set content if provided
                if (content != null)
                {
                    file.SetContent(Encoding.UTF8.GetBytes(content));
                }
                
                // Add to parent directory
                parentDir.AddChild(file);
                
                // Add to cache
                _pathCache[absolutePath] = file;
                
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.FileCreated,
                    Path = absolutePath,
                    Message = $"File created by {user.Username}",
                    Timestamp = DateTime.UtcNow
                });
                
                FileSystemChanged?.Invoke(this, CreateFileSystemEventArgs(FileSystemEventType.FileCreated, absolutePath));
                
                return true;
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = path,
                    Message = $"Error creating file: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
                
                return false;
            }
        }
        
        /// <summary>
        /// Deletes file (user-specific)
        /// </summary>
        /// <param name="path">File path</param>
        /// <param name="user">User context</param>
        /// <returns>True if successful</returns>
        public async Task<bool> DeleteFileAsync(string path, HackerOs.OS.User.User user)
        {
            try
            {
                var absolutePath = GetAbsolutePath(path, _currentWorkingDirectory);
                
                // Check if the file exists
                var node = GetNode(absolutePath);
                if (node == null)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"File not found: {absolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                // Ensure it's a file
                if (node.IsDirectory)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"Cannot delete directory as file: {absolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                // Get parent directory
                var parentPath = HackerOs.OS.System.IO.Path.GetDirectoryName(absolutePath);
                if (string.IsNullOrEmpty(parentPath))
                    parentPath = "/";
                
                var parentNode = GetNode(parentPath);
                if (parentNode == null || !parentNode.IsDirectory)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"Parent directory not found: {parentPath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                var parentDir = (VirtualDirectory)parentNode;
                
                // Check permissions - we need to handle sticky bit specially
                bool hasAccess = true;
                
                // Root user can always delete files
                if (user.UserId != 0)
                {
                    // If the directory has sticky bit set, user must either:
                    // 1. Own the file, or
                    // 2. Own the directory, or
                    // 3. Be root (already checked)
                    if (parentDir.Permissions.Sticky)
                    {
                        bool isFileOwner = node.OwnerId == user.UserId;
                        bool isDirOwner = parentDir.OwnerId == user.UserId;
                        
                        if (!isFileOwner && !isDirOwner)
                        {
                            hasAccess = false;
                        }
                    }
                    else
                    {
                        // Normal case: check write permission on parent directory
                        hasAccess = await CheckAccessAsync(parentPath, user, FileAccessMode.Write);
                    }
                }
                
                if (!hasAccess)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.PermissionDenied,
                        Path = absolutePath,
                        Message = $"Permission denied: {user.Username} cannot delete {absolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                // Remove from parent
                parentDir.RemoveChild(node.Name);
                
                // Remove from cache
                RemoveFromCache(absolutePath, node);
                
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.FileDeleted,
                    Path = absolutePath,
                    Message = $"File deleted by {user.Username}",
                    Timestamp = DateTime.UtcNow
                });
                
                FileSystemChanged?.Invoke(this, CreateFileSystemEventArgs(FileSystemEventType.FileDeleted, absolutePath));
                
                return true;
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = path,
                    Message = $"Error deleting file: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
                
                return false;
            }
        }
        
        /// <summary>
        /// Deletes directory (user-specific)
        /// </summary>
        /// <param name="path">Directory path</param>        /// <param name="user">User context</param>
        /// <param name="recursive">Whether to delete directories recursively</param>
        /// <returns>True if successful</returns>
        public async Task<bool> DeleteDirectoryAsync(string path, HackerOs.OS.User.User user, bool recursive = false)
        {
            try
            {
                var absolutePath = GetAbsolutePath(path, _currentWorkingDirectory);
                
                // Check if the directory exists
                var node = GetNode(absolutePath);
                if (node == null)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"Directory not found: {absolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                // Ensure it's a directory
                if (!node.IsDirectory)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"Path is not a directory: {absolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                var directory = (VirtualDirectory)node;
                
                // Check if directory is not empty and recursive flag is not set
                if (directory.Children.Count > 0 && !recursive)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"Directory not empty: {absolutePath}. Use recursive option to delete.",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                // Get parent directory
                var parentPath = HackerOs.OS.System.IO.Path.GetDirectoryName(absolutePath);
                if (string.IsNullOrEmpty(parentPath))
                    parentPath = "/";
                
                var parentNode = GetNode(parentPath);
                if (parentNode == null || !parentNode.IsDirectory)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"Parent directory not found: {parentPath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                var parentDir = (VirtualDirectory)parentNode;
                
                // Cannot delete root directory
                if (absolutePath == "/")
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = "Cannot delete root directory",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                // Check permissions - we need to handle sticky bit specially
                bool hasAccess = true;
                
                // Root user can always delete directories
                if (user.UserId != 0)
                {
                    // If the parent directory has sticky bit set, user must either:
                    // 1. Own the directory being deleted, or
                    // 2. Own the parent directory, or
                    // 3. Be root (already checked)
                    if (parentDir.Permissions.Sticky)
                    {
                        bool isDirOwner = directory.OwnerId == user.UserId;
                        bool isParentDirOwner = parentDir.OwnerId == user.UserId;
                        
                        if (!isDirOwner && !isParentDirOwner)
                        {
                            hasAccess = false;
                        }
                    }
                    else
                    {
                        // Normal case: check write permission on parent directory
                        hasAccess = await CheckAccessAsync(parentPath, user, FileAccessMode.Write);
                    }
                    
                    // If recursive, check permissions for all subdirectories
                    if (hasAccess && recursive && directory.Children.Count > 0)
                    {
                        // Check if user has permission to delete all children
                        foreach (var child in directory.Children.Values)
                        {
                            // For directories, check recursive permission
                            if (child.IsDirectory)
                            {
                                // Check if we can delete this subdirectory
                                bool canDeleteSubdir = await CheckDirectoryDeletionPermission(child, user, recursive);
                                if (!canDeleteSubdir)
                                {
                                    FireFileSystemEvent(new FileSystemEvent
                                    {
                                        EventType = FileSystemEventType.PermissionDenied,
                                        Path = child.FullPath,
                                        Message = $"Permission denied: {user.Username} cannot delete subdirectory {child.FullPath}",
                                        Timestamp = DateTime.UtcNow
                                    });
                                    return false;
                                }
                            }
                            else
                            {
                                // For files, check if we have write permission on this directory
                                // And if sticky bit is set, check ownership
                                bool canDeleteFile = true;
                                
                                if (directory.Permissions.Sticky)
                                {
                                    bool isFileOwner = child.OwnerId == user.UserId;
                                    bool isDirOwner = directory.OwnerId == user.UserId;
                                    
                                    if (!isFileOwner && !isDirOwner)
                                    {
                                        canDeleteFile = false;
                                    }
                                }
                                
                                if (!canDeleteFile)
                                {
                                    FireFileSystemEvent(new FileSystemEvent
                                    {
                                        EventType = FileSystemEventType.PermissionDenied,
                                        Path = child.FullPath,
                                        Message = $"Permission denied: {user.Username} cannot delete file {child.FullPath} in sticky bit directory",
                                        Timestamp = DateTime.UtcNow
                                    });
                                    return false;
                                }
                            }
                        }
                    }
                }
                
                if (!hasAccess)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.PermissionDenied,
                        Path = absolutePath,
                        Message = $"Permission denied: {user.Username} cannot delete {absolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                // Remove from parent directory
                parentDir.RemoveChild(directory.Name);
                
                // Remove from cache (and all children if directory)
                RemoveFromCache(absolutePath, directory);
                
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.DirectoryDeleted,
                    Path = absolutePath,
                    Message = $"Directory deleted by {user.Username}",
                    Timestamp = DateTime.UtcNow
                });
                
                FileSystemChanged?.Invoke(this, CreateFileSystemEventArgs(FileSystemEventType.DirectoryDeleted, absolutePath));
                
                return true;
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = path,
                    Message = $"Error deleting directory: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
                
                return false;
            }
        }
        
        /// <summary>
        /// Helper method to check if a user has permission to delete a directory recursively.
        /// </summary>
        private async Task<bool> CheckDirectoryDeletionPermission(VirtualFileSystemNode dirNode, HackerOs.OS.User.User user, bool recursive)
        {
            // Root user can always delete
            if (user.UserId == 0)
            {
                return true;
            }
            
            // Must be a directory
            if (!dirNode.IsDirectory)
            {
                return false;
            }
            
            var directory = (VirtualDirectory)dirNode;
            
            // First check if we have write permission on the parent directory
            var parentPath = HackerOs.OS.System.IO.Path.GetDirectoryName(directory.FullPath);
            if (string.IsNullOrEmpty(parentPath))
                parentPath = "/";
                
            var parentNode = GetNode(parentPath);
            if (parentNode == null || !parentNode.IsDirectory)
            {
                return false;
            }
            
            var parentDir = (VirtualDirectory)parentNode;
            
            // Check if parent has sticky bit and if we own the directory
            if (parentDir.Permissions.Sticky)
            {
                bool isDirOwner = directory.OwnerId == user.UserId;
                bool isParentDirOwner = parentDir.OwnerId == user.UserId;
                
                if (!isDirOwner && !isParentDirOwner)
                {
                    return false;
                }
            }
            else
            {
                // Normal case: check write permission on parent directory
                bool hasAccess = await CheckAccessAsync(parentPath, user, FileAccessMode.Write);
                if (!hasAccess)
                {
                    return false;
                }
            }
            
            // If directory has children and recursive is true, check them too
            if (recursive && directory.Children.Count > 0)
            {
                foreach (var child in directory.Children.Values)
                {
                    if (child.IsDirectory)
                    {
                        bool canDeleteSubdir = await CheckDirectoryDeletionPermission(child, user, recursive);
                        if (!canDeleteSubdir)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        // Check if file can be deleted (considering sticky bit)
                        if (directory.Permissions.Sticky)
                        {
                            bool isFileOwner = child.OwnerId == user.UserId;
                            bool isDirOwner = directory.OwnerId == user.UserId;
                            
                            if (!isFileOwner && !isDirOwner)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Lists directory contents (user-specific)
        /// </summary>
        /// <param name="path">Directory path</param>
        /// <param name="user">User context</param>
        /// <returns>Directory contents</returns>
        public async Task<IEnumerable<VirtualFileSystemNode>> ListDirectoryAsync(string path, HackerOs.OS.User.User user)
        {
            try
            {
                var absolutePath = GetAbsolutePath(path, _currentWorkingDirectory);
                
                // Check if the directory exists
                var node = GetNode(absolutePath);
                if (node == null)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"Directory not found: {absolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return Enumerable.Empty<VirtualFileSystemNode>();
                }
                
                // Ensure it's a directory
                if (!node.IsDirectory)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = absolutePath,
                        Message = $"Path is not a directory: {absolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return Enumerable.Empty<VirtualFileSystemNode>();
                }
                
                // Check if user has execute permission on the directory
                bool hasAccess = await CheckAccessAsync(absolutePath, user, FileAccessMode.Execute);
                if (!hasAccess)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.PermissionDenied,
                        Path = absolutePath,
                        Message = $"Permission denied: {user.Username} cannot list directory {absolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return Enumerable.Empty<VirtualFileSystemNode>();
                }
                
                var directory = (VirtualDirectory)node;
                
                // Update access time
                directory.UpdateAccessTime();
                
                // Log directory listing
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.FileRead,
                    Path = absolutePath,
                    Message = $"Directory listed by {user.Username}",
                    Timestamp = DateTime.UtcNow
                });
                
                // Return all children (no permission filtering, just listing permission)
                // In a real Unix system, we don't filter directory listing results by permission
                // The user just needs execute permission on the directory to list its contents
                return directory.Children.Values;
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = path,
                    Message = $"Error listing directory: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
                
                return Enumerable.Empty<VirtualFileSystemNode>();
            }
        }
        
        #endregion

        #region Persistence Methods

        /// <summary>
        /// Enables persistence for the file system.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task EnablePersistenceAsync()
        {
            _persistenceEnabled = true;
            if (_storage != null)
            {
                await LoadFromPersistentStorageAsync();
            }
        }

        /// <summary>
        /// Reads text content from a file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>The file content as text, or null if the file doesn't exist.</returns>
        public async Task<string?> ReadTextAsync(string path)
        {
            var bytes = await ReadFileAsync(path);
            return bytes != null ? Encoding.UTF8.GetString(bytes) : null;
        }

        /// <summary>
        /// Writes text content to a file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="content">The text content to write.</param>
        /// <returns>True if the operation was successful.</returns>
        public async Task<bool> WriteTextAsync(string path, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            return await WriteFileAsync(path, bytes);
        }



        /// <summary>
        /// Writes all text to a file with user permission checking.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="content">The text content to write.</param>
        /// <param name="user">The user writing the file.</param>
        /// <returns>True if the operation was successful.</returns>
        public async Task<bool> WriteAllTextAsync(string path, string content, HackerOs.OS.User.User user)
        {
            return await WriteFileAsync(path, content, user);
        }

        #endregion

        /// <summary>
        /// Gets a file node at the specified path for shell operations.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>A file node if found and is a file; otherwise, null.</returns>
        public VirtualFile? GetFile(string path)
        {
            var node = GetNodeAsync(path).Result;
            return node as VirtualFile;
        }

        /// <summary>
        /// Gets a directory node at the specified path for shell operations.
        /// </summary>
        /// <param name="path">The path to the directory.</param>
        /// <returns>A directory node if found and is a directory; otherwise, null.</returns>
        public VirtualDirectory? GetDirectory(string path)
        {
            var node = GetNodeAsync(path).Result;
            return node as VirtualDirectory;
        }

        /// <summary>
        /// Creates a FileSystemEventArgs object for a given file system event.
        /// </summary>
        /// <param name="eventType">The type of file system event</param>
        /// <param name="path">The path that the event applies to</param>
        /// <returns>A properly configured FileSystemEventArgs object</returns>
        private FileSystemEventArgs CreateFileSystemEventArgs(FileSystemEventType eventType, string path)
        {
            // Convert FileSystemEventType to WatcherChangeTypes
            WatcherChangeTypes changeType = WatcherChangeTypes.Changed; // Default
            
            switch (eventType)
            {
                case FileSystemEventType.FileCreated:
                case FileSystemEventType.DirectoryCreated:
                    changeType = WatcherChangeTypes.Created;
                    break;
                case FileSystemEventType.FileDeleted:
                case FileSystemEventType.DirectoryDeleted:
                    changeType = WatcherChangeTypes.Deleted;
                    break;
                case FileSystemEventType.FileWritten:
                    changeType = WatcherChangeTypes.Changed;
                    break;
                case FileSystemEventType.FileCopied:
                case FileSystemEventType.DirectoryCopied:
                case FileSystemEventType.SymbolicLinkCreated:
                    changeType = WatcherChangeTypes.Created;
                    break;
                default:
                    changeType = WatcherChangeTypes.Changed;
                    break;
            }
            
            // Split path into directory and filename
            string directory = Path.GetDirectoryName(path) ?? "/";
            string fileName = Path.GetFileName(path);
            
            // If path ends with /, it's a directory and we need to ensure fileName is correct
            if (path.EndsWith('/') || path.EndsWith('\\'))
            {
                var parts = path.TrimEnd('/', '\\').Split('/', '\\');
                fileName = parts.Length > 0 ? parts[parts.Length - 1] : "";
            }
            
            return new FileSystemEventArgs(changeType, directory, fileName);
        }
        
        /// <summary>
        /// Checks if a user has the required access permissions for a file or directory path.
        /// This method verifies all path components for traversal permissions and the target for the specified access mode.
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <param name="user">The user requesting access</param>
        /// <param name="accessMode">The type of access required (Read, Write, Execute, ReadWrite)</param>
        /// <returns>True if access is granted; otherwise, false</returns>
        public async Task<bool> CheckAccessAsync(string path, UserEntity user, FileAccessMode accessMode)
        {
            // Root user always has access
            if (user.UserId == 0)
            {
                return true;
            }

            try
            {
                // Add a Task.CompletedTask to satisfy the async/await requirement
                await Task.CompletedTask;
                
                // Normalize and resolve the path
                string absolutePath = GetAbsolutePath(path, _currentWorkingDirectory);
                
                // Split the path into components
                string[] pathComponents = absolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                
                // Start from the root directory
                VirtualDirectory currentDir = _rootDirectory;
                string currentPath = "/";
                
                // Check execute permission on each directory in the path
                for (int i = 0; i < pathComponents.Length - 1; i++)
                {
                    // Update current path
                    currentPath = currentPath == "/" 
                        ? $"/{pathComponents[i]}" 
                        : $"{currentPath}/{pathComponents[i]}";
                    
                    // Get the next directory node
                    var node = GetNode(currentPath);
                    
                    // If node doesn't exist or isn't a directory, access is denied
                    if (node == null || !node.IsDirectory)
                    {
                        return false;
                    }
                    
                    // Check if user can traverse (execute) this directory
                    // Use the string version to avoid ambiguity
                    if (!node.CanAccess(user.Username, UserModelExtensions.GetGroupName(user.PrimaryGroupId), FileAccessMode.Execute))
                    {
                        return false;
                    }
                    
                    currentDir = (VirtualDirectory)node;
                }
                
                // Get the target node (the last component in the path)
                VirtualFileSystemNode? targetNode;
                
                if (pathComponents.Length == 0)
                {
                    // Root directory
                    targetNode = _rootDirectory;
                }
                else
                {
                    // Regular file or directory
                    targetNode = GetNode(absolutePath);
                }
                
                // If target doesn't exist, check parent directory for write access (for creation)
                if (targetNode == null)
                {
                    if (accessMode == FileAccessMode.Write || accessMode == FileAccessMode.ReadWrite)
                    {
                        string parentPath = HackerOs.OS.System.IO.Path.GetDirectoryName(absolutePath) ?? "/";
                        var parentNode = GetNode(parentPath);
                        
                        return parentNode != null && parentNode.IsDirectory && 
                               parentNode.CanAccess(user.Username, UserModelExtensions.GetGroupName(user.PrimaryGroupId), FileAccessMode.Write);
                    }
                    
                    return false;
                }
                
                // Special handling for sticky bit on directories when deleting/renaming
                if (accessMode == FileAccessMode.Write && targetNode.Parent != null && 
                    targetNode.Parent.Permissions.Sticky)
                {
                    // If sticky bit is set on parent directory, only owner of the file or directory owner can delete/rename
                    bool isFileOwner = targetNode.OwnerId == user.UserId;
                    bool isDirOwner = targetNode.Parent.OwnerId == user.UserId;
                    
                    if (!isFileOwner && !isDirOwner && user.UserId != 0)
                    {
                        return false;
                    }
                }
                
                // Check final access based on mode
                return targetNode.CanAccess(user.Username, UserModelExtensions.GetGroupName(user.PrimaryGroupId), accessMode);
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = path,
                    Message = $"Error checking access: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
                
                return false;
            }
        }

        /// <summary>
        /// Moves or renames a file or directory with user permission checking.
        /// </summary>
        /// <param name="sourcePath">Source path</param>
        /// <param name="destinationPath">Destination path</param>
        /// <param name="user">User performing the operation</param>
        /// <returns>True if successful; otherwise, false</returns>
        public async Task<bool> MoveAsync(string sourcePath, string destinationPath, HackerOs.OS.User.User user)
        {
            try
            {
                var sourceAbsolutePath = GetAbsolutePath(sourcePath, _currentWorkingDirectory);
                var destinationAbsolutePath = GetAbsolutePath(destinationPath, _currentWorkingDirectory);
                
                // Check if source exists
                var sourceNode = GetNode(sourceAbsolutePath);
                if (sourceNode == null)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = sourceAbsolutePath,
                        Message = $"Source not found: {sourceAbsolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                // Check if destination exists
                var destinationNode = GetNode(destinationAbsolutePath);
                if (destinationNode != null)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = destinationAbsolutePath,
                        Message = $"Destination already exists: {destinationAbsolutePath}",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                // Get parent directories
                var sourceParentPath = HackerOs.OS.System.IO.Path.GetDirectoryName(sourceAbsolutePath);
                var destinationParentPath = HackerOs.OS.System.IO.Path.GetDirectoryName(destinationAbsolutePath);
                
                if (string.IsNullOrEmpty(sourceParentPath)) sourceParentPath = "/";
                if (string.IsNullOrEmpty(destinationParentPath)) destinationParentPath = "/";
                
                var sourceParentNode = GetNode(sourceParentPath);
                var destinationParentNode = GetNode(destinationParentPath);
                
                if (sourceParentNode == null || !sourceParentNode.IsDirectory ||
                    destinationParentNode == null || !destinationParentNode.IsDirectory)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = sourceAbsolutePath,
                        Message = "Source or destination parent directory does not exist",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                var sourceParentDir = (VirtualDirectory)sourceParentNode;
                var destinationParentDir = (VirtualDirectory)destinationParentNode;
                
                // Check permissions
                
                // Root user can always move files
                if (user.UserId != 0)
                {
                    // Check sticky bit for source directory
                    if (sourceParentDir.Permissions.Sticky)
                    {
                        bool isFileOwner = sourceNode.OwnerId == user.UserId;
                        bool isDirOwner = sourceParentDir.OwnerId == user.UserId;
                        
                        if (!isFileOwner && !isDirOwner)
                        {
                            FireFileSystemEvent(new FileSystemEvent
                            {
                                EventType = FileSystemEventType.PermissionDenied,
                                Path = sourceAbsolutePath,
                                Message = $"Permission denied: {user.Username} cannot move file from sticky bit directory {sourceParentPath}",
                                Timestamp = DateTime.UtcNow
                            });
                            return false;
                        }
                    }
                    else
                    {
                        // Check write permission on source parent
                        bool hasSourceAccess = await CheckAccessAsync(sourceParentPath, user, FileAccessMode.Write);
                        if (!hasSourceAccess)
                        {
                            FireFileSystemEvent(new FileSystemEvent
                            {
                                EventType = FileSystemEventType.PermissionDenied,
                                Path = sourceAbsolutePath,
                                Message = $"Permission denied: {user.Username} cannot write to source directory {sourceParentPath}",
                                Timestamp = DateTime.UtcNow
                            });
                            return false;
                        }
                    }
                    
                    // Check write permission on destination parent
                    bool hasDestAccess = await CheckAccessAsync(destinationParentPath, user, FileAccessMode.Write);
                    if (!hasDestAccess)
                    {
                        FireFileSystemEvent(new FileSystemEvent
                        {
                            EventType = FileSystemEventType.PermissionDenied,
                            Path = destinationAbsolutePath,
                            Message = $"Permission denied: {user.Username} cannot write to destination directory {destinationParentPath}",
                            Timestamp = DateTime.UtcNow
                        });
                        return false;
                    }
                }
                
                // All permission checks passed, perform the move
                
                // Create a new node at the destination
                var fileName = HackerOs.OS.System.IO.Path.GetFileName(destinationAbsolutePath);
                
                // Clone the node (keeping all properties)
                VirtualFileSystemNode newNode;
                if (sourceNode.IsDirectory)
                {
                    var sourceDir = (VirtualDirectory)sourceNode;
                    var newDir = new VirtualDirectory(fileName, destinationAbsolutePath);
                    
                    // Copy all properties
                    newDir.CreatedAt = sourceDir.CreatedAt;
                    newDir.ModifiedAt = DateTime.UtcNow;
                    newDir.AccessedAt = DateTime.UtcNow;
                    newDir.Permissions = sourceDir.Permissions;
                    newDir.Owner = sourceDir.Owner;
                    newDir.Group = sourceDir.Group;
                    
                    // Copy all children
                    foreach (var child in sourceDir.Children.Values)
                    {
                        // Update the child's path
                        string childNewPath = HackerOs.OS.System.IO.Path.Combine(destinationAbsolutePath, child.Name);
                        child.FullPath = childNewPath;
                        child.Parent = newDir;
                        
                        // Add to the new directory
                        newDir.AddChild(child);
                        
                        // Update the path cache for this child
                        _pathCache[childNewPath] = child;
                        
                        // Recursively update paths for all descendants if it's a directory
                        if (child.IsDirectory)
                        {
                            UpdatePathsRecursively((VirtualDirectory)child, childNewPath);
                        }
                    }
                    
                    newNode = newDir;
                }
                else
                {
                    var sourceFile = (VirtualFile)sourceNode;
                    var newFile = new VirtualFile(fileName, destinationAbsolutePath);
                    
                    // Copy all properties
                    newFile.CreatedAt = sourceFile.CreatedAt;
                    newFile.ModifiedAt = DateTime.UtcNow;
                    newFile.AccessedAt = DateTime.UtcNow;
                    newFile.Permissions = sourceFile.Permissions;
                    newFile.Owner = sourceFile.Owner;
                    newFile.Group = sourceFile.Group;
                    newFile.MimeType = sourceFile.MimeType;
                    newFile.IsSymbolicLink = sourceFile.IsSymbolicLink;
                    newFile.SymbolicLinkTarget = sourceFile.SymbolicLinkTarget;
                    
                    // Copy content
                    newFile.SetContent(sourceFile.Content);
                    
                    newNode = newFile;
                }
                
                // Add the new node to the destination parent
                destinationParentDir.AddChild(newNode);
                
                // Update the path cache
                _pathCache[destinationAbsolutePath] = newNode;
                
                // Remove the source node from its parent
                sourceParentDir.RemoveChild(sourceNode.Name);
                
                // Remove the source from the path cache
                _pathCache.Remove(sourceAbsolutePath);
                
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.FileCopied,
                    Path = destinationAbsolutePath,
                    Message = $"File moved by {user.Username} from {sourceAbsolutePath} to {destinationAbsolutePath}",
                    Timestamp = DateTime.UtcNow
                });
                
                FileSystemChanged?.Invoke(this, CreateFileSystemEventArgs(FileSystemEventType.FileCopied, destinationAbsolutePath));
                
                return true;
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = sourcePath,
                    Message = $"Error moving file: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
                
                return false;
            }
        }
        
        /// <summary>
        /// Helper method to recursively update paths for all descendants of a directory.
        /// </summary>
        private void UpdatePathsRecursively(VirtualDirectory directory, string newBasePath)
        {
            foreach (var child in directory.Children.Values)
            {
                string childNewPath = HackerOs.OS.System.IO.Path.Combine(newBasePath, child.Name);
                child.FullPath = childNewPath;
                
                // Update the path cache
                _pathCache[childNewPath] = child;
                
                // Recursively update paths for all descendants if it's a directory
                if (child.IsDirectory)
                {
                    UpdatePathsRecursively((VirtualDirectory)child, childNewPath);
                }
            }
        }

        /// <summary>
        /// Logs a file system event.
        /// </summary>
        /// <param name="eventType">The type of event</param>
        /// <param name="path">The path associated with the event</param>
        /// <param name="message">A message describing the event</param>
        public void LogFileSystemEvent(FileSystemEventType eventType, string path, string message)
        {
            FireFileSystemEvent(new FileSystemEvent
            {
                EventType = eventType,
                Path = path,
                Message = message,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
