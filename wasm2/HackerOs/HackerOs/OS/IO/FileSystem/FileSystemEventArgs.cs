using System;

namespace HackerOs.OS.IO.FileSystem;

/// <summary>
/// Types of file system events
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

/// <summary>
/// Event arguments for file system operations
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
/// Legacy event args for backwards compatibility
/// </summary>
public class FileSystemEventArgs : EventArgs
{
    private readonly string path;

    /// <summary>
    /// Gets the path of the file or directory that triggered the event
    /// </summary>
    public string FilePath => path;

    /// <summary>
    /// Gets the type of change that occurred
    /// </summary>
    public FileSystemChangeType ChangeType { get; }

    /// <summary>
    /// Gets the name of the file or directory that changed
    /// </summary>
    public string Name => HSystem.IO.HPath.GetFileName(path);

    /// <summary>
    /// Initializes a new instance of the FileSystemEventArgs class
    /// </summary>
    public FileSystemEventArgs(string path, FileSystemChangeType changeType)
    {
        this.path = path ?? throw new ArgumentNullException(nameof(path));
        ChangeType = changeType;
    }
}

/// <summary>
/// Types of file system changes
/// </summary>
public enum FileSystemChangeType
{
    Created,
    Modified,
    Deleted,
    FileCreated,
    DirectoryCreated
}
