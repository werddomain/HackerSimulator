using HackerOs.App.Abstractions;

namespace HackerOs.Simulation.Abstractions.FileSystem;

/// <summary>Defines stable virtual path traversal limits.</summary>
public static class FileSystemTraversalPolicy
{
    /// <summary>Maximum symbolic links followed during one resolution.</summary>
    public const int MaximumSymbolicLinkHops = 40;
}

/// <summary>Contains the final canonical path and followed symbolic-link count.</summary>
/// <param name="Path">Resolved canonical path.</param>
/// <param name="FollowedSymbolicLinks">Number of followed symbolic links.</param>
public sealed record FileSystemPathResolution(
    VirtualPath Path,
    int FollowedSymbolicLinks);

/// <summary>Resolves symbolic links through the global mount namespace.</summary>
public interface IFileSystemPathResolver
{
    /// <summary>Resolves one canonical path with explicit final-link behavior.</summary>
    ValueTask<FileSystemResult<FileSystemPathResolution>> ResolveAsync(
        VirtualPath path,
        FileSystemLinkBehavior finalLinkBehavior,
        FileSystemOperation operation,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default);
}