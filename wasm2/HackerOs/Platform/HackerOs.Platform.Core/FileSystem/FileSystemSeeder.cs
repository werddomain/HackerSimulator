using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Platform.Core.FileSystem;

/// <summary>Creates the required Linux-like clean-profile directory layout exactly once.</summary>
public sealed class FileSystemSeeder(IFileSystemService fileSystem, TimeProvider? timeProvider = null)
{
    private readonly IFileSystemService _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>Ensures the system layout and one user's standard directories exist.</summary>
    public async ValueTask SeedAsync(
        string userId,
        string primaryGroupId,
        CancellationToken cancellationToken = default)
    {
        FileSystemEntryName userName = FileSystemEntryName.Parse(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryGroupId);

        FileSystemAuthorizationContext system = CreateContext("system", "system");
        FileSystemAuthorizationContext userProvisioning = CreateContext(userName.Value, primaryGroupId);

        await EnsureDirectoryAsync("/bin", 0x01ED, system, cancellationToken);
        await EnsureDirectoryAsync("/etc", 0x01ED, system, cancellationToken);
        await EnsureDirectoryAsync("/home", 0x01ED, system, cancellationToken);
        await EnsureDirectoryAsync("/tmp", 0x01FF, system, cancellationToken);
        await EnsureDirectoryAsync("/var", 0x01ED, system, cancellationToken);
        await EnsureDirectoryAsync("/var/log", 0x01ED, system, cancellationToken);

        string home = $"/home/{userName.Value}";
        await EnsureDirectoryAsync(home, 0x01C0, userProvisioning, cancellationToken);
        await EnsureDirectoryAsync($"{home}/Desktop", 0x01C0, userProvisioning, cancellationToken);
        await EnsureDirectoryAsync($"{home}/Documents", 0x01C0, userProvisioning, cancellationToken);
        await EnsureDirectoryAsync($"{home}/Downloads", 0x01C0, userProvisioning, cancellationToken);
        await EnsureDirectoryAsync($"{home}/.config", 0x01C0, userProvisioning, cancellationToken);
        await EnsureDirectoryAsync($"{home}/.config/hackeros", 0x01C0, userProvisioning, cancellationToken);
        await EnsureDirectoryAsync($"{home}/.config/apps", 0x01C0, userProvisioning, cancellationToken);
    }

    private async ValueTask EnsureDirectoryAsync(
        string pathValue,
        ushort mode,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken)
    {
        VirtualPath path = VirtualPath.Parse(pathValue);
        FileSystemResult<FileSystemEntrySnapshot> existing = await _fileSystem.StatAsync(
            new FileSystemStatRequest(path, FileSystemLinkBehavior.NoFollow),
            context,
            cancellationToken);
        if (existing.Succeeded)
        {
            if (existing.Value!.Metadata.Kind != FileSystemEntryKind.Directory)
            {
                throw new InvalidOperationException($"Seed path '{path}' exists but is not a directory.");
            }

            return;
        }

        if (existing.Error?.Code != FileSystemErrorCode.NotFound)
        {
            throw new InvalidOperationException(
                $"Cannot inspect seed path '{path}': {existing.Error?.Code}.");
        }

        VirtualPath parent = GetParent(path);
        FileSystemResult<FileSystemEntrySnapshot> parentResult = await _fileSystem.StatAsync(
            new FileSystemStatRequest(parent),
            context,
            cancellationToken);
        if (!parentResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Cannot inspect seed parent '{parent}': {parentResult.Error?.Code}.");
        }

        FileSystemMutationResult created = await _fileSystem.CreateAsync(
            new FileSystemCreateRequest(
                path,
                FileSystemEntryKind.Directory,
                FileSystemPermissions.FromMode(mode),
                parentResult.Value!.Metadata.Revision),
            context,
            cancellationToken);
        if (!created.Succeeded && created.Transaction.Error?.Code != FileSystemErrorCode.AlreadyExists)
        {
            throw new InvalidOperationException(
                $"Cannot create seed path '{path}': {created.Transaction.Error?.Code}.");
        }
    }

    private FileSystemAuthorizationContext CreateContext(string userId, string groupId)
    {
        AppOperationContext operation = new()
        {
            AppId = "org.hackeros.kernel",
            UserId = userId,
            UserAuthority = AppAuthority.System,
            GrantedCapabilities = new HashSet<string>(AppCapabilities.All, StringComparer.Ordinal),
            IsSystemOperation = true
        };
        return new FileSystemAuthorizationContext(
            operation,
            [groupId],
            _timeProvider.GetUtcNow().ToUniversalTime());
    }

    private static VirtualPath GetParent(VirtualPath path)
    {
        int separator = path.Value.LastIndexOf('/');
        return separator <= 0 ? VirtualPath.Parse("/") : VirtualPath.Parse(path.Value[..separator]);
    }
}