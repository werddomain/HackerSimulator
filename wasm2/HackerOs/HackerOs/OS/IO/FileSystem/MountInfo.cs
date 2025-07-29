namespace HackerOs.OS.IO.FileSystem;

/// <summary>
/// Information about a mounted file system
/// </summary>
public class MountInfo
{
    /// <summary>
    /// The mount point path
    /// </summary>
    public string MountPoint { get; set; } = string.Empty;

    /// <summary>
    /// The source file system
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// The type of file system
    /// </summary>
    public string FileSystem { get; set; } = string.Empty;

    /// <summary>
    /// Mount options in use
    /// </summary>
    public MountOptions Options { get; set; } = new();
}
