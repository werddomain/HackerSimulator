using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HackerOs.OS.User;
using System.Linq;

namespace HackerOs.OS.IO.FileSystem;

public partial class VirtualFileSystem
{
    public async Task<bool> ExistsAsync(string path)
    {
        var node = await GetNodeAsync(path);
        return node != null;
    }

    public async Task<IEnumerable<VirtualFileSystemNode>> ListDirectoryAsync(string path)
    {
        var node = await GetNodeAsync(path);
        if (node == null || !node.IsDirectory)
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        return node.Children;
    }

    public async Task<bool> CreateFileAsync(string path, byte[]? content = null)
    {
        try
        {
            await CreateNodeAsync(path, false);
            if (content != null)
            {
                await WriteFileAsync(path, content);
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> WriteFileAsyncImpl(string path, byte[] content)
    {
        try
        {
            var node = await GetNodeAsync(path);
            if (node == null)
                throw new FileNotFoundException($"File not found: {path}");

            if (node.IsDirectory)
                throw new UnauthorizedAccessException($"Cannot write to directory as file: {path}");

            node.Data = content;
            node.Size = content.Length;
            node.LastModifiedTime = DateTime.UtcNow;
            
            OnFileSystemChanged(new FileSystemEventArgs(path, FileSystemChangeType.Modified));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> MoveAsync(string sourcePath, string destinationPath)
    {
        try
        {
            // Ensure source exists
            var sourceNode = await GetNodeAsync(sourcePath);
            if (sourceNode == null)
                return false;

            // Ensure destination parent directory exists
            var destParentPath = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrEmpty(destParentPath))
                destParentPath = _rootPath;

            var destParent = await GetNodeAsync(destParentPath);
            if (destParent == null || !destParent.IsDirectory)
                return false;

            // If destination exists, fail
            if (await ExistsAsync(destinationPath))
                return false;

            // Remove from source parent
            if (sourceNode.Parent != null)
                sourceNode.Parent.Children.Remove(sourceNode);

            // Update node info
            sourceNode.Name = Path.GetFileName(destinationPath);
            sourceNode.Parent = destParent;

            // Add to destination parent
            destParent.Children.Add(sourceNode);
            _fileSystem.Remove(sourcePath);
            _fileSystem[destinationPath] = sourceNode;

            OnFileSystemChanged(new FileSystemEventArgs(sourcePath, FileSystemChangeType.Deleted));
            OnFileSystemChanged(new FileSystemEventArgs(destinationPath, FileSystemChangeType.Created));

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> CopyAsync(string sourcePath, string destinationPath)
    {
        try
        {
            var sourceNode = await GetNodeAsync(sourcePath);
            if (sourceNode == null)
                return false;

            // Create new node at destination
            await CreateNodeAsync(destinationPath, sourceNode.IsDirectory);
            var destNode = await GetNodeAsync(destinationPath);
            if (destNode == null)
                return false;

            // Copy properties
            destNode.Data = sourceNode.Data?.ToArray(); // Make a copy of the data
            destNode.Size = sourceNode.Size;
            destNode.Permissions = sourceNode.Permissions;

            // If it's a directory, recursively copy children
            if (sourceNode.IsDirectory)
            {
                foreach (var child in sourceNode.Children)
                {
                    var childDestPath = Path.Combine(destinationPath, child.Name);
                    await CopyAsync(Path.Combine(sourcePath, child.Name), childDestPath);
                }
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public Task<string> GetCurrentDirectory()
    {
        return Task.FromResult(_currentWorkingDirectory);
    }

    public Task<bool> ChangeDirectoryAsync(string path)
    {
        try
        {
            var normalizedPath = NormalizePath(path);
            _currentWorkingDirectory = normalizedPath;
            return Task.FromResult(true);
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> SaveToPersistentStorageAsync()
    {
        // TODO: Implement storage persistence
        return Task.FromResult(true);
    }

    public Task<bool> LoadFromPersistentStorageAsync()
    {
        // TODO: Implement storage loading
        return Task.FromResult(true);
    }

    public Task<string> GetAbsolutePath(string path, string basePath = "")
    {
        try
        {
            if (string.IsNullOrEmpty(basePath))
                basePath = _currentWorkingDirectory;

            if (!path.StartsWith("/"))
            {
                path = Path.Combine(basePath, path).Replace('\\', '/');
            }

            return Task.FromResult(path);
        }
        catch (Exception)
        {
            return Task.FromResult(path);
        }
    }

    public Task<bool> CreateSymbolicLinkAsync(string linkPath, string targetPath)
    {
        // TODO: Implement symbolic links
        return Task.FromResult(false);
    }

    public Task<bool> MountAsync(string mountPoint, string source, string fileSystem, MountOptions? options = null)
    {
        // TODO: Implement mount points
        return Task.FromResult(false);
    }

    public Task<bool> Unmount(string mountPoint, bool force = false)
    {
        // TODO: Implement unmounting
        return Task.FromResult(false);
    }

    public Task<IEnumerable<MountInfo>> GetMountInfo()
    {
        // TODO: Implement mount info
        return Task.FromResult(Enumerable.Empty<MountInfo>());
    }

    public Task<IEnumerable<VirtualFileSystemNode>> GetDirectoryContentsAsync(string path)
    {
        return ListDirectoryAsync(path);
    }
}
