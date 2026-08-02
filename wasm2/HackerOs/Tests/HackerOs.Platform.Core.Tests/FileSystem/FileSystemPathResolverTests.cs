using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.FileSystem;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Platform.Core.Tests.FileSystem;

public sealed class FileSystemPathResolverTests
{
    [Fact]
    public async Task Relative_link_resolves_from_parent_and_reenters_mount_routing()
    {
        MetadataProvider root = new("root");
        MetadataProvider mounted = new("mounted");
        root.AddDirectory("/home");
        root.AddDirectory("/home/user");
        root.AddDirectory("/etc");
        root.AddLink("/home/user/config", "/etc/hackeros/settings.json");
        mounted.AddDirectory("/etc");
        mounted.AddDirectory("/etc/hackeros");
        mounted.AddFile("/etc/hackeros/settings.json");
        FileSystemPathResolver resolver = CreateResolver(root, mounted);

        FileSystemResult<FileSystemPathResolution> result = await resolver.ResolveAsync(
            VirtualPath.Parse("/home/user/config"),
            FileSystemLinkBehavior.Follow,
            FileSystemOperation.Read,
            CreateContext());

        Assert.True(result.Succeeded);
        Assert.Equal("/etc/hackeros/settings.json", result.Value?.Path.Value);
        Assert.Equal(1, result.Value?.FollowedSymbolicLinks);
        Assert.Contains("/etc/hackeros/settings.json", mounted.StatPaths);
    }

    [Fact]
    public async Task No_follow_preserves_the_final_link_for_delete_semantics()
    {
        MetadataProvider root = new("root");
        root.AddDirectory("/home");
        root.AddLink("/home/link", "/target");
        FileSystemPathResolver resolver = CreateResolver(root);

        FileSystemResult<FileSystemPathResolution> result = await resolver.ResolveAsync(
            VirtualPath.Parse("/home/link"),
            FileSystemLinkBehavior.NoFollow,
            FileSystemOperation.Delete,
            CreateContext());

        Assert.True(result.Succeeded);
        Assert.Equal("/home/link", result.Value?.Path.Value);
        Assert.Equal(0, result.Value?.FollowedSymbolicLinks);
    }

    [Fact]
    public async Task Repeated_link_identity_returns_loop_error()
    {
        MetadataProvider root = new("root");
        root.AddLink("/a", "/b", "15f88b8c98a4479d9463d68867d35e15");
        root.AddLink("/b", "/a", "6fa459eaee8a3ca4894e0db77e160355");
        FileSystemPathResolver resolver = CreateResolver(root);

        FileSystemResult<FileSystemPathResolution> result = await resolver.ResolveAsync(
            VirtualPath.Parse("/a"),
            FileSystemLinkBehavior.Follow,
            FileSystemOperation.Read,
            CreateContext());

        Assert.Equal(FileSystemErrorCode.SymbolicLinkLoop, result.Error?.Code);
    }

    [Fact]
    public async Task More_than_forty_distinct_links_returns_limit_error()
    {
        MetadataProvider root = new("root");
        for (int index = 0; index <= FileSystemTraversalPolicy.MaximumSymbolicLinkHops; index++)
        {
            root.AddLink(
                $"/link-{index}",
                index == FileSystemTraversalPolicy.MaximumSymbolicLinkHops
                    ? "/target"
                    : $"/link-{index + 1}");
        }

        root.AddFile("/target");
        FileSystemPathResolver resolver = CreateResolver(root);

        FileSystemResult<FileSystemPathResolution> result = await resolver.ResolveAsync(
            VirtualPath.Parse("/link-0"),
            FileSystemLinkBehavior.Follow,
            FileSystemOperation.Read,
            CreateContext());

        Assert.Equal(FileSystemErrorCode.SymbolicLinkLimitExceeded, result.Error?.Code);
    }

    [Fact]
    public async Task Relative_link_cannot_escape_virtual_root()
    {
        MetadataProvider root = new("root");
        root.AddDirectory("/home");
        root.AddLink("/home/link", "../../outside");
        FileSystemPathResolver resolver = CreateResolver(root);

        FileSystemResult<FileSystemPathResolution> result = await resolver.ResolveAsync(
            VirtualPath.Parse("/home/link"),
            FileSystemLinkBehavior.Follow,
            FileSystemOperation.Read,
            CreateContext());

        Assert.Equal(FileSystemErrorCode.RootContainmentViolation, result.Error?.Code);
    }

    [Fact]
    public async Task Intermediate_directory_requires_execute_permission()
    {
        MetadataProvider root = new("root");
        root.AddDirectory("/private", owner: "other", mode: 0x01C0);
        root.AddFile("/private/file.txt");
        FileSystemPathResolver resolver = CreateResolver(root);

        FileSystemResult<FileSystemPathResolution> result = await resolver.ResolveAsync(
            VirtualPath.Parse("/private/file.txt"),
            FileSystemLinkBehavior.Follow,
            FileSystemOperation.Read,
            CreateContext());

        Assert.Equal(FileSystemErrorCode.PermissionDenied, result.Error?.Code);
    }

