using HackerOs.OS.Applications;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using System.Text;
using System.Text.Json;
using System.IO; // For disambiguating FileSystemEventArgs

namespace HackerOs.OS.Applications.BuiltIn
{
    /// <summary>
    /// Graphical file manager application for browsing and managing files
    /// </summary>
    public class FileManager : ApplicationBase
    {
        private readonly IVirtualFileSystem _fileSystem;
        private string _currentPath;
        private List<VirtualFileSystemNode> _currentContents;
        private VirtualFileSystemNode? _selectedNode;
        private readonly object _lockObject = new object();

        // File operation modes
        public enum FileOperation
        {
            None,
            Copy,
            Cut,
            Move
        }

        private FileOperation _pendingOperation = FileOperation.None;
        private List<string> _operationPaths = new List<string>();

        public FileManager(IVirtualFileSystem fileSystem) : base()
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _currentPath = "/";
            _currentContents = new List<VirtualFileSystemNode>();
        }

        protected override async Task<bool> OnStartAsync(ApplicationLaunchContext context)
        {
            try
            {
                // Set initial directory to user's home directory
                var userHome = context.UserSession?.User?.HomeDirectory ?? "/home";
                var user = context.UserSession?.GetUser();
                
                if (user != null && await _fileSystem.DirectoryExistsAsync(userHome, user))
                {
                    _currentPath = userHome;
                }
                else
                {
                    _currentPath = "/";
                }

                // Subscribe to file system events
                _fileSystem.FileSystemChanged += OnFileSystemChanged;

                // Load initial directory contents
                await RefreshDirectoryAsync();

                await OnOutputAsync($"File Manager opened at: {_currentPath}");
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to start file manager: {ex.Message}");
                return false;
            }
        }        protected override async Task<bool> OnStopAsync()
        {
            try
            {
                // Unsubscribe from file system events
                if (_fileSystem != null)
                {
                    _fileSystem.FileSystemChanged -= OnFileSystemChanged;
                }

                await OnOutputAsync("File Manager closed.");
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Error stopping file manager: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Navigate to a specific directory
        /// </summary>
        public async Task<bool> NavigateToAsync(string path)
        {
            try
            {
                var user = Context?.UserSession?.GetUser();
                if (user == null)
                {
                    await OnErrorAsync("No user context available for file operation.");
                    return false;
                }

                if (!await _fileSystem.DirectoryExistsAsync(path, user))
                {
                    await OnErrorAsync($"Directory does not exist: {path}");
                    return false;
                }

                _currentPath = path;
                await RefreshDirectoryAsync();
                await OnOutputAsync($"Navigated to: {_currentPath}");
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Navigation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Navigate up one directory level
        /// </summary>
        public async Task<bool> NavigateUpAsync()
        {
            if (_currentPath == "/") return false;

            var parentPath = System.IO.Path.GetDirectoryName(_currentPath.Replace('/', '\\'))?.Replace('\\', '/') ?? "/";
            if (string.IsNullOrEmpty(parentPath)) parentPath = "/";
            
            return await NavigateToAsync(parentPath);
        }

        /// <summary>
        /// Refresh current directory contents
        /// </summary>
        public async Task RefreshDirectoryAsync()
        {
            try
            {
                var user = Context?.UserSession?.GetUser();
                if (user == null)
                {
                    await OnErrorAsync("No user context available for file operation.");
                    return;
                }                // Use the implementation in IVirtualFileSystem for getting directory contents
                var contents = await _fileSystem.ListDirectoryAsync(_currentPath, user);
                
                lock (_lockObject)
                {
                    _currentContents = contents.ToList();
                }

                await OnOutputAsync($"Directory refreshed: {contents.Count()} items");
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to refresh directory: {ex.Message}");
            }
        }

        /// <summary>
        /// Select a file or directory
        /// </summary>
        public async Task SelectNodeAsync(string name)
        {
            lock (_lockObject)
            {
                _selectedNode = _currentContents.FirstOrDefault(n => n.Name == name);
            }

            if (_selectedNode != null)
            {
                await OnOutputAsync($"Selected: {_selectedNode.Name} ({(_selectedNode.IsDirectory ? "Directory" : "File")})");
            }
        }

        /// <summary>
        /// Open selected file or directory
        /// </summary>
        public async Task<bool> OpenSelectedAsync()
        {
            if (_selectedNode == null)
            {
                await OnErrorAsync("No item selected.");
                return false;
            }

            if (_selectedNode.IsDirectory)
            {
                return await NavigateToAsync(_selectedNode.FullPath);
            }
            else
            {
                // For files, try to open with appropriate application
                await OnOutputAsync($"Opening file: {_selectedNode.Name}");
                // TODO: Integrate with application launcher to open files with appropriate apps
                return true;
            }
        }

        /// <summary>
        /// Create a new directory
        /// </summary>
        public async Task<bool> CreateDirectoryAsync(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    await OnErrorAsync("Directory name cannot be empty.");
                    return false;
                }

                var user = Context?.UserSession?.GetUser();
                if (user == null)
                {
                    await OnErrorAsync("No user context available for file operation.");
                    return false;
                }

                var newPath = System.IO.Path.Combine(_currentPath, name).Replace('\\', '/');
                await _fileSystem.CreateDirectoryAsync(newPath, user);
                
                await OnOutputAsync($"Directory created: {name}");
                await RefreshDirectoryAsync();
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to create directory: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Create a new file
        /// </summary>
        public async Task<bool> CreateFileAsync(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    await OnErrorAsync("File name cannot be empty.");
                    return false;
                }

                var user = Context?.UserSession?.GetUser();
                if (user == null)
                {
                    await OnErrorAsync("No user context available for file operation.");
                    return false;
                }

                var newPath = System.IO.Path.Combine(_currentPath, name).Replace('\\', '/');
                await _fileSystem.WriteFileAsync(newPath, string.Empty, user);
                
                await OnOutputAsync($"File created: {name}");
                await RefreshDirectoryAsync();
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to create file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete selected file or directory
        /// </summary>
        public async Task<bool> DeleteSelectedAsync()
        {
            if (_selectedNode == null)
            {
                await OnErrorAsync("No item selected.");
                return false;
            }

            try
            {
                var user = Context?.UserSession?.GetUser();
                if (user == null)
                {
                    await OnErrorAsync("No user context available for file operation.");
                    return false;
                }

                if (_selectedNode.IsDirectory)
                {
                    await _fileSystem.DeleteDirectoryAsync(_selectedNode.FullPath, user, recursive: false);
                }
                else
                {
                    await _fileSystem.DeleteFileAsync(_selectedNode.FullPath, user);
                }

                await OnOutputAsync($"Deleted: {_selectedNode.Name}");
                _selectedNode = null;
                await RefreshDirectoryAsync();
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to delete item: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Copy selected item to clipboard
        /// </summary>
        public async Task CopySelectedAsync()
        {
            if (_selectedNode == null)
            {
                await OnErrorAsync("No item selected.");
                return;
            }

            _pendingOperation = FileOperation.Copy;
            _operationPaths.Clear();
            _operationPaths.Add(_selectedNode.FullPath);
            
            await OnOutputAsync($"Copied to clipboard: {_selectedNode.Name}");
        }

        /// <summary>
        /// Cut selected item to clipboard
        /// </summary>
        public async Task CutSelectedAsync()
        {
            if (_selectedNode == null)
            {
                await OnErrorAsync("No item selected.");
                return;
            }

            _pendingOperation = FileOperation.Cut;
            _operationPaths.Clear();
            _operationPaths.Add(_selectedNode.FullPath);
            
            await OnOutputAsync($"Cut to clipboard: {_selectedNode.Name}");
        }

        /// <summary>
        /// Paste clipboard contents to current directory
        /// </summary>
        public async Task<bool> PasteAsync()
        {
            if (_pendingOperation == FileOperation.None || _operationPaths.Count == 0)
            {
                await OnErrorAsync("Nothing to paste.");
                return false;
            }

            try
            {
                foreach (var sourcePath in _operationPaths)
                {
                    var fileName = System.IO.Path.GetFileName(sourcePath);
                    var targetPath = System.IO.Path.Combine(_currentPath, fileName).Replace('\\', '/');

                    if (_pendingOperation == FileOperation.Copy)
                    {
                        await CopyItemAsync(sourcePath, targetPath);
                    }
                    else if (_pendingOperation == FileOperation.Cut)
                    {
                        await MoveItemAsync(sourcePath, targetPath);
                    }
                }

                await OnOutputAsync($"Paste operation completed.");
                
                if (_pendingOperation == FileOperation.Cut)
                {
                    // Clear clipboard after cut operation
                    _pendingOperation = FileOperation.None;
                    _operationPaths.Clear();
                }

                await RefreshDirectoryAsync();
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Paste operation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Rename selected item
        /// </summary>
        public async Task<bool> RenameSelectedAsync(string newName)
        {
            if (_selectedNode == null)
            {
                await OnErrorAsync("No item selected.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                await OnErrorAsync("New name cannot be empty.");
                return false;
            }

            try
            {
                var newPath = System.IO.Path.Combine(_currentPath, newName).Replace('\\', '/');
                await MoveItemAsync(_selectedNode.FullPath, newPath);
                
                await OnOutputAsync($"Renamed '{_selectedNode.Name}' to '{newName}'");
                _selectedNode = null;
                await RefreshDirectoryAsync();
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Rename failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get current directory contents for UI display
        /// </summary>
        public IEnumerable<VirtualFileSystemNode> GetCurrentContents()
        {
            lock (_lockObject)
            {
                return _currentContents.ToList();
            }
        }

        /// <summary>
        /// Get current path
        /// </summary>
        public string GetCurrentPath()
        {
            return _currentPath;
        }

        /// <summary>
        /// Get selected node information
        /// </summary>
        public VirtualFileSystemNode? GetSelectedNode()
        {
            return _selectedNode;
        }        /// <summary>
        /// Get file/directory properties
        /// </summary>
        public async Task<string> GetPropertiesAsync(string name)
        {
            try
            {
                // Await a completed task to make this method truly async
                await Task.CompletedTask;
                
                var node = _currentContents.FirstOrDefault(n => n.Name == name);
                if (node == null)
                {
                    return "Item not found.";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Name: {node.Name}");
                sb.AppendLine($"Type: {(node.IsDirectory ? "Directory" : "File")}");
                sb.AppendLine($"Path: {node.FullPath}");
                sb.AppendLine($"Created: {node.CreatedTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Modified: {node.ModifiedTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Owner: {node.OwnerId}");
                sb.AppendLine($"Group: {node.GroupId}");
                sb.AppendLine($"Permissions: {node.Permissions?.ToString() ?? "N/A"}");
                
                if (!node.IsDirectory && node is VirtualFile file)
                {
                    sb.AppendLine($"Size: {file.Size:N0} bytes");
                    sb.AppendLine($"MIME Type: {file.MimeType ?? "Unknown"}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting properties: {ex.Message}";
            }
        }

        private async Task CopyItemAsync(string sourcePath, string targetPath)
        {
            var user = Context?.UserSession?.GetUser();
            
            if (user == null)
            {
                await OnErrorAsync("No user context available for file operation.");
                return;
            }
            
            if (await _fileSystem.FileExistsAsync(sourcePath, user))
            {
                var content = await _fileSystem.ReadFileAsync(sourcePath, user);
                if (content != null)
                {
                    await _fileSystem.WriteFileAsync(targetPath, content, user);
                }
            }
            else if (await _fileSystem.DirectoryExistsAsync(sourcePath, user))
            {
                // TODO: Implement recursive directory copying
                await _fileSystem.CreateDirectoryAsync(targetPath, user);
            }
        }

        private async Task MoveItemAsync(string sourcePath, string targetPath)
        {
            var user = Context?.UserSession?.GetUser();
            
            if (user == null)
            {
                await OnErrorAsync("No user context available for file operation.");
                return;
            }
            
            if (await _fileSystem.FileExistsAsync(sourcePath, user))
            {
                var content = await _fileSystem.ReadFileAsync(sourcePath, user);
                if (content != null)
                {
                    await _fileSystem.WriteFileAsync(targetPath, content, user);
                    await _fileSystem.DeleteFileAsync(sourcePath, user);
                }
            }
            else if (await _fileSystem.DirectoryExistsAsync(sourcePath, user))
            {
                // TODO: Implement recursive directory moving
                await _fileSystem.CreateDirectoryAsync(targetPath, user);
                await _fileSystem.DeleteDirectoryAsync(sourcePath, user, recursive: false);
            }
        }

        private void OnFileSystemChanged(object? sender, HackerOs.OS.IO.FileSystem.FileSystemEventArgs e)
        {
            // Refresh directory if the change affects current directory
            if (e.Path.StartsWith(_currentPath))
            {
                Task.Run(async () => await RefreshDirectoryAsync());
            }
        }
    }
}
