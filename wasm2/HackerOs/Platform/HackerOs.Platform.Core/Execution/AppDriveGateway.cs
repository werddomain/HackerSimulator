using HackerOs.AppSdk.IO;
using HackerOs.Platform.Core.FileSystem.Virtual;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Metrics;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Trusted <see cref="IAppDriveGateway"/> behind <see cref="HackDriveInfo"/> (ADR 0057, section 7):
/// the OS's <c>statfs</c> for every mount that has a size.
/// </summary>
/// <remarks>
/// <para>
/// A mount has a size when its provider stores data, i.e. it is not one of the computed
/// <c>/proc</c> and <c>/dev</c> mounts (<see cref="IVirtualFileSystemProvider"/>). Today that is
/// only <c>/</c>, the IndexedDB disk <c>hda</c>.
/// </para>
/// <para>
/// <b>Size.</b> <see cref="VirtualHardwareProfile.StorageCapacityBytes"/>, lowered to the browser
/// quota when the browser grants less. <b>Free space.</b> Size minus the bytes the deduplicated
/// persistence stores, never negative. No capability is checked, as <c>df</c> needs none.
/// </para>
/// </remarks>
public sealed class AppDriveGateway : IAppDriveGateway
{
    private readonly Func<IFileSystemMountRouter> _mounts;
    private readonly VirtualHardwareProfile _hardware;
    private readonly IStorageUsageProvider? _usage;
    private readonly string _deviceName;

    /// <summary>Initializes the gateway.</summary>
    /// <param name="mounts">Lazily resolves the mount table (it is built after the execution factory).</param>
    /// <param name="hardware">Virtual hardware supplying the disk size.</param>
    /// <param name="usage">Measures the persistent filesystem; <see langword="null"/> reports an empty disk.</param>
    /// <param name="deviceName">Device name reported as the volume label; <c>hda</c> by default.</param>
    public AppDriveGateway(
        Func<IFileSystemMountRouter> mounts,
        VirtualHardwareProfile hardware,
        IStorageUsageProvider? usage = null,
        string deviceName = "hda")
    {
        _mounts = mounts ?? throw new ArgumentNullException(nameof(mounts));
        _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
        _usage = usage;
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        _deviceName = deviceName;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<HackDriveSnapshot>> GetDrivesAsync(CancellationToken cancellationToken = default)
    {
        // ADR 0067: time spent in the gateway is the caller's stime (kernel sub-frame).
        using HackerOs.AppSdk.HackExecutionContext.KernelScope kernel = HackerOs.AppSdk.HackExecutionContext.EnterKernel();
        cancellationToken.ThrowIfCancellationRequested();
        FileSystemMount[] sized =
        [
            .. _mounts().Mounts.Where(static mount => mount.Provider is not IVirtualFileSystemProvider)
        ];
        if (sized.Length == 0)
        {
            return [];
        }

        StorageUsage usage = _usage is null
            ? new StorageUsage(0, null)
            : await _usage.GetUsageAsync(cancellationToken).ConfigureAwait(false);
        long total = usage.QuotaBytes is > 0 and { } quota
            ? Math.Min(_hardware.StorageCapacityBytes, quota)
            : _hardware.StorageCapacityBytes;
        long free = Math.Max(0, total - Math.Max(0, usage.UsedBytes));

        return
        [
            .. sized.Select(mount => new HackDriveSnapshot(
                mount.Path.Value,
                MountDescription.From(mount).Type,
                _deviceName,
                total,
                free))
        ];
    }
}
