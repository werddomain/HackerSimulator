using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.User;
using UserEntity = HackerOs.OS.User.User;

namespace HackerOs.OS.IO.FileSystem
{    /// <summary>
    /// Interface for the virtual file system providing Linux-like file operations.
    /// </summary>
    public interface IVirtualFileSystem
    {
        /// <summary>
        /// Event triggered when the file system changes (file created, deleted, modified, etc.)
        /// </summary>
        event EventHandler<FileSystemEventArgs>? FileSystemChanged;

        /// <summary>
        /// Initializes the virtual file system with standard directory structure.
        /// </summary>
        /// <returns>True if initialization was successful; otherwise, false.</returns>
        Task<bool> InitializeAsync();

        /// <summary>
        /// Gets a file system node at the specified path.
        /// </summary>
        /// <param name="path">The absolute or relative path to the node.</param>
        /// <returns>The file system node if found; otherwise, null.</returns>
        Task<VirtualFileSystemNode?> GetNodeAsync(string path);

        /// <summary>
        /// Creates a new file at the specified path.
        /// </summary>
        /// <param name="path">The path where the file should be created.</param>
        /// <param name="content">Optional initial content for the file.</param>
        /// <returns>True if the file was created successfully; otherwise, false.</returns>
        Task<bool> CreateFileAsync(string path, byte[]? content = null);

        /// <summary>
        /// Creates a new directory at the specified path.
        /// </summary>
        /// <param name="path">The path where the directory should be created.</param>
        /// <returns>True if the directory was created successfully; otherwise, false.</returns>
        Task<bool> CreateDirectoryAsync(string path);

        /// <summary>
        /// Deletes a file or directory at the specified path.
        /// </summary>
        /// <param name="path">The path to the item to delete.</param>
        /// <param name="recursive">Whether to delete directories recursively.</param>
        /// <returns>True if the item was deleted successfully; otherwise, false.</returns>
        Task<bool> DeleteAsync(string path, bool recursive = false);

        /// <summary>
        /// Checks if a file or directory exists at the specified path.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>True if the item exists; otherwise, false.</returns>
        Task<bool> ExistsAsync(string path);

        /// <summary>
        /// Lists the contents of a directory.
        /// </summary>
        /// <param name="path">The path to the directory.</param>
        /// <returns>A collection of file system nodes in the directory.</returns>
        Task<IEnumerable<VirtualFileSystemNode>> ListDirectoryAsync(string path);

        /// <summary>
        /// Reads the content of a file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>The file content as bytes; null if the file doesn't exist or is not a file.</returns>
        Task<byte[]?> ReadFileAsync(string path);

        /// <summary>
        /// Writes content to a file, creating it if it doesn't exist.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="content">The content to write.</param>
        /// <returns>True if the content was written successfully; otherwise, false.</returns>
        Task<bool> WriteFileAsync(string path, byte[] content);

        /// <summary>
        /// Moves a file or directory from source to destination.
        /// </summary>
        /// <param name="sourcePath">The current path of the item.</param>
        /// <param name="destinationPath">The new path for the item.</param>
        /// <returns>True if the item was moved successfully; otherwise, false.</returns>
        Task<bool> MoveAsync(string sourcePath, string destinationPath);

        /// <summary>
        /// Copies a file or directory from source to destination.
        /// </summary>
        /// <param name="sourcePath">The path of the item to copy.</param>
        /// <param name="destinationPath">The destination path for the copy.</param>
        /// <returns>True if the item was copied successfully; otherwise, false.</returns>
        Task<bool> CopyAsync(string sourcePath, string destinationPath);        /// <summary>
        /// Lists the contents of a directory with extended information asynchronously.
        /// </summary>
        /// <param name="path">The path to the directory.</param>
        /// <returns>A collection of file system nodes in the directory.</returns>
        Task<IEnumerable<VirtualFileSystemNode>> GetDirectoryContentsAsync(string path);

        /// <summary>
        /// Creates a symbolic link at the specified path pointing to the target.
        /// </summary>
        /// <param name="linkPath">The path where the symbolic link should be created.</param>
        /// <param name="targetPath">The path the symbolic link should point to.</param>
        /// <returns>True if the symbolic link was created successfully; otherwise, false.</returns>
        Task<bool> CreateSymbolicLinkAsync(string linkPath, string targetPath);

