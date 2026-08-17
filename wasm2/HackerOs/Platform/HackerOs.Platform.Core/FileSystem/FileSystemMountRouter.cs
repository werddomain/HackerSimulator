using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Platform.Core.FileSystem;

/// <summary>Routes paths to the most-specific provider mount on a segment boundary.</summary>
public sealed class FileSystemMountRouter : IFileSystemMountRouter
{
    private readonly FileSystemMount[] _mountsBySpecificity;

    /// <summary>Initializes a router with one root provider and optional projected mounts.</summary>
    public FileSystemMountRouter(
        IFileSystemProvider rootProvider,
        IEnumerable<FileSystemMount>? mounts = null)
    {
        ArgumentNullException.ThrowIfNull(rootProvider);
        FileSystemMount root = new(VirtualPath.Parse("/"), rootProvider, isProtectedRoot: true);
        FileSystemMount[] supplied = mounts?.ToArray() ?? [];

        if (supplied.Any(static mount => mount.Path.Value == "/"))
        {
            throw new ArgumentException("The root provider is registered separately.", nameof(mounts));
        }

        FileSystemMount[] all = [root, .. supplied];
        string? duplicatePath = all
            .GroupBy(static mount => mount.Path.Value, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1)
            ?.Key;
        if (duplicatePath is not null)
        {
            throw new ArgumentException($"Duplicate filesystem mount path '{duplicatePath}'.", nameof(mounts));
        }

        Mounts = Array.AsReadOnly(all
            .OrderBy(static mount => mount.Path.Value.Length)
            .ThenBy(static mount => mount.Path.Value, StringComparer.Ordinal)
            .ToArray());
        _mountsBySpecificity = all
            .OrderByDescending(static mount => mount.Path.Value.Length)
            .ThenBy(static mount => mount.Path.Value, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<FileSystemMount> Mounts { get; }

    /// <inheritdoc />
    public FileSystemMountResolution Resolve(VirtualPath path)
    {
        if (string.IsNullOrEmpty(path.Value))
        {
            throw new ArgumentException("Mount routing requires a canonical path.", nameof(path));
        }

        FileSystemMount mount = _mountsBySpecificity.First(candidate => Matches(path, candidate.Path));
        return new FileSystemMountResolution(mount, path);
    }

    private static bool Matches(VirtualPath path, VirtualPath mountPath) =>
        mountPath.Value == "/"
        || path == mountPath
        || path.Value.StartsWith($"{mountPath.Value}/", StringComparison.Ordinal);
}