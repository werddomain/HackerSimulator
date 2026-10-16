using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Metrics;

namespace HackerOs.Platform.Core.FileSystem.Virtual;

/// <summary>
/// Counters behind the measurement files of <c>/proc</c> (ADR 0057). Every member is optional: a
/// missing source renders its file with zeros (or, for <c>mounts</c>, only <c>/</c>), so older
/// wiring and tests keep working.
/// </summary>
/// <param name="Cpu">CPU time per core, behind <c>/proc/stat</c>.</param>
/// <param name="ProcessIo">Per-process I/O, behind <c>/proc/&lt;pid&gt;/io</c>.</param>
/// <param name="Disk">Statistics of <c>hda</c>, behind <c>/proc/diskstats</c>.</param>
/// <param name="Network">Interface counters, behind <c>/proc/net/dev</c>.</param>
/// <param name="Mounts">
/// Lazily resolves the mount table behind <c>/proc/mounts</c>. Lazy because the router owns this
/// very provider: resolving it eagerly would close a construction cycle.
/// </param>
public sealed record ProcMetricsSources(
    ICpuTimeAccounting? Cpu = null,
    IProcessIoAccounting? ProcessIo = null,
    IBlockDeviceStatistics? Disk = null,
    INetworkInterfaceStatistics? Network = null,
    Func<IFileSystemMountRouter>? Mounts = null);

/// <summary>One line of <c>/proc/mounts</c>: what is mounted where, with which type and options.</summary>
/// <param name="Source">Device or pseudo-source, e.g. <c>/dev/hda</c> or <c>proc</c>.</param>
/// <param name="MountPoint">Canonical mount path.</param>
/// <param name="Type">Filesystem type: <c>idbfs</c>, <c>proc</c> or <c>devfs</c>.</param>
/// <param name="Options">Mount options: <c>rw</c> or <c>ro</c>.</param>
public sealed record MountDescription(string Source, string MountPoint, string Type, string Options)
{
    /// <summary>Type of the persistent IndexedDB filesystem, specific to HackerOS.</summary>
    public const string IndexedDbType = "idbfs";

    /// <summary>
    /// Describes one router mount the way <c>/proc/mounts</c> lists it (ADR 0057 section 6): the
    /// <c>procfs</c> and <c>devfs</c> providers by their Linux-style pseudo-source, everything
    /// else as the persistent disk <c>/dev/hda</c>.
    /// </summary>
    public static MountDescription From(FileSystemMount mount)
    {
        ArgumentNullException.ThrowIfNull(mount);
        return mount.Provider.ProviderId switch
        {
            "procfs" => new MountDescription("proc", mount.Path.Value, "proc", "ro"),
            "devfs" => new MountDescription("devfs", mount.Path.Value, "devfs", "rw"),
            _ => new MountDescription("/dev/hda", mount.Path.Value, IndexedDbType, "rw")
        };
    }
}
