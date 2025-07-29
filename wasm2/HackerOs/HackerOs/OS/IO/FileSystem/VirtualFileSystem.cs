using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.User;
using System.Linq;
using UserEntity = HackerOs.OS.User.User;
using IOPath = System.IO.Path;

namespace HackerOs.OS.IO.FileSystem
{    
    public partial class VirtualFileSystem : IVirtualFileSystem, IFileSystem
    {
        private readonly Dictionary<string, VirtualFileSystemNode> _fileSystem = new();
        private readonly string _rootPath = "/";
        private string _currentWorkingDirectory = "/";

        public event EventHandler<FileSystemEventArgs>? FileSystemChanged;

        protected virtual void OnFileSystemChanged(FileSystemEventArgs e)
        {
            FileSystemChanged?.Invoke(this, e);
        }

        public Task<bool> InitializeAsync()
        {
            if (_fileSystem.Count > 0)
                return Task.FromResult(false);

            var rootNode = new VirtualDirectory
            {
                Name = _rootPath,
                CreatedAt = DateTime.UtcNow, // Root directory created at system start
                ModifiedAt = DateTime.UtcNow,
                AccessedAt = DateTime.UtcNow,
                Permissions = FilePermissions.Common.OwnerOnly,
                Owner = "root" // Root owned
            };

            _fileSystem[_rootPath] = rootNode;

            return Task.FromResult(true);
        }

        public async Task<bool> IsDirectoryAsync(string path)
        {
            var node = await GetNodeAsync(path);
            return node?.IsDirectory ?? false;
        }

        public async Task<FileSystemEntry> GetEntryAsync(string path)
        {
            var node = await GetNodeAsync(path);
            if (node == null)
                throw new FileNotFoundException($"Path not found: {path}");
                
            return new FileSystemEntry
            {
                FullPath = path,
                Name = IOPath.GetFileName(path),
                IsDirectory = node.IsDirectory,
                Size = node.Size,
                CreationTime = node.CreatedTime,
                LastWriteTime = node.LastModifiedTime,
                LastAccessTime = node.LastAccessTime,
                Permissions = node.Permissions,
                Owner = node.Owner
            };
        }

        public async Task<IEnumerable<FileSystemEntry>> ListEntriesAsync(string path)
        {
            var node = await GetNodeAsync(path);
            if (node == null || !node.IsDirectory)
                throw new DirectoryNotFoundException($"Directory not found: {path}");

            var entries = new List<FileSystemEntry>();
            var directory = node as VirtualDirectory;
            if (directory == null)
                throw new InvalidOperationException($"Node at {path} is marked as a directory but is not a VirtualDirectory instance");

            foreach (var child in directory.Children.Values)
            {
                entries.Add(await GetEntryAsync(IOPath.Combine(path, child.Name)));
            }
            return entries;
        }
        

        public async Task CreateFileAsync(string path)
        {
            await CreateNodeAsync(path, isDirectory: false);
        }

        public async Task CreateDirectoryAsync(string path)
        {
            await CreateNodeAsync(path, isDirectory: true);
        }

        protected async Task<bool> CreateNodeAsync(string path, bool isDirectory)
        {
            var normalizedPath = NormalizePath(path);

            if (string.IsNullOrEmpty(normalizedPath))
                throw new ArgumentException("Path cannot be empty", nameof(path));

            if (normalizedPath == _rootPath && isDirectory)
                throw new UnauthorizedAccessException("Cannot create root directory");

            var node = await GetNodeAsync(normalizedPath);
            if (node != null)
                throw new IOException($"A file or directory already exists at path: {path}");

            var parentPath = IOPath.GetDirectoryName(normalizedPath);
            if (string.IsNullOrEmpty(parentPath))
                parentPath = _rootPath;

            var parent = await GetNodeAsync(parentPath);
            if (parent == null)
                throw new DirectoryNotFoundException($"Parent directory not found: {parentPath}");

            if (!parent.IsDirectory)
                throw new IOException($"Parent path is not a directory: {parentPath}");

            var fileName = IOPath.GetFileName(normalizedPath);
            VirtualFileSystemNode newNode;
            
            if (isDirectory)
            {
                newNode = new VirtualDirectory
                {
                    Name = fileName,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                    AccessedAt = DateTime.UtcNow,
                    Permissions = FilePermissions.Common.DirectoryDefault,
                    Owner = parent.Owner // Inherit owner from parent
                };
            }
            else
            {
                newNode = new VirtualFile
                {
                    Name = fileName,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                    AccessedAt = DateTime.UtcNow,
                    Permissions = FilePermissions.Common.FileDefault,
                    Owner = parent.Owner // Inherit owner from parent
                };
            }

            var parentDirectory = parent as VirtualDirectory;
            if (parentDirectory == null)
                throw new InvalidOperationException($"Parent node at {parentPath} is marked as a directory but is not a VirtualDirectory instance");
                
            parentDirectory.AddChild(newNode);
            _fileSystem[normalizedPath] = newNode;

            OnFileSystemChanged(new FileSystemEventArgs(normalizedPath, 
                isDirectory ? FileSystemChangeType.DirectoryCreated : FileSystemChangeType.FileCreated));
                
            return true;
        }