    [Fact]
    public void Virtual_path_normalizes_unicode_and_enforces_encoded_limits()
    {
        VirtualPath normalized = VirtualPath.Parse("/home/re\u0301sume\u0301.txt");

        Assert.Equal("/home/résumé.txt", normalized.Value);
        Assert.Throws<FormatException>(() => VirtualPath.Parse($"/{new string('é', 128)}"));
        Assert.Throws<FormatException>(() => VirtualPath.Parse("~"));
    }

    private static FileSystemPathResolver CreateResolver(
        MetadataProvider root,
        MetadataProvider? mounted = null) =>
        new(new FileSystemMountRouter(
            root,
            mounted is null
                ? null
                : [new FileSystemMount(VirtualPath.Parse("/etc/hackeros"), mounted)]));

    private static FileSystemAuthorizationContext CreateContext()
    {
        AppOperationContext operationContext = new()
        {
            AppId = "org.hackeros.test",
            UserId = "user",
            UserAuthority = AppAuthority.User,
            GrantedCapabilities = new HashSet<string>(StringComparer.Ordinal)
        };
        return new FileSystemAuthorizationContext(
            operationContext,
            ["users"],
            new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero));
    }

    private sealed class MetadataProvider(string providerId) : IFileSystemProvider
    {
        private readonly Dictionary<string, FileSystemEntryMetadata> _entries =
            new(StringComparer.Ordinal);
        private int _nextId = 1;

        public string ProviderId { get; } = providerId;

        public List<string> StatPaths { get; } = [];

        public void AddDirectory(
            string path,
            string owner = "user",
            ushort mode = 0x01FF) =>
            Add(path, FileSystemEntryKind.Directory, owner: owner, mode: mode);

        public void AddFile(string path) => Add(path, FileSystemEntryKind.File);

        public void AddLink(string path, string target, string? id = null) =>
            Add(path, FileSystemEntryKind.SymbolicLink, target, id);

        public ValueTask<FileSystemResult<FileSystemEntrySnapshot>> StatAsync(
            FileSystemStatRequest request,
            FileSystemAuthorizationContext context,
            CancellationToken cancellationToken = default)
        {
            StatPaths.Add(request.Path.Value);
            return ValueTask.FromResult(_entries.TryGetValue(request.Path.Value, out FileSystemEntryMetadata? metadata)
                ? FileSystemResult<FileSystemEntrySnapshot>.Success(
                    new FileSystemEntrySnapshot(request.Path, metadata))
                : FileSystemResult<FileSystemEntrySnapshot>.Failure(
                    new FileSystemError(FileSystemOperation.Stat, FileSystemErrorCode.NotFound, request.Path)));
        }

        public ValueTask<FileSystemResult<FileSystemContentReadHandle>> ReadAsync(
            FileSystemReadRequest request,
            FileSystemAuthorizationContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<FileSystemResult<FileSystemDirectorySnapshot>> EnumerateAsync(
            FileSystemEnumerateRequest request,
            FileSystemAuthorizationContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<FileSystemMutationResult> CreateAsync(FileSystemCreateRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<FileSystemMutationResult> WriteAsync(FileSystemWriteRequest request, IFileSystemContentSource content, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<FileSystemMutationResult> MoveAsync(FileSystemMoveRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<FileSystemMutationResult> CopyAsync(FileSystemCopyRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<FileSystemMutationResult> DeleteAsync(FileSystemDeleteRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<FileSystemMutationResult> SetPermissionsAsync(FileSystemSetPermissionsRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private void Add(
            string path,
            FileSystemEntryKind kind,
            string? target = null,
            string? id = null,
            string owner = "user",
            ushort mode = 0x01FF)
        {
            DateTimeOffset timestamp = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
            FileSystemEntryId entryId = id is null
                ? FileSystemEntryId.FromGuid(new Guid(_nextId++, 0, 0, new byte[8]))
                : FileSystemEntryId.Parse(id);
            FileSystemTimestamps timestamps = new(timestamp, timestamp, timestamp);
            FileSystemPermissions permissions = FileSystemPermissions.FromMode(mode);
            _entries[VirtualPath.Parse(path).Value] = kind switch
            {
                FileSystemEntryKind.Directory => new DirectoryMetadata(entryId, owner, "users", permissions, timestamps, 1),
                FileSystemEntryKind.File => new FileMetadata(entryId, owner, "users", permissions, timestamps, 1, 0),
                FileSystemEntryKind.SymbolicLink => new SymbolicLinkMetadata(entryId, owner, "users", permissions, timestamps, 1, target!),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }
    }
}