        /// <summary>
        /// Changes the current working directory.
        /// </summary>
        /// <param name="path">The new working directory path</param>
        /// <returns>True if the directory change was successful, false otherwise</returns>
        Task<bool> ChangeDirectoryAsync(string path);

        /// <summary>
        /// Gets the current working directory path.
        /// </summary>
        /// <returns>The current working directory path</returns>
        string GetCurrentDirectory();

        /// <summary>
        /// Saves the current file system state to persistent storage.
        /// </summary>
        /// <returns>True if save was successful, false otherwise</returns>
        Task<bool> SaveToPersistentStorageAsync();

        /// <summary>
        /// Loads the file system state from persistent storage.
        /// </summary>
        /// <returns>True if load was successful, false otherwise</returns>
        Task<bool> LoadFromPersistentStorageAsync();

        /// <summary>
        /// Mounts a file system at the specified path.
        /// </summary>
        /// <param name="source">The source or device to mount</param>
        /// <param name="mountPath">The path where the file system should be mounted</param>
        /// <param name="fileSystemType">The type of the file system</param>
        /// <param name="options">Mount options</param>
        /// <returns>True if the mount was successful; otherwise, false</returns>
        Task<bool> MountAsync(string source, string mountPath, string fileSystemType, MountOptions? options = null);

        /// <summary>
        /// Unmounts a file system from the specified path.
        /// </summary>
        /// <param name="mountPath">The path to unmount</param>
        /// <param name="force">Whether to force unmount even if busy</param>
        /// <returns>True if the unmount was successful; otherwise, false</returns>
        bool Unmount(string mountPath, bool force = false);

        /// <summary>
        /// Gets information about all mount points.
        /// </summary>
        /// <returns>A string containing mount information similar to /proc/mounts</returns>
        string GetMountInfo();

        /// <summary>
        /// Gets the absolute path from a relative or absolute path using the specified current directory.
        /// </summary>
        /// <param name="path">The path to resolve (can be relative or absolute).</param>
        /// <param name="currentDirectory">The current working directory to use for relative paths.</param>
        /// <returns>The absolute path.</returns>
        string GetAbsolutePath(string path, string currentDirectory);        /// <summary>
        /// Checks if a directory exists and the User has permission to access it.
        /// </summary>
        /// <param name="path">The path to the directory.</param>        /// <param name="user">The user checking access.</param>
        /// <returns>True if the directory exists and user can access it; otherwise, false.</returns>
        Task<bool> DirectoryExistsAsync(string path, UserEntity user);

        /// <summary>
        /// Checks if a file exists and the User has permission to access it.
        /// </summary>
        /// <param name="path">The path to the file.</param>        /// <param name="user">The user checking access.</param>
        /// <returns>True if the file exists and user can access it; otherwise, false.</returns>
        Task<bool> FileExistsAsync(string path, UserEntity user);

        /// <summary>
        /// Gets a file system node at the specified path with User permission checking.
        /// </summary>
        /// <param name="path">The absolute or relative path to the node.</param>        /// <param name="user">The user requesting the node.</param>
        /// <returns>The file system node if found and accessible; otherwise, null.</returns>
        Task<VirtualFileSystemNode?> GetNodeAsync(string path, UserEntity user);

        /// <summary>
        /// Reads the content of a file with User permission checking.
        /// </summary>
        /// <param name="path">The path to the file.</param>        /// <param name="user">The user reading the file.</param>
        /// <returns>The file content as a byte array; null if the file doesn't exist or user lacks permission.</returns>
        Task<byte[]?> ReadFileAsync(string path, UserEntity user);

        /// <summary>
        /// Writes content to a file with User permission checking, creating it if it doesn't exist.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="content">The content to write as a string.</param>        /// <param name="user">The user writing the file.</param>
        /// <returns>True if the content was written successfully; otherwise, false.</returns>
        Task<bool> WriteFileAsync(string path, string content, UserEntity user);

        /// <summary>
        /// Creates a new directory at the specified path with User permission checking.
        /// </summary>
        /// <param name="path">The path where the directory should be created.</param>        /// <param name="user">The user creating the directory.</param>
        /// <returns>True if the directory was created successfully; otherwise, false.</returns>
        Task<bool> CreateDirectoryAsync(string path, UserEntity user);

