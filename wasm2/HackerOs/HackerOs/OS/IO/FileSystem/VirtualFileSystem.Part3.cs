using System;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using UserEntity = HackerOs.OS.User.User;

namespace HackerOs.OS.IO.FileSystem;

public partial class VirtualFileSystem
{
    public event EventHandler<FileSystemEvent>? OnFileSystemEvent;

    protected virtual void RaiseFileSystemEvent(FileSystemEventType eventType, string path, string? sourcePath = null, string? targetPath = null, string? message = null)
    {
        var fsEvent = new FileSystemEvent
        {
            EventType = eventType,
            Path = path,
            SourcePath = sourcePath,
            TargetPath = targetPath,
            Message = message
        };

        OnFileSystemEvent?.Invoke(this, fsEvent);
    }

    public Task<bool> DirectoryExistsAsync(string path, UserEntity user)
    {
        return CheckUserAccessAndExecuteAsync(path, user, FilePermissions.Read, async () =>
        {
            var node = await GetNodeAsync(path);
            return node?.IsDirectory ?? false;
        });
    }

    public Task<bool> FileExistsAsync(string path, UserEntity user)
    {
        return CheckUserAccessAndExecuteAsync(path, user, FilePermissions.Read, async () =>
        {
            var node = await GetNodeAsync(path);
            return node != null && !node.IsDirectory;
        });
    }

    public async Task<VirtualFileSystemNode?> GetNodeAsync(string path, UserEntity user)
    {
        var result = await CheckUserAccessAndExecuteAsync(path, user, FilePermissions.Read, async () =>
        {
            return await GetNodeAsync(path);
        });

        return result;
    }

    public Task<byte[]?> ReadFileAsync(string path, UserEntity user)
    {
        return CheckUserAccessAndExecuteAsync(path, user, FilePermissions.Read, async () =>
        {
            var node = await GetNodeAsync(path);
            if (node == null || node.IsDirectory)
                return null;

            RaiseFileSystemEvent(FileSystemEventType.FileRead, path);
            return node.Data;
        });
    }

    public async Task<bool> WriteFileAsync(string path, string content, UserEntity user)
    {
        return await CheckUserAccessAndExecuteAsync(path, user, FilePermissions.Write, async () =>
        {
            var node = await GetNodeAsync(path);
            if (node == null)
            {
                await CreateFileAsync(path, user, content);
                return true;
            }

            if (node.IsDirectory)
                return false;

            node.Data = Encoding.UTF8.GetBytes(content);
            node.Size = node.Data.Length;
            node.LastModifiedTime = DateTime.UtcNow;

            RaiseFileSystemEvent(FileSystemEventType.FileWritten, path);
            return true;
        });
    }

    public Task<bool> CreateDirectoryAsync(string path, UserEntity user)
    {
        return CheckUserAccessAndExecuteAsync(GetParentPath(path), user, FilePermissions.Write, async () =>
        {
            var result = await CreateNodeAsync(path, true);
            if (result)
            {
                RaiseFileSystemEvent(FileSystemEventType.DirectoryCreated, path);
            }
            return result;
        });
    }

    public Task<bool> CreateFileAsync(string path, UserEntity user, string? content = null)
    {
        return CheckUserAccessAndExecuteAsync(GetParentPath(path), user, FilePermissions.Write, async () =>
        {
            var data = content != null ? Encoding.UTF8.GetBytes(content) : null;
            var result = await CreateNodeAsync(path, false);
            if (result && data != null)
            {
                var node = await GetNodeAsync(path);
                if (node != null)
                {
                    node.Data = data;
                    node.Size = data.Length;
                    node.Owner = user;
                }
            }
            
            if (result)
            {
                RaiseFileSystemEvent(FileSystemEventType.FileCreated, path);
            }
            return result;
        });
    }

