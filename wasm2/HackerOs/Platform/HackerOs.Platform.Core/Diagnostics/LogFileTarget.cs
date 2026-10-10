using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Platform.Core.Diagnostics;

/// <summary>
/// Identifies one log file under <c>/var/log</c> that <see cref="LogFileWriter"/> appends to, with
/// the Unix mode it is created with (ADR 0045, section 1). Every log is owned by <c>system:system</c>.
/// </summary>
public sealed record LogFileTarget
{
    /// <summary>Canonical <c>/var/log</c> directory.</summary>
    public static readonly VirtualPath LogDirectory = VirtualPath.Parse("/var/log");

    /// <summary>Canonical directory holding one log per app.</summary>
    public static readonly VirtualPath AppLogDirectory = VirtualPath.Parse("/var/log/apps");

    /// <summary>Platform log: every <c>ILogger</c> category, boot, and OS service errors. Mode <c>0644</c>.</summary>
    public static readonly LogFileTarget Syslog = new(VirtualPath.Parse("/var/log/syslog"), 0b110_100_100);

    /// <summary>Audit log: logins, <c>sudo</c>, permission approvals. Mode <c>0600</c>, like Linux.</summary>
    public static readonly LogFileTarget AuthLog = new(VirtualPath.Parse("/var/log/auth.log"), 0b110_000_000);

    private LogFileTarget(VirtualPath path, ushort mode)
    {
        Path = path;
        Mode = mode;
    }

    /// <summary>Gets the canonical path of the active log file.</summary>
    public VirtualPath Path { get; }

    /// <summary>Gets the Unix permission bits the file is created with.</summary>
    public ushort Mode { get; }

    /// <summary>Gets the directory containing the log.</summary>
    public VirtualPath Directory => VirtualPath.Parse(Path.Value[..Path.Value.LastIndexOf('/')]);

    /// <summary>
    /// Returns the per-app log <c>/var/log/apps/&lt;appId&gt;.log</c>, mode <c>0644</c>.
    /// </summary>
    /// <param name="appId">App identifier; must be a valid single file-name segment.</param>
    /// <exception cref="ArgumentException">The app ID cannot be used as a file name.</exception>
    public static LogFileTarget ForApp(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        FileSystemEntryName name = FileSystemEntryName.Parse($"{appId}.log");
        return new LogFileTarget(VirtualPath.Parse($"{AppLogDirectory.Value}/{name.Value}"), 0b110_100_100);
    }

    /// <summary>Returns the path of archive <paramref name="index"/> (<c>&lt;file&gt;.1</c> is the newest).</summary>
    /// <param name="index">Archive number, from 1 to <see cref="LogRotationPolicy.MaxArchives"/>.</param>
    public VirtualPath ArchivePath(int index)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, LogRotationPolicy.MaxArchives);
        return VirtualPath.Parse($"{Path.Value}.{index}");
    }
}
