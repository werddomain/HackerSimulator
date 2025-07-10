using System.IO;

namespace HackerOs.OS.IO;

/// <summary>
/// Represents an entry in the file system
/// </summary>
public class FileSystemEntry
{
    /// <summary>
    /// The full path of the entry
    /// </summary>
    public string FullPath { get; set; } = null!;

    /// <summary>
    /// The name of the entry
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Whether this is a directory
    /// </summary>
    public bool IsDirectory { get; set; }

    /// <summary>
    /// The size of the file in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// When the file was created
    /// </summary>
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// When the file was last modified
    /// </summary>
    public DateTime LastWriteTime { get; set; }

    /// <summary>
    /// When the file was last accessed
    /// </summary>
    public DateTime LastAccessTime { get; set; }

    /// <summary>
    /// File attributes
    /// </summary>
    public FileAttributes Attributes { get; set; }

    /// <summary>
    /// File permissions
    /// </summary>
    public FilePermissions Permissions { get; set; } = new();

    /// <summary>
    /// The file owner's username
    /// </summary>
    public string Owner { get; set; } = null!;

    /// <summary>
    /// The file group
    /// </summary>
    public string Group { get; set; } = null!;
}
