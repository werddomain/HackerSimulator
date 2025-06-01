using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace HackerOs.IO.FileSystem
{
    /// <summary>
    /// Main implementation of the virtual file system.
    /// Provides a complete Linux-like file system simulation with persistence.
    /// </summary>
    public class VirtualFileSystem : IVirtualFileSystem
    {
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
        }

        /// <summary>
        /// Gets or sets the current user for tilde expansion and permissions.
        /// </summary>
        public string CurrentUser
        {
            get => _currentUser;
            set => _currentUser = value ?? "root";
        }        /// <summary>
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
        public async Task<VirtualFileSystemNode?> GetNodeAsync(string path)
        {
            if (!_isInitialized)
                await InitializeAsync();

            var normalizedPath = NormalizePath(path);
            
            lock (_lockObject)
            {
                // Check cache first
                if (_pathCache.TryGetValue(normalizedPath, out var cachedNode))
                {
                    cachedNode.UpdateAccessTime();
                    return cachedNode;
                }

                // Traverse path to find node
                var node = TraversePath(normalizedPath);
                if (node != null)
                {
                    _pathCache[normalizedPath] = node;
                    node.UpdateAccessTime();
                }

                return node;
            }
        }

        /// <summary>
        /// Creates a new file at the specified path.
        /// </summary>
        public async Task<bool> CreateFileAsync(string path, byte[]? content = null)
        {
            if (!_isInitialized)
                await InitializeAsync();

            var normalizedPath = NormalizePath(path);
            var fileName = Path.GetFileName(normalizedPath);
            var parentPath = Path.GetDirectoryName(normalizedPath) ?? "/";

            if (string.IsNullOrEmpty(fileName))
                return false;

            lock (_lockObject)
            {
                var parentDir = TraversePath(parentPath) as VirtualDirectory;
                if (parentDir == null)
                    return false;

                if (parentDir.HasChild(fileName))
                    return false;

                var file = new VirtualFile(fileName, normalizedPath, content ?? Array.Empty<byte>());
                if (parentDir.AddChild(file))
                {
                    _pathCache[normalizedPath] = file;
                      FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.FileCreated,
                        Path = normalizedPath,
                        Timestamp = DateTime.UtcNow
                    });
                    
                    // Auto-save to persistent storage
                    _ = AutoSaveToPersistentStorageAsync();
                    
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a new directory at the specified path.
        /// </summary>
        public async Task<bool> CreateDirectoryAsync(string path)
        {
            if (!_isInitialized)
                await InitializeAsync();

            var normalizedPath = NormalizePath(path);
            
            // Handle recursive directory creation
            var pathParts = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentPath = "/";

            lock (_lockObject)
            {
                var currentDir = _rootDirectory;

                foreach (var part in pathParts)
                {
                    currentPath = currentPath == "/" ? $"/{part}" : $"{currentPath}/{part}";
                    
                    var existingChild = currentDir.GetChild(part);
                    if (existingChild != null)
                    {
                        if (existingChild is VirtualDirectory dir)
                        {
                            currentDir = dir;
                            continue;
                        }
                        else
                        {
                            // File exists with same name
                            return false;
                        }
                    }

                    // Create new directory
                    var newDir = new VirtualDirectory(part, currentPath);
                    if (!currentDir.AddChild(newDir))
                        return false;                    _pathCache[currentPath] = newDir;
                    currentDir = newDir;
                }

                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.DirectoryCreated,
                    Path = normalizedPath,
                    Timestamp = DateTime.UtcNow
                });

                // Auto-save to persistent storage
                _ = AutoSaveToPersistentStorageAsync();

                return true;
            }
        }

        /// <summary>
        /// Deletes a file or directory at the specified path.
        /// </summary>
        public async Task<bool> DeleteAsync(string path, bool recursive = false)
        {
            if (!_isInitialized)
                await InitializeAsync();

            var normalizedPath = NormalizePath(path);
            
            if (normalizedPath == "/")
                return false; // Cannot delete root

            lock (_lockObject)
            {
                var node = TraversePath(normalizedPath);
                if (node == null)
                    return false;

                if (node.IsDirectory && !recursive)
                {
                    var dir = (VirtualDirectory)node;
                    if (dir.ChildCount > 0)
                        return false; // Directory not empty
                }

                if (node.Parent != null)
                {
                    node.Parent.RemoveChild(node.Name);
                    RemoveFromCache(normalizedPath, recursive);

                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = node.IsDirectory ? FileSystemEventType.DirectoryDeleted : FileSystemEventType.FileDeleted,
                        Path = normalizedPath,
                        Timestamp = DateTime.UtcNow
                    });

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a file or directory exists at the specified path.
        /// </summary>
        public async Task<bool> ExistsAsync(string path)
        {
            var node = await GetNodeAsync(path);
            return node != null;
        }        /// <summary>
        /// Lists all items in the specified directory.
        /// </summary>
        /// <param name="path">The directory path to list</param>
        /// <param name="includeHidden">Whether to include hidden files (starting with .)</param>
        /// <returns>An enumerable of file system nodes in the directory</returns>
        public async Task<IEnumerable<VirtualFileSystemNode>> ListDirectoryAsync(string path, bool includeHidden = false)
        {
            var node = await GetNodeAsync(path);
            if (node is VirtualDirectory directory)
            {
                return directory.ListChildren(includeHidden);
            }
            return Array.Empty<VirtualFileSystemNode>();
        }

        /// <summary>
        /// Lists all items in the specified directory.
        /// </summary>
        public async Task<IEnumerable<VirtualFileSystemNode>> ListDirectoryAsync(string path)
        {
            return await ListDirectoryAsync(path, false);
        }

        /// <summary>
        /// Reads the content of a file.
        /// </summary>
        public async Task<byte[]?> ReadFileAsync(string path)
        {
            var node = await GetNodeAsync(path);
            if (node is VirtualFile file)
            {
                // Handle symbolic links
                if (file.IsSymbolicLink && !string.IsNullOrEmpty(file.SymbolicLinkTarget))
                {
                    var targetPath = ResolveSymbolicLink(file.SymbolicLinkTarget, path);
                    return await ReadFileAsync(targetPath);
                }

                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.FileRead,
                    Path = path,
                    Timestamp = DateTime.UtcNow
                });

                return file.Content;
            }
            return null;
        }

        /// <summary>
        /// Writes content to a file.
        /// </summary>
        public async Task<bool> WriteFileAsync(string path, byte[] content)
        {
            var node = await GetNodeAsync(path);
            if (node is VirtualFile file)
            {
                // Handle symbolic links
                if (file.IsSymbolicLink && !string.IsNullOrEmpty(file.SymbolicLinkTarget))
                {
                    var targetPath = ResolveSymbolicLink(file.SymbolicLinkTarget, path);
                    return await WriteFileAsync(targetPath, content);
                }

                lock (_lockObject)
                {
                    file.SetContent(content);
                    
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.FileWritten,
                        Path = path,
                        Timestamp = DateTime.UtcNow
                    });
                    
                    return true;
                }
            }
            else
            {
                // File doesn't exist, create it
                return await CreateFileAsync(path, content);
            }
        }

        /// <summary>
        /// Moves a file or directory from source to destination.
        /// </summary>
        public async Task<bool> MoveAsync(string sourcePath, string destinationPath)
        {
            var sourceNode = await GetNodeAsync(sourcePath);
            if (sourceNode == null)
                return false;

            // Check if destination already exists
            if (await ExistsAsync(destinationPath))
                return false;

            // Create a copy and then delete the original
            if (await CopyAsync(sourcePath, destinationPath))
            {
                return await DeleteAsync(sourcePath, true);
            }

            return false;
        }

        /// <summary>
        /// Copies a file or directory from source to destination.
        /// </summary>
        public async Task<bool> CopyAsync(string sourcePath, string destinationPath)
        {
            var sourceNode = await GetNodeAsync(sourcePath);
            if (sourceNode == null)
                return false;

            lock (_lockObject)
            {
                var clonedNode = sourceNode.Clone();
                var destFileName = Path.GetFileName(destinationPath);
                var destParentPath = Path.GetDirectoryName(destinationPath) ?? "/";
                
                var parentDir = TraversePath(destParentPath) as VirtualDirectory;
                if (parentDir == null)
                    return false;

                clonedNode.Name = destFileName;
                clonedNode.FullPath = NormalizePath(destinationPath);
                
                if (parentDir.AddChild(clonedNode))
                {
                    _pathCache[clonedNode.FullPath] = clonedNode;
                    
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = sourceNode.IsDirectory ? FileSystemEventType.DirectoryCopied : FileSystemEventType.FileCopied,
                        Path = destinationPath,
                        SourcePath = sourcePath,
                        Timestamp = DateTime.UtcNow
                    });
                    
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a symbolic link.
        /// </summary>
        public async Task<bool> CreateSymbolicLinkAsync(string linkPath, string targetPath)
        {
            if (!_isInitialized)
                await InitializeAsync();

            var normalizedLinkPath = NormalizePath(linkPath);
            var linkName = Path.GetFileName(normalizedLinkPath);
            var parentPath = Path.GetDirectoryName(normalizedLinkPath) ?? "/";

            if (string.IsNullOrEmpty(linkName))
                return false;

            lock (_lockObject)
            {
                var parentDir = TraversePath(parentPath) as VirtualDirectory;
                if (parentDir == null)
                    return false;

                var symlink = parentDir.CreateSymbolicLink(linkName, targetPath);
                if (symlink != null)
                {
                    _pathCache[normalizedLinkPath] = symlink;
                    
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.SymbolicLinkCreated,
                        Path = normalizedLinkPath,
                        TargetPath = targetPath,
                        Timestamp = DateTime.UtcNow
                    });
                    
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Changes the current working directory.
        /// </summary>
        /// <param name="path">The new working directory path</param>
        /// <returns>True if the directory change was successful, false otherwise</returns>
        public async Task<bool> ChangeDirectoryAsync(string path)
        {
            var node = await GetNodeAsync(path);
            if (node is VirtualDirectory)
            {
                CurrentWorkingDirectory = node.FullPath;
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
        }

        /// <summary>
        /// Enables persistence using IndexedDB storage.
        /// </summary>
        /// <param name="storage">The IndexedDB storage instance</param>
        /// <returns>True if persistence was enabled successfully</returns>
        public async Task<bool> EnablePersistenceAsync(IndexedDBStorage storage)
        {
            _storage = storage;
            
            if (await _storage.InitializeAsync())
            {
                _persistenceEnabled = true;
                
                // Try to load existing file system data
                var savedRoot = await _storage.LoadFileSystemAsync();
                if (savedRoot != null)
                {
                    _rootDirectory = savedRoot;
                    RebuildPathCache();
                }
                
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Enables persistence for the file system using IndexedDB storage.
        /// </summary>
        /// <param name="jsRuntime">The JavaScript runtime for browser interop</param>
        /// <param name="databaseName">Optional database name</param>
        /// <param name="version">Optional database version</param>
        /// <returns>True if persistence was successfully enabled, false otherwise</returns>
        public async Task<bool> EnablePersistenceAsync(IJSRuntime jsRuntime, string databaseName = "HackerOSFileSystem", int version = 1)
        {
            try
            {
                _storage = new IndexedDBStorage(jsRuntime, databaseName, version);
                var initialized = await _storage.InitializeAsync();
                
                if (initialized)
                {
                    _persistenceEnabled = true;
                    
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.SystemInitialized,
                        Path = "/",
                        Message = "Persistence enabled successfully",
                        Timestamp = DateTime.UtcNow
                    });
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = "/",
                    Message = $"Failed to enable persistence: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
                
                return false;
            }
        }

        /// <summary>
        /// Disables persistence and clears storage reference.
        /// </summary>
        public void DisablePersistence()
        {
            _persistenceEnabled = false;
            _storage = null;
        }

        /// <summary>
        /// Saves the current file system state to persistent storage.
        /// </summary>
        /// <returns>True if save was successful, false otherwise</returns>
        public async Task<bool> SaveToPersistentStorageAsync()
        {
            if (!_persistenceEnabled || _storage == null)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = "/",
                    Message = "Persistence not enabled",
                    Timestamp = DateTime.UtcNow
                });
                return false;
            }

            try
            {
                var saved = await _storage.SaveFileSystemAsync(_rootDirectory);
                
                if (saved)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.SystemInitialized,
                        Path = "/",
                        Message = "File system saved to persistent storage",
                        Timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = "/",
                        Message = "Failed to save file system to persistent storage",
                        Timestamp = DateTime.UtcNow
                    });
                }
                
                return saved;
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = "/",
                    Message = $"Error saving to persistent storage: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
                
                return false;
            }
        }

        /// <summary>
        /// Loads the file system state from persistent storage.
        /// </summary>
        /// <returns>True if load was successful, false otherwise</returns>
        public async Task<bool> LoadFromPersistentStorageAsync()
        {
            if (!_persistenceEnabled || _storage == null)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = "/",
                    Message = "Persistence not enabled",
                    Timestamp = DateTime.UtcNow
                });
                return false;
            }

            try
            {
                var loadedRoot = await _storage.LoadFileSystemAsync();
                
                if (loadedRoot != null)
                {
                    lock (_lockObject)
                    {
                        _rootDirectory = loadedRoot;
                        _pathCache.Clear();
                        _pathCache["/"] = _rootDirectory;
                        
                        // Rebuild path cache
                        RebuildPathCache(_rootDirectory, "/");
                    }
                    
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.SystemInitialized,
                        Path = "/",
                        Message = "File system loaded from persistent storage",
                        Timestamp = DateTime.UtcNow
                    });
                    
                    return true;
                }
                else
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = "/",
                        Message = "No file system data found in persistent storage",
                        Timestamp = DateTime.UtcNow
                    });
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                FireFileSystemEvent(new FileSystemEvent
                {
                    EventType = FileSystemEventType.Error,
                    Path = "/",
                    Message = $"Error loading from persistent storage: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
                
                return false;
            }
        }

        /// <summary>
        /// Rebuilds the path cache after loading from persistent storage.
        /// </summary>
        private void RebuildPathCache(VirtualFileSystemNode node, string currentPath)
        {
            _pathCache[currentPath] = node;
            
            if (node is VirtualDirectory directory)
            {
                foreach (var child in directory.Children.Values)
                {
                    var childPath = currentPath.TrimEnd('/') + "/" + child.Name;
                    if (currentPath == "/")
                        childPath = "/" + child.Name;
                    
                    RebuildPathCache(child, childPath);
                }
            }
        }

        /// <summary>
        /// Auto-saves the file system to persistent storage if persistence is enabled.
        /// Called automatically after file system modifications.
        /// </summary>
        private async Task AutoSaveAsync()
        {
            if (_persistenceEnabled && _storage != null)
            {
                try
                {
                    await _storage.SaveFileSystemAsync(_rootDirectory);
                }
                catch (Exception ex)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = "/",
                        Message = $"Auto-save failed: {ex.Message}",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
        }

        /// <summary>
        /// Creates the standard Unix directory structure.
        /// </summary>
        private void CreateStandardDirectoryStructure()
        {
            var standardDirs = new[]
            {
                "/bin",      // Essential command binaries
                "/boot",     // Boot loader files
                "/dev",      // Device files
                "/etc",      // System configuration files
                "/home",     // User home directories
                "/lib",      // Essential shared libraries
                "/media",    // Mount point for removable media
                "/mnt",      // Mount point for temporarily mounted filesystems
                "/opt",      // Optional application software packages
                "/proc",     // Virtual filesystem documenting kernel and process status
                "/root",     // Home directory for root user
                "/run",      // Runtime variable data
                "/sbin",     // Essential system binaries
                "/srv",      // Site-specific data served by system
                "/sys",      // Virtual filesystem providing information about the system
                "/tmp",      // Temporary files
                "/usr",      // Secondary hierarchy for read-only user data
                "/usr/bin",  // Non-essential command binaries
                "/usr/lib",  // Libraries for /usr/bin/ and /usr/sbin/
                "/usr/local", // Tertiary hierarchy for local data
                "/usr/sbin", // Non-essential system binaries
                "/var",      // Variable data files
                "/var/log",  // Log files
                "/var/tmp"   // Temporary files preserved between reboots
            };

            foreach (var dir in standardDirs)
            {
                CreateDirectoryStructure(dir);
            }
        }

        /// <summary>
        /// Creates a directory structure recursively.
        /// </summary>
        private void CreateDirectoryStructure(string path)
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentDir = _rootDirectory;
            var currentPath = "/";

            foreach (var part in parts)
            {
                currentPath = currentPath == "/" ? $"/{part}" : $"{currentPath}/{part}";
                
                var existingChild = currentDir.GetChild(part);
                if (existingChild is VirtualDirectory existingDir)
                {
                    currentDir = existingDir;
                }
                else if (existingChild == null)
                {
                    var newDir = new VirtualDirectory(part, currentPath);
                    currentDir.AddChild(newDir);
                    currentDir = newDir;
                }
            }
        }

        /// <summary>
        /// Removes entries from the cache when files/directories are deleted.
        /// </summary>
        private void RemoveFromCache(string path, bool recursive)
        {
            _pathCache.Remove(path);

            if (recursive)
            {
                var keysToRemove = _pathCache.Keys.Where(k => k.StartsWith(path + "/")).ToList();
                foreach (var key in keysToRemove)
                {
                    _pathCache.Remove(key);
                }
            }
        }

        /// <summary>
        /// Fires a file system event.
        /// </summary>
        private void FireFileSystemEvent(FileSystemEvent fsEvent)
        {
            OnFileSystemEvent?.Invoke(this, fsEvent);
        }

        /// <summary>
        /// Gets the home directory for a user.
        /// </summary>
        private string GetHomeDirectory(string user)
        {
            // For simplicity, assume home directories are under /home
            // and named after the user. E.g., /home/root, /home/john, etc.
            return $"/home/{user}";
        }

        /// <summary>
        /// Expands tilde (~) to the user's home directory.
        /// </summary>
        private string ExpandTilde(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith('~'))
                return path;

            if (path == "~")
            {
                return GetUserHomeDirectory(_currentUser);
            }
            else if (path.StartsWith("~/"))
            {
                var homeDir = GetUserHomeDirectory(_currentUser);
                return homeDir + path.Substring(1);
            }
            else if (path.StartsWith("~") && (path.Length > 1 && path[1] != '/'))
            {
                // Handle ~username format
                var slashIndex = path.IndexOf('/');
                var username = slashIndex > 0 ? path.Substring(1, slashIndex - 1) : path.Substring(1);
                var homeDir = GetUserHomeDirectory(username);
                
                if (slashIndex > 0)
                    return homeDir + path.Substring(slashIndex);
                else
                    return homeDir;
            }

            return path;
        }

        /// <summary>
        /// Gets the home directory path for a user.
        /// </summary>
        private string GetUserHomeDirectory(string username)
        {
            if (string.IsNullOrEmpty(username) || username == "root")
                return "/root";
            
            return $"/home/{username}";
        }        /// <summary>
        /// Combines two paths in a Linux-style manner.
        /// </summary>
        private string CombinePaths(string basePath, string relativePath)
        {
            if (string.IsNullOrEmpty(basePath))
                basePath = "/";
            if (string.IsNullOrEmpty(relativePath))
                return basePath;

            // Ensure base path ends with / for proper combination
            if (!basePath.EndsWith('/'))
                basePath += "/";

            return basePath + relativePath;
        }

        /// <summary>
        /// Normalizes a path by expanding tilde, resolving relative paths, and cleaning up separators.
        /// </summary>
        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "/";

            // Expand tilde
            path = ExpandTilde(path);

            // Handle relative paths
            if (!path.StartsWith('/'))
            {
                path = CombinePaths(_currentWorkingDirectory, path);
            }

            // Normalize path separators and handle .. and .
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var stack = new List<string>();

            foreach (var part in parts)
            {
                if (part == ".")
                {
                    continue; // Current directory, skip
                }
                else if (part == "..")
                {
                    if (stack.Count > 0)
                    {
                        stack.RemoveAt(stack.Count - 1); // Go up one directory
                    }
                    // If at root, stay at root
                }
                else
                {
                    stack.Add(part);
                }
            }

            return "/" + string.Join("/", stack);
        }

        /// <summary>
        /// Traverses a path to find the corresponding node.
        /// </summary>
        private VirtualFileSystemNode? TraversePath(string normalizedPath)
        {
            if (normalizedPath == "/")
                return _rootDirectory;

            var parts = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentNode = _rootDirectory as VirtualFileSystemNode;

            foreach (var part in parts)
            {
                if (currentNode is VirtualDirectory dir)
                {
                    currentNode = dir.GetChild(part);
                    if (currentNode == null)
                        return null;
                }
                else
                {
                    return null; // Cannot traverse through a file
                }
            }

            return currentNode;
        }

        /// <summary>
        /// Resolves a symbolic link path relative to the link's location.
        /// </summary>
        private string ResolveSymbolicLink(string targetPath, string linkPath)
        {
            if (targetPath.StartsWith('/'))
            {
                // Absolute path
                return targetPath;
            }
            else
            {
                // Relative path - resolve relative to the link's directory
                var linkDir = Path.GetDirectoryName(linkPath) ?? "/";
                return NormalizePath(CombinePaths(linkDir, targetPath));
            }
        }

        /// <summary>
        /// Auto-saves the file system to persistent storage if persistence is enabled.
        /// </summary>
        private async Task AutoSaveToPersistentStorageAsync()
        {
            if (_persistenceEnabled && _storage != null)
            {
                try
                {
                    await _storage.SaveFileSystemAsync(_rootDirectory);
                }
                catch (Exception ex)
                {
                    FireFileSystemEvent(new FileSystemEvent
                    {
                        EventType = FileSystemEventType.Error,
                        Path = "/",
                        Message = $"Auto-save failed: {ex.Message}",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
        }

        /// <summary>
        /// Rebuilds the path cache from the file system tree.
        /// </summary>
        private void RebuildPathCache()
        {
            _pathCache.Clear();
            _pathCache["/"] = _rootDirectory;
            RebuildPathCacheRecursive(_rootDirectory, "/");
        }

        /// <summary>
        /// Recursively rebuilds the path cache.
        /// </summary>
        private void RebuildPathCacheRecursive(VirtualFileSystemNode node, string currentPath)
        {
            if (node is VirtualDirectory directory)
            {
                foreach (var child in directory.Children.Values)
                {
                    var childPath = currentPath == "/" ? $"/{child.Name}" : $"{currentPath}/{child.Name}";
                    _pathCache[childPath] = child;
                    RebuildPathCacheRecursive(child, childPath);
                }
            }
        }

        /// <summary>
        /// Gets the mount manager for this file system.
        /// </summary>
        public MountManager MountManager => _mountManager;

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
            if (!_isInitialized)
                await InitializeAsync();

            // Ensure the mount point directory exists
            await CreateDirectoryAsync(mountPath);

            return _mountManager.Mount(source, mountPath, fileSystemType, options);
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
    }
}
