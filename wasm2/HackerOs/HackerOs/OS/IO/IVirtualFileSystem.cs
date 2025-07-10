using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;

namespace HackerOs.OS.IO;

/// <summary>
/// Interface for the virtual file system providing Linux-like file operations.
/// This is a wrapper interface that extends the IVirtualFileSystem in the FileSystem namespace.
/// </summary>
public interface IVirtualFileSystem : FileSystem.IVirtualFileSystem
{
    /// <summary>
    /// Gets directory entries for a given path
    /// </summary>
    /// <param name="path">The directory path</param>
    /// <param name="user">The user requesting the operation (for permission checks)</param>
    /// <returns>A list of file system entries</returns>
    new Task<List<FileSystemEntry>> GetDirectoryEntriesAsync(string path, User.User? user = null);
    
    /// <summary>
    /// Reads all text from a file
    /// </summary>
    /// <param name="path">The file path</param>
    /// <param name="user">The user requesting the operation (for permission checks)</param>
    /// <returns>The text content of the file</returns>
    new Task<string> ReadAllTextAsync(string path, User.User? user = null);
}

/// <summary>
/// Represents a mount point in the file system
/// </summary>
public class MountPoint
{
    /// <summary>
    /// The path where the file system is mounted
    /// </summary>
    public string Path { get; set; } = null!;

    /// <summary>
    /// The mounted file system
    /// </summary>
    public IFileSystem FileSystem { get; set; } = null!;
}
