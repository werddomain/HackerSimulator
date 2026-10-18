using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Metrics;
using HackerOs.Simulation.Abstractions.Processes;

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
/// <param name="CpuAccountant">
/// Measured CPU per process (ADR 0066, 0067), behind <c>Cpu</c> and <c>CpuSource</c> of
/// <c>/proc/&lt;pid&gt;/status</c>; missing or not measuring, the simulated value applies.
/// </param>
/// <param name="MemoryAccountant">
/// Measured memory per process (ADR 0066, 0068), behind <c>VmRSS</c>, the layer fields
/// (<c>RssAnon</c>, <c>RssFile</c>, <c>VmData</c>, <c>VmExe</c>, <c>HackAllocBytes</c>) and
/// <c>MemSource</c> of <c>/proc/&lt;pid&gt;/status</c>, and the measured lines of
/// <c>/proc/meminfo</c>; missing or not measuring, the simulated value applies.
/// </param>
/// <param name="Workers">
/// Lazily resolves the Web Worker pool (ADR 0070, 0071) behind <c>/proc/hackeros/workers</c>; the
/// directory does not exist when it is missing or returns <see langword="null"/> (a host without
/// workers, such as the server).
/// </param>
/// <param name="Listeners">
/// Lazily resolves the table of listening ports (ADR 0074, section 2) behind <c>/proc/net/tcp</c>;
/// missing, the file lists no socket.
/// </param>
public sealed record ProcMetricsSources(
    ICpuTimeAccounting? Cpu = null,
    IProcessIoAccounting? ProcessIo = null,
    IBlockDeviceStatistics? Disk = null,
    INetworkInterfaceStatistics? Network = null,
    Func<IFileSystemMountRouter>? Mounts = null,
    ICpuTimeAccountant? CpuAccountant = null,
    IMemoryAccountant? MemoryAccountant = null,
    Func<HackerOs.Simulation.Abstractions.Workers.IWorkerPoolStatus?>? Workers = null,
    Func<HackerOs.Platform.Core.Network.Local.ListeningPortTable?>? Listeners = null)
{
    /// <summary>
    /// Lazily resolves the resource limits (ADR 0125) behind <c>/proc/&lt;pid&gt;/limits</c>; missing,
    /// every limit reads <c>unlimited</c>.
    /// </summary>
    public Func<IProcessResourceLimitTable?>? ResourceLimits { get; init; }

    /// <summary>
    /// Lazily resolves the open descriptors (ADR 0125, <c>RLIMIT_NOFILE</c>) behind
    /// <c>/proc/&lt;pid&gt;/fd</c>; missing, the directory is empty.
    /// </summary>
    public Func<IProcessFileDescriptorTable?>? FileDescriptors { get; init; }
}

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