        public async Task DeleteAsync(string path, bool recursive = false)
        {
            var node = await GetNodeAsync(path);
            if (node == null)
                throw new FileNotFoundException($"Path not found: {path}");

            var directory = node as VirtualDirectory;
            if (node.IsDirectory && directory != null && directory.Children.Count > 0 && !recursive)
                throw new IOException($"Directory not empty: {path}");

            if (node.Parent != null)
            {
                var parentDirectory = node.Parent as VirtualDirectory;
                if (parentDirectory != null)
                {
                    parentDirectory.Children.Remove(node.Name);
                }
            }

            _fileSystem.Remove(path);

            OnFileSystemChanged(new FileSystemEventArgs(path, FileSystemChangeType.Deleted));
        }

        public async Task<Stream> OpenFileAsync(string path, FileMode mode, FileAccess access)
        {
            var node = await GetNodeAsync(path);
            if (node == null)
            {
                if (mode == FileMode.Create || mode == FileMode.CreateNew || mode == FileMode.OpenOrCreate)
                {
                    await CreateFileAsync(path);
                    node = await GetNodeAsync(path);
                }
                else
                {
                    throw new FileNotFoundException($"File not found: {path}");
                }
            }

            if (node == null || node.IsDirectory)
                throw new UnauthorizedAccessException($"Cannot open directory as file: {path}");

            var file = node as VirtualFile;
            if (file == null)
                throw new InvalidOperationException($"Node at {path} is marked as a file but is not a VirtualFile instance");

            // Return memory stream containing file data
            var stream = new MemoryStream(file.Content ?? Array.Empty<byte>());
            
            // Seek to end if append mode
            if (mode == FileMode.Append)
                stream.Seek(0, System.IO.SeekOrigin.End);
                
            return stream;
        }

        public async Task<byte[]> ReadFileAsync(string path)
        {
            var node = await GetNodeAsync(path);
            if (node == null)
                throw new FileNotFoundException($"File not found: {path}");

            if (node.IsDirectory)
                throw new UnauthorizedAccessException($"Cannot read directory as file: {path}");

            var file = node as VirtualFile;
            if (file == null)
                throw new InvalidOperationException($"Node at {path} is marked as a file but is not a VirtualFile instance");

            return file.Content ?? Array.Empty<byte>();
        }

        public async Task WriteFileAsync(string path, byte[] data)
        {
            var node = await GetNodeAsync(path);
            if (node == null)
                throw new FileNotFoundException($"File not found: {path}");

            if (node.IsDirectory)
                throw new UnauthorizedAccessException($"Cannot write to directory as file: {path}");

            var file = node as VirtualFile;
            if (file == null)
                throw new InvalidOperationException($"Node at {path} is marked as a file but is not a VirtualFile instance");

            file.Content = data;
            file.Size = data.Length;
            file.ModifiedAt = DateTime.UtcNow;
            
            OnFileSystemChanged(new FileSystemEventArgs(path, FileSystemChangeType.Modified));
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return _currentWorkingDirectory;

            // Convert to Unix-style paths
            path = path.Replace('\\', '/');

            // Make relative paths absolute
            if (!path.StartsWith("/"))
            {
                path = IOPath.Combine(_currentWorkingDirectory, path).Replace('\\', '/');
            }

            // Remove trailing slashes except for root
            if (path.Length > 1 && path.EndsWith("/"))
                path = path.TrimEnd('/');

            return path;
        }

        public Task<VirtualFileSystemNode?> GetNodeAsync(string path)
        {
            var normalizedPath = NormalizePath(path);
            _fileSystem.TryGetValue(normalizedPath, out var node);
            return Task.FromResult(node);
        }

        Task<bool> IVirtualFileSystem.CreateDirectoryAsync(string path)
        {
            throw new NotImplementedException();
        }

        Task<bool> IVirtualFileSystem.DeleteAsync(string path, bool recursive)
        {
            throw new NotImplementedException();
        }

        Task<bool> IVirtualFileSystem.WriteFileAsync(string path, byte[] content)
        {
            throw new NotImplementedException();
        }

        string IVirtualFileSystem.GetCurrentDirectory()
        {
            throw new NotImplementedException();
        }

        bool IVirtualFileSystem.Unmount(string mountPath, bool force)
        {
            throw new NotImplementedException();
        }

        string IVirtualFileSystem.GetMountInfo()
        {
            throw new NotImplementedException();
        }

        string IVirtualFileSystem.GetAbsolutePath(string path, string currentDirectory)
        {
            throw new NotImplementedException();
        }

        public Task EnablePersistenceAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Stream> OpenFileAsync(string path, FileMode mode, System.IO.FileAccess access)
        {
            throw new NotImplementedException();
        }

        Task<bool> IFileSystem.WriteFileAsync(string path, byte[] content)
        {
            throw new NotImplementedException();
        }

        public Task EnsureDirectoryExistsAsync(string path)
        {
            throw new NotImplementedException();
        }

        public Task WriteTextAsync(string filePath, string content, bool append = false)
        {
            throw new NotImplementedException();
        }

        public Task<bool> FileExistsAsync(string filePath)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<string>> GetFilesAsync(string directory, string? searchPattern = null)
        {
            throw new NotImplementedException();
        }

        internal void LogFileSystemEvent(FileSystemEventType fileWritten, string path, string v)
        {
            throw new NotImplementedException();
        }
    }
}
