using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.FileSystem;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Platform.Core.Tests.FileSystem;

public sealed class FileSystemAuthorizationTests
{
    private static readonly VirtualPath Path = VirtualPath.Parse("/home/user/Documents/file.txt");
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly FileSystemAuthorizer _authorizer = new();

    [Fact]
    public void Exact_capability_and_owner_mode_permit_access()
    {
        FileSystemAuthorizationResult result = Authorize(
            CreateContext(AppAuthority.User, ["users"], AppCapabilities.FileSystemUserHomeRead),
            CreateMetadata("user", "users", 0x0180),
            AppCapabilities.FileSystemUserHomeRead,
            FileSystemAccess.Read,
            FileSystemHandleAccess.Read);

        Assert.True(result.Allowed);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Capability_matching_is_exact_and_case_sensitive()
    {
        FileSystemAuthorizationResult result = Authorize(
            CreateContext(AppAuthority.User, ["users"], "FILESYSTEM.USER-HOME.READ"),
            CreateMetadata("user", "users", 0x0180),
            AppCapabilities.FileSystemUserHomeRead,
            FileSystemAccess.Read,
            FileSystemHandleAccess.Read);

        Assert.False(result.Allowed);
        Assert.Equal(FileSystemErrorCode.CapabilityDenied, result.Error?.Code);
    }

    [Fact]
    public void System_authority_never_bypasses_exact_capability()
    {
        FileSystemAuthorizationContext context = new(
            CreateOperationContext(AppAuthority.User, []) with { IsSystemOperation = true },
            ["users"],
            Now);

        FileSystemAuthorizationResult result = Authorize(
            context,
            CreateMetadata("system", "system", 0x01FF),
            AppCapabilities.FileSystemSystemRead,
            FileSystemAccess.Read,
            FileSystemHandleAccess.Read,
            AppAuthority.System);

        Assert.Equal(FileSystemErrorCode.CapabilityDenied, result.Error?.Code);
    }

    [Fact]
    public void Group_mode_is_used_for_exact_group_membership()
    {
        FileSystemAuthorizationResult result = Authorize(
            CreateContext(AppAuthority.User, ["developers"], AppCapabilities.FileSystemUserHomeRead),
            CreateMetadata("other-user", "developers", 0x0020),
            AppCapabilities.FileSystemUserHomeRead,
            FileSystemAccess.Read,
            FileSystemHandleAccess.Read);

        Assert.True(result.Allowed);
    }

    [Fact]
    public void Selected_handle_replaces_broad_capability_only_within_scope_and_lifetime()
    {
        FileSystemSelectedResourceHandle handle = new(
            Guid.Parse("d9428888-122b-11e1-b85c-61cd3cbb3210"),
            "org.hackeros.editor",
            "user",
            VirtualPath.Parse("/home/user/Documents"),
            FileSystemHandleAccess.Read | FileSystemHandleAccess.Metadata,
            Now.AddMinutes(-1),
            Now.AddMinutes(1),
            4);
        FileSystemAuthorizationContext context = CreateContext(AppAuthority.User, ["users"], handle: handle);

        FileSystemAuthorizationResult inside = Authorize(
            context,
            CreateMetadata("user", "users", 0x0180),
            AppCapabilities.FileSystemUserHomeRead,
            FileSystemAccess.Read,
            FileSystemHandleAccess.Read);
        FileSystemAuthorizationResult outside = Authorize(
            context,
            CreateMetadata("user", "users", 0x0180),
            AppCapabilities.FileSystemUserHomeRead,
            FileSystemAccess.Read,
            FileSystemHandleAccess.Read,
            path: VirtualPath.Parse("/home/user/Downloads/file.txt"));

        Assert.True(inside.Allowed);
        Assert.Equal(FileSystemErrorCode.CapabilityDenied, outside.Error?.Code);
    }

    [Fact]
    public void Authority_and_mode_denials_are_distinct()
    {
        FileSystemAuthorizationContext context = CreateContext(
            AppAuthority.User,
            ["users"],
            AppCapabilities.FileSystemSystemWrite);
        FileSystemEntryMetadata metadata = CreateMetadata("other", "other", 0x0000);

        FileSystemAuthorizationResult authority = Authorize(
            context,
            metadata,
            AppCapabilities.FileSystemSystemWrite,
            FileSystemAccess.Write,
            FileSystemHandleAccess.Write,
            AppAuthority.Administrator);
        FileSystemAuthorizationResult mode = Authorize(
            context,
            metadata,
            AppCapabilities.FileSystemSystemWrite,
            FileSystemAccess.Write,
            FileSystemHandleAccess.Write);

        Assert.Equal(FileSystemErrorCode.AuthorityDenied, authority.Error?.Code);
        Assert.Equal(FileSystemErrorCode.PermissionDenied, mode.Error?.Code);
    }

    private FileSystemAuthorizationResult Authorize(
        FileSystemAuthorizationContext context,
        FileSystemEntryMetadata metadata,
        string capability,
        FileSystemAccess modeAccess,
        FileSystemHandleAccess handleAccess,
        AppAuthority authority = AppAuthority.User,
        VirtualPath? path = null) =>
        _authorizer.Authorize(new FileSystemAuthorizationRequest(
            FileSystemOperation.Read,
            path ?? Path,
            metadata,
            modeAccess,
            capability,
            handleAccess,
            authority,
            context));

    private static FileSystemAuthorizationContext CreateContext(
        AppAuthority authority,
        IEnumerable<string> groups,
        string? capability = null,
        FileSystemSelectedResourceHandle? handle = null) =>
        new(
            CreateOperationContext(authority, capability is null ? [] : [capability]),
            groups,
            Now,
            handle);

    private static AppOperationContext CreateOperationContext(
        AppAuthority authority,
        IEnumerable<string> capabilities) => new()
    {
        AppId = "org.hackeros.editor",
        UserId = "user",
        UserAuthority = authority,
        GrantedCapabilities = new HashSet<string>(capabilities, StringComparer.Ordinal)
    };

    private static FileMetadata CreateMetadata(string owner, string group, ushort mode)
    {
        DateTimeOffset timestamp = Now.AddHours(-1);
        return new FileMetadata(
            FileSystemEntryId.Parse("15f88b8c98a4479d9463d68867d35e15"),
            owner,
            group,
            FileSystemPermissions.FromMode(mode),
            new FileSystemTimestamps(timestamp, timestamp, timestamp),
            1,
            0);
    }
}