        /// <summary>
        /// Creates a new file at the specified path with User permission checking.
        /// </summary>
        /// <param name="path">The path where the file should be created.</param>        /// <param name="user">The user creating the file.</param>
        /// <param name="content">Optional initial content for the file.</param>
        /// <returns>True if the file was created successfully; otherwise, false.</returns>
        Task<bool> CreateFileAsync(string path, UserEntity user, string? content = null);

        /// <summary>
        /// Deletes a file at the specified path with User permission checking.
        /// </summary>
        /// <param name="path">The path to the file to delete.</param>        /// <param name="user">The user deleting the file.</param>
        /// <returns>True if the file was deleted successfully; otherwise, false.</returns>
        Task<bool> DeleteFileAsync(string path, UserEntity user);        /// <summary>
        /// Deletes a directory at the specified path with User permission checking.
        /// </summary>
        /// <param name="path">The path to the directory to delete.</param>
        /// <param name="user">The user deleting the directory.</param>
        /// <param name="recursive">Whether to delete directories recursively.</param>
        /// <returns>True if the directory was deleted successfully; otherwise, false.</returns>
        Task<bool> DeleteDirectoryAsync(string path, UserEntity user, bool recursive = false);

        /// <summary>
        /// Lists the contents of a directory with User permission checking.
        /// </summary>
        /// <param name="path">The path to the directory.</param>        /// <param name="user">The user listing the directory.</param>
        /// <returns>A collection of file system nodes in the directory that the user can access.</returns>
        Task<IEnumerable<VirtualFileSystemNode>> ListDirectoryAsync(string path, UserEntity user);

        /// <summary>
        /// Enables persistence for the file system.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task EnablePersistenceAsync();

        /// <summary>
        /// Reads text content from a file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>The file content as text, or null if the file doesn't exist.</returns>
        Task<string?> ReadTextAsync(string path);

        /// <summary>
        /// Writes text content to a file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="content">The text content to write.</param>
        /// <returns>True if the operation was successful.</returns>
        Task<bool> WriteTextAsync(string path, string content);

        /// <summary>
        /// Reads all text from a file with User permission checking.
        /// </summary>
        /// <param name="path">The path to the file.</param>        /// <param name="user">The user reading the file.</param>
        /// <returns>The file content as text, or null if the file doesn't exist or user lacks permission.</returns>
        Task<string?> ReadAllTextAsync(string path, UserEntity user);        /// <summary>
        /// Writes all text to a file with User permission checking.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="content">The text content to write.</param>        /// <param name="user">The user writing the file.</param>
        /// <returns>True if the operation was successful.</returns>
        Task<bool> WriteAllTextAsync(string path, string content, UserEntity user);

        /// <summary>
        /// Gets a file node at the specified path for shell operations.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>A file node if found and is a file; otherwise, null.</returns>
        VirtualFile? GetFile(string path);

        /// <summary>
        /// Gets a directory node at the specified path for shell operations.
        /// </summary>
        /// <param name="path">The path to the directory.</param>
        /// <returns>A directory node if found and is a directory; otherwise, null.</returns>
        VirtualDirectory? GetDirectory(string path);
        Task<byte[]> ReadAllBytesAsync(string filePath, UserEntity User);

        /// <summary>
        /// Event raised when file system operations occur.
        /// </summary>
        event EventHandler<FileSystemEvent>? OnFileSystemEvent;

        
    }

    /// <summary>
    /// Event arguments for file system operations.
    /// </summary>
    public class FileSystemEvent : EventArgs
    {
        public FileSystemEventType EventType { get; set; }
        public string Path { get; set; } = string.Empty;
        public string? SourcePath { get; set; }
        public string? TargetPath { get; set; }
        public string? Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Types of file system events.
    /// </summary>
    public enum FileSystemEventType
    {
        SystemInitialized,
        FileCreated,
        FileDeleted,
        FileRead,
        FileWritten,
        FileCopied,
        DirectoryCreated,
        DirectoryDeleted,
        DirectoryCopied,
        SymbolicLinkCreated,
        PermissionElevation,
        PermissionDenied,
        Error
    }
}
