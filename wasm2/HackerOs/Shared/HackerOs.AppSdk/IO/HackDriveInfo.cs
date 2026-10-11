namespace HackerOs.AppSdk.IO;

/// <summary>Kind of a drive, the subset of <c>System.IO.DriveType</c> HackerOS produces.</summary>
public enum HackDriveType
{
    /// <summary>A fixed disk: the IndexedDB disk behind <c>/</c>.</summary>
    Fixed
}

/// <summary>
/// What the OS knows about one sized mount, as an app instance receives it from
/// <see cref="IAppDriveGateway"/>: the raw <c>statfs</c> answer behind <see cref="HackDriveInfo"/>.
/// </summary>
/// <param name="Name">Mount point, e.g. <c>/</c>.</param>
/// <param name="DriveFormat">Filesystem type from <c>/proc/mounts</c>, e.g. <c>idbfs</c>.</param>
/// <param name="VolumeLabel">Device name, e.g. <c>hda</c>.</param>
/// <param name="TotalSize">Size of the disk, in bytes.</param>
/// <param name="AvailableFreeSpace">Free bytes, never negative.</param>
public sealed record HackDriveSnapshot(
    string Name,
    string DriveFormat,
    string VolumeLabel,
    long TotalSize,
    long AvailableFreeSpace);

/// <summary>
/// Space of the mounted disks as one app instance sees it (ADR 0057, section 7), behind
/// <see cref="HackDriveInfo"/>. Reached through <see cref="IAppExecutionContext.Drives"/>. Needs no
/// capability, like <c>df</c> under Linux.
/// </summary>
public interface IAppDriveGateway
{
    /// <summary>Gets every mount that has a size, today only <c>/</c>.</summary>
    ValueTask<IReadOnlyList<HackDriveSnapshot>> GetDrivesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Space and format of a mounted disk, modeled on <c>System.IO.DriveInfo</c> (ADR 0057, section 7).
/// Under Linux the space of a disk is not a file but a <c>statfs</c> call; this is its .NET-shaped
/// equivalent. No capability is needed.
/// </summary>
/// <example>
/// <code>
/// foreach (HackDriveInfo drive in await HackDriveInfo.GetDrivesAsync())
/// {
///     Console.WriteLine($"{drive.Name} {drive.DriveFormat} {drive.AvailableFreeSpace}/{drive.TotalSize}");
/// }
/// </code>
/// </example>
/// <remarks>
/// Unlike <c>DriveInfo</c>, the values are read once, when the instance is created: the browser
/// only answers asynchronously, so a property cannot query it. Call <see cref="GetDrivesAsync"/>
/// again for fresh numbers.
/// </remarks>
public sealed class HackDriveInfo
{
    private HackDriveInfo(HackDriveSnapshot snapshot)
    {
        Name = snapshot.Name;
        DriveFormat = snapshot.DriveFormat;
        VolumeLabel = snapshot.VolumeLabel;
        TotalSize = snapshot.TotalSize;
        AvailableFreeSpace = Math.Max(0, snapshot.AvailableFreeSpace);
    }

    /// <summary>Gets the mount point, e.g. <c>/</c>.</summary>
    public string Name { get; }

    /// <summary>Gets the filesystem type, e.g. <c>idbfs</c>.</summary>
    public string DriveFormat { get; }

    /// <summary>Gets the kind of drive; always <see cref="HackDriveType.Fixed"/>.</summary>
    public HackDriveType DriveType => HackDriveType.Fixed;

    /// <summary>Gets whether the drive is ready; a mounted drive always is.</summary>
    public bool IsReady => true;

    /// <summary>Gets the device name, e.g. <c>hda</c>.</summary>
    public string VolumeLabel { get; }

    /// <summary>Gets the size of the drive, in bytes.</summary>
    public long TotalSize { get; }

    /// <summary>Gets the free space available to the app, in bytes; never negative.</summary>
    public long AvailableFreeSpace { get; }

    /// <summary>Gets the total free space, in bytes; HackerOS reserves no blocks, so it equals <see cref="AvailableFreeSpace"/>.</summary>
    public long TotalFreeSpace => AvailableFreeSpace;

    /// <summary>Gets the root directory of the drive.</summary>
    public HackDirectoryInfo RootDirectory => new(Name);

    /// <summary>Gets every mounted drive that has a size, today only <c>/</c>.</summary>
    /// <exception cref="HackExecutionContextMissingException">Called outside an app's execution context.</exception>
    public static async Task<HackDriveInfo[]> GetDrivesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<HackDriveSnapshot> drives = await HackExecutionContext.Current.Drives
            .GetDrivesAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. drives.Select(static drive => new HackDriveInfo(drive))];
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}
