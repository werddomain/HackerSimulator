namespace HackerOs.OS.IO.FileSystem;

/// <summary>
/// Options for mounting a file system
/// </summary>
public class MountOptions
{
    /// <summary>
    /// Whether to mount the file system read-only
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Whether to allow execution of files on this file system
    /// </summary>
    public bool NoExec { get; set; }

    /// <summary>
    /// Whether to allow device files on this file system
    /// </summary>
    public bool NoDev { get; set; }

    /// <summary>
    /// Whether to update access times for files
    /// </summary>
    public bool NoAtime { get; set; }

    /// <summary>
    /// Whether to allow setuid binaries on this file system
    /// </summary>
    public bool NoSuid { get; set; }

    public override string ToString()
    {
        var options = new List<string>();
        if (ReadOnly) options.Add("ro");
        if (NoExec) options.Add("noexec");
        if (NoDev) options.Add("nodev");
        if (NoAtime) options.Add("noatime");
        if (NoSuid) options.Add("nosuid");

        return string.Join(",", options);
    }
}