    public Task<bool> DeleteFileAsync(string path, UserEntity user)
    {
        return CheckUserAccessAndExecuteAsync(path, user, FilePermissions.Write, async () =>
        {
            var node = await GetNodeAsync(path);
            if (node == null || node.IsDirectory)
                return false;

            var result = await DeleteAsync(path, false);
            if (result)
            {
                RaiseFileSystemEvent(FileSystemEventType.FileDeleted, path);
            }
            return result;
        });
    }

    public Task<bool> DeleteDirectoryAsync(string path, UserEntity user, bool recursive = false)
    {
        return CheckUserAccessAndExecuteAsync(path, user, FilePermissions.Write, async () =>
        {
            var node = await GetNodeAsync(path);
            if (node == null || !node.IsDirectory)
                return false;

            var result = await DeleteAsync(path, recursive);
            if (result)
            {
                RaiseFileSystemEvent(FileSystemEventType.DirectoryDeleted, path);
            }
            return result;
        });
    }

    public Task<IEnumerable<VirtualFileSystemNode>> ListDirectoryAsync(string path, UserEntity user)
    {
        return CheckUserAccessAndExecuteAsync(path, user, FilePermissions.Read, async () =>
        {
            return await ListDirectoryAsync(path);
        });
    }

    public async Task<string?> ReadTextAsync(string path)
    {
        var data = await ReadFileAsync(path);
        return data != null ? Encoding.UTF8.GetString(data) : null;
    }

    public Task<bool> WriteTextAsync(string path, string content)
    {
        return WriteFileAsync(path, Encoding.UTF8.GetBytes(content));
    }

    public Task<string?> ReadAllTextAsync(string path, UserEntity user)
    {
        return CheckUserAccessAndExecuteAsync(path, user, FilePermissions.Read, async () =>
        {
            return await ReadTextAsync(path);
        });
    }

    public Task<bool> WriteAllTextAsync(string path, string content, UserEntity user)
    {
        return WriteFileAsync(path, content, user);
    }

    public VirtualFile? GetFile(string path)
    {
        var node = GetNodeAsync(path).Result;
        if (node == null || node.IsDirectory)
            return null;

        return new VirtualFile(path, this, node);
    }

    public VirtualDirectory? GetDirectory(string path)
    {
        var node = GetNodeAsync(path).Result;
        if (node == null || !node.IsDirectory)
            return null;

        return new VirtualDirectory(path, this, node);
    }

    private async Task<bool> CheckUserAccess(string path, UserEntity user, FilePermissions requiredPermissions)
    {
        var node = await GetNodeAsync(path);
        if (node == null)
            return true; // Allow operations on non-existent paths for creation

        // Root can do anything
        if (user.IsRoot)
            return true;

        // Check owner permissions
        if (node.Owner?.Id == user.Id)
        {
            if ((node.Permissions & requiredPermissions) != 0)
                return true;
        }

        // TODO: Implement group permissions

        // Check others permissions
        var othersPermissions = node.Permissions & FilePermissions.OthersAll;
        if ((othersPermissions & requiredPermissions) != 0)
            return true;

        RaiseFileSystemEvent(FileSystemEventType.PermissionDenied, path, message: $"Access denied for user {user.Name}");
        return false;
    }

    private async Task<T> CheckUserAccessAndExecuteAsync<T>(string path, UserEntity user, FilePermissions requiredPermissions, Func<Task<T>> operation)
    {
        if (!await CheckUserAccess(path, user, requiredPermissions))
            throw new UnauthorizedAccessException($"Access denied to {path} for user {user.Name}");

        return await operation();
    }

    private string GetParentPath(string path)
    {
        var parent = Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(parent) ? "/" : parent;
    }

    public Task<byte[]> ReadAllBytesAsync(string filePath, UserEntity user)
    {
        return CheckUserAccessAndExecuteAsync(filePath, user, FilePermissions.Read, async () =>
        {
            var data = await ReadFileAsync(filePath);
            if (data == null)
                throw new FileNotFoundException($"File not found: {filePath}");
            return data;
        });
    }
}
