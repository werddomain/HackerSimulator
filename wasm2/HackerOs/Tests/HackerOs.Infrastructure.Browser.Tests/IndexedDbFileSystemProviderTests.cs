using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.Infrastructure.Browser.FileSystem;
using HackerOs.Simulation.Abstractions.FileSystem;
using Microsoft.JSInterop;

namespace HackerOs.Infrastructure.Browser.Tests;

/// <summary>Verifies the public IndexedDB filesystem provider contract.</summary>
public sealed class IndexedDbFileSystemProviderTests
{
    private const string RootId = "00000000000000000000000000000001";
    private const string ChildId = "11111111111111111111111111111111";
    private static readonly FileSystemEntryId CreatedId =
        FileSystemEntryId.Parse("22222222222222222222222222222222");
    private static readonly FileSystemEntryId SecondCreatedId =
        FileSystemEntryId.Parse("33333333333333333333333333333333");

    [Fact]
    public async Task StatAsync_ReturnsPersistedMetadataSnapshot()
    {
        ScriptedModule module = new(One(Entry(RootId, FileSystemEntryKind.Directory, revision: 9)));
        await using IndexedDbFileSystemProvider provider = new(new FakeJsRuntime(module));

        FileSystemResult<FileSystemEntrySnapshot> result = await provider.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/")),
            Context());

        Assert.True(result.Succeeded);
        Assert.Equal("/", result.Value!.Path.Value);
        Assert.Equal(9, result.Value.Metadata.Revision);
        Assert.Equal(FileSystemEntryKind.Directory, result.Value.Metadata.Kind);
    }

    [Fact]
    public async Task StatAsync_MissingPathReturnsNotFound()
    {
        ScriptedModule module = new(One(Entry(RootId, FileSystemEntryKind.Directory)), One("null"));
        await using IndexedDbFileSystemProvider provider = new(new FakeJsRuntime(module));

        FileSystemResult<FileSystemEntrySnapshot> result = await provider.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/missing")),
            Context());

        Assert.False(result.Succeeded);
        Assert.Equal(FileSystemErrorCode.NotFound, result.Error?.Code);
        Assert.Equal(FileSystemOperation.Stat, result.Error?.Operation);
    }

    [Fact]
    public async Task EnumerateAsync_ReturnsDirectoryRevisionAndChildren()
    {
        ScriptedModule module = new(
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 4)),
            One($"[{Link("alpha", ChildId)}]"),
            One(Entry(ChildId, FileSystemEntryKind.File, revision: 2, mediaType: "image/png")));
        await using IndexedDbFileSystemProvider provider = new(new FakeJsRuntime(module));

        FileSystemResult<FileSystemDirectorySnapshot> result = await provider.EnumerateAsync(
            new FileSystemEnumerateRequest(VirtualPath.Parse("/")),
            Context());

        Assert.True(result.Succeeded);
        Assert.Equal(4, result.Value!.Revision);
        FileSystemDirectoryItem child = Assert.Single(result.Value.Entries);
        Assert.Equal("alpha", child.Name.Value);
        Assert.Equal(ChildId, child.Metadata.Id.ToString());
        Assert.Equal("image/png", Assert.IsType<FileMetadata>(child.Metadata).MediaType);
    }

    [Fact]
    public async Task ReadAsync_ReconstructsPersistedTextChunks()
    {
        const string contentHash = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";
        ScriptedModule module = new(
            One(Entry(RootId, FileSystemEntryKind.Directory)),
            One(Link("file.txt", ChildId)),
            One(Entry(
                ChildId,
                FileSystemEntryKind.File,
                revision: 4,
                length: 5,
                contentHash: contentHash,
                contentKind: FileSystemContentKind.Text,
                mediaType: "text/plain",
                encodingName: "utf-8")),
            One($"[{{\"contentHash\":\"{contentHash}\",\"chunkIndex\":0,\"dataBase64\":\"aGVsbG8=\"}}]"));
        await using IndexedDbFileSystemProvider provider = new(new FakeJsRuntime(module));

        FileSystemResult<FileSystemContentReadHandle> result = await provider.ReadAsync(
            new FileSystemReadRequest(VirtualPath.Parse("/file.txt")),
            Context());

        Assert.True(result.Succeeded);
        await using FileSystemContentReadHandle handle = result.Value!;
        using StreamReader reader = new(handle.Content);
        Assert.Equal("hello", await reader.ReadToEndAsync());
        Assert.Equal(FileSystemContentKind.Text, handle.Descriptor.Kind);
        Assert.Equal(4, handle.Entry.Metadata.Revision);
    }

    [Fact]
    public async Task WriteAsync_PersistsChunksThenPublishesRevisionedMetadata()
    {
        const string expectedHash = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";
        ScriptedModule module = new(
            One(Entry(RootId, FileSystemEntryKind.Directory)),
            One(Link("file.txt", ChildId)),
            One(Entry(ChildId, FileSystemEntryKind.File, revision: 4)),
            One("{\"added\":true}"),
            [Parse("4"), Parse("null")]);
        await using IndexedDbFileSystemProvider provider = new(
            new FakeJsRuntime(module),
            new FixedTimeProvider(),
            null,
            () => Guid.Parse("88888888-8888-8888-8888-888888888888"));

        FileSystemMutationResult result = await provider.WriteAsync(
            new FileSystemWriteRequest(VirtualPath.Parse("/file.txt"), 4),
            new TextSource("hello"),
            Context());

        Assert.True(result.Succeeded);
        Assert.Equal(5, result.Entry!.Metadata.Revision);
        Assert.Equal(5, Assert.IsType<FileMetadata>(result.Entry.Metadata).Length);
        Assert.Equal(5, module.ExecuteArguments.Count);
        IReadOnlyList<HackerOs.Infrastructure.Browser.Interop.IndexedDbOperation> chunkOperations =
            Assert.IsAssignableFrom<IReadOnlyList<HackerOs.Infrastructure.Browser.Interop.IndexedDbOperation>>(
                module.ExecuteArguments[3][4]);
        Assert.Equal(expectedHash, Assert.IsType<object[]>(Assert.Single(chunkOperations).Key)[0]);
        IReadOnlyList<HackerOs.Infrastructure.Browser.Interop.IndexedDbOperation> metadataOperations =
            Assert.IsAssignableFrom<IReadOnlyList<HackerOs.Infrastructure.Browser.Interop.IndexedDbOperation>>(
                module.ExecuteArguments[4][4]);
        IndexedDbFileSystemEntryRecord updated =
            Assert.IsType<IndexedDbFileSystemEntryRecord>(metadataOperations[1].Value);
        Assert.Equal(expectedHash, updated.ContentHash);
        Assert.Equal((int)FileSystemContentKind.Text, updated.ContentKind);
    }

    [Fact]
    public async Task CreateAsync_CommitsAtomicBatchAndInheritsParentGroup()
    {
        ScriptedModule module = new(
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 3, groupId: "operators")),
            One("null"),
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 3, groupId: "operators")),
            [Parse("null"), Parse("null"), Parse("null"), Parse("null")]);
        await using IndexedDbFileSystemProvider provider = new(
            new FakeJsRuntime(module),
            new FixedTimeProvider(),
            () => CreatedId,
            () => Guid.Parse("33333333-3333-3333-3333-333333333333"));

        FileSystemMutationResult result = await provider.CreateAsync(
            new FileSystemCreateRequest(
                VirtualPath.Parse("/notes"),
                FileSystemEntryKind.Directory,
                FileSystemPermissions.FromMode(493),
                3),
            Context());

        Assert.True(result.Succeeded);
        Assert.Equal(CreatedId, result.Entry!.Metadata.Id);
        Assert.Equal("operators", result.Entry.Metadata.GroupId);
        Assert.Equal(4, Assert.IsAssignableFrom<System.Collections.ICollection>(module.ExecuteArguments[^1][4]).Count);
    }

    [Fact]
    public async Task CreateAsync_StaleParentRevisionRejectsBeforeWrite()
    {
        ScriptedModule module = new(
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 3)),
            One("null"),
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 3)));
        await using IndexedDbFileSystemProvider provider = new(new FakeJsRuntime(module));

        FileSystemMutationResult result = await provider.CreateAsync(
            new FileSystemCreateRequest(
                VirtualPath.Parse("/notes"),
                FileSystemEntryKind.Directory,
                FileSystemPermissions.FromMode(493),
                2),
            Context());

        Assert.False(result.Succeeded);
        Assert.Equal(FileSystemErrorCode.RevisionConflict, result.Transaction.Error?.Code);
        Assert.Equal(3, module.ExecuteArguments.Count);
    }

    [Fact]
    public async Task SetPermissionsAsync_CommitsRevisionedMetadataUpdate()
    {
        ScriptedModule module = new(
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 5)),
            [Parse("null"), Parse("null")]);
        await using IndexedDbFileSystemProvider provider = new(
            new FakeJsRuntime(module),
            new FixedTimeProvider(),
            null,
            () => Guid.Parse("44444444-4444-4444-4444-444444444444"));

        FileSystemMutationResult result = await provider.SetPermissionsAsync(
            new FileSystemSetPermissionsRequest(
                VirtualPath.Parse("/"),
                FileSystemPermissions.FromMode(0x01C0),
                5),
            Context());

        Assert.True(result.Succeeded);
        Assert.Equal(6, result.Entry!.Metadata.Revision);
        Assert.Equal(0x01C0, result.Entry.Metadata.Permissions.Mode);
        IReadOnlyList<HackerOs.Infrastructure.Browser.Interop.IndexedDbOperation> operations =
            Assert.IsAssignableFrom<IReadOnlyList<HackerOs.Infrastructure.Browser.Interop.IndexedDbOperation>>(
                module.ExecuteArguments[^1][4]);
        Assert.Equal(["assertPropertyEquals", "put"], operations.Select(operation => operation.Kind));
    }

    [Fact]
    public async Task SetPermissionsAsync_StaleRevisionRejectsBeforeWrite()
    {
        ScriptedModule module = new(One(Entry(RootId, FileSystemEntryKind.Directory, revision: 5)));
        await using IndexedDbFileSystemProvider provider = new(new FakeJsRuntime(module));

        FileSystemMutationResult result = await provider.SetPermissionsAsync(
            new FileSystemSetPermissionsRequest(
                VirtualPath.Parse("/"),
                FileSystemPermissions.FromMode(0x01C0),
                4),
            Context());

        Assert.False(result.Succeeded);
        Assert.Equal(FileSystemErrorCode.RevisionConflict, result.Transaction.Error?.Code);
        Assert.Single(module.ExecuteArguments);
    }

    [Fact]
    public async Task MoveAsync_RenamesLinkAndReturnsDestinationSnapshot()
    {
        ScriptedModule module = new(
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 3)),
            One(Link("old.txt", ChildId)),
            One(Entry(ChildId, FileSystemEntryKind.File, revision: 2)),
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 3)),
            One("null"),
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 3)),
            [Parse("null"), Parse("null"), Parse("null"), Parse("null"), Parse("null"), Parse("null")]);
        await using IndexedDbFileSystemProvider provider = new(
            new FakeJsRuntime(module),
            new FixedTimeProvider(),
            null,
            () => Guid.Parse("55555555-5555-5555-5555-555555555555"));

        FileSystemMutationResult result = await provider.MoveAsync(
            new FileSystemMoveRequest(
                VirtualPath.Parse("/old.txt"),
                VirtualPath.Parse("/new.txt"),
                2,
                3,
                3),
            Context());

        Assert.True(result.Succeeded);
        Assert.Equal("/new.txt", result.Entry!.Path.Value);
        Assert.Equal(3, result.Entry.Metadata.Revision);
        IReadOnlyList<HackerOs.Infrastructure.Browser.Interop.IndexedDbOperation> operations =
            Assert.IsAssignableFrom<IReadOnlyList<HackerOs.Infrastructure.Browser.Interop.IndexedDbOperation>>(
                module.ExecuteArguments[^1][4]);
        Assert.Equal(
            ["assertPropertyEquals", "assertPropertyEquals", "put", "put", "delete", "add"],
            operations.Select(operation => operation.Kind));
    }

    [Fact]
    public async Task DeleteAsync_NonEmptyDirectoryWithoutRecursiveIsRejected()
    {
        ScriptedModule module = new(
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 3)),
            One(Link("folder", ChildId)),
            One(Entry(ChildId, FileSystemEntryKind.Directory, revision: 2)),
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 3)),
            One($"[{Link("child.txt", CreatedId.ToString(), ChildId)}]"),
            One(Entry(CreatedId.ToString(), FileSystemEntryKind.File)));
        await using IndexedDbFileSystemProvider provider = new(new FakeJsRuntime(module));

        FileSystemMutationResult result = await provider.DeleteAsync(
            new FileSystemDeleteRequest(VirtualPath.Parse("/folder"), 2, 3),
            Context());

        Assert.False(result.Succeeded);
        Assert.Equal(FileSystemErrorCode.DirectoryNotEmpty, result.Transaction.Error?.Code);
        Assert.Equal(6, module.ExecuteArguments.Count);
    }

    [Fact]
    public async Task DeleteAsync_RecursiveDeletesCompleteObservedSubtree()
    {
        ScriptedModule module = new(
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 3)),
            One(Link("folder", ChildId)),
            One(Entry(ChildId, FileSystemEntryKind.Directory, revision: 2)),
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 3)),
            One($"[{Link("child.txt", CreatedId.ToString(), ChildId)}]"),
            One(Entry(CreatedId.ToString(), FileSystemEntryKind.File)),
            [Parse("null"), Parse("null"), Parse("null"), Parse("null"), Parse("null"), Parse("null"), Parse("null"), Parse("null")]);
        await using IndexedDbFileSystemProvider provider = new(
            new FakeJsRuntime(module),
            new FixedTimeProvider(),
            null,
            () => Guid.Parse("66666666-6666-6666-6666-666666666666"));

        FileSystemMutationResult result = await provider.DeleteAsync(
            new FileSystemDeleteRequest(VirtualPath.Parse("/folder"), 2, 3, recursive: true),
            Context());

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Transaction.AffectedEntryIds.Count);
        IReadOnlyList<HackerOs.Infrastructure.Browser.Interop.IndexedDbOperation> operations =
            Assert.IsAssignableFrom<IReadOnlyList<HackerOs.Infrastructure.Browser.Interop.IndexedDbOperation>>(
                module.ExecuteArguments[^1][4]);
        Assert.Equal(4, operations.Count(operation => operation.Kind == "delete"));
    }

    [Fact]
    public async Task CopyAsync_RecursiveCopyUsesNewIdsAndOneAtomicBatch()
    {
        Queue<FileSystemEntryId> ids = new([CreatedId, SecondCreatedId]);
        ScriptedModule module = new(
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 3)),
            One(Link("folder", ChildId)),
            One(Entry(ChildId, FileSystemEntryKind.Directory, revision: 2)),
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 3)),
            One("null"),
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 3)),
            One($"[{Link("child.txt", SecondCreatedId.ToString(), ChildId)}]"),
            One(Entry(SecondCreatedId.ToString(), FileSystemEntryKind.File, revision: 5)),
            [Parse("null"), Parse("null"), Parse("null"), Parse("null"), Parse("null"), Parse("null"), Parse("null"), Parse("null")]);
        await using IndexedDbFileSystemProvider provider = new(
            new FakeJsRuntime(module),
            new FixedTimeProvider(),
            () => ids.Dequeue(),
            () => Guid.Parse("77777777-7777-7777-7777-777777777777"));

        FileSystemMutationResult result = await provider.CopyAsync(
            new FileSystemCopyRequest(
                VirtualPath.Parse("/folder"),
                VirtualPath.Parse("/folder-copy"),
                2,
                3),
            Context());

        Assert.True(result.Succeeded);
        Assert.Equal(CreatedId, result.Entry!.Metadata.Id);
        Assert.Equal(1, result.Entry.Metadata.Revision);
        Assert.Equal(2, result.Transaction.AffectedEntryIds.Count);
        IReadOnlyList<HackerOs.Infrastructure.Browser.Interop.IndexedDbOperation> operations =
            Assert.IsAssignableFrom<IReadOnlyList<HackerOs.Infrastructure.Browser.Interop.IndexedDbOperation>>(
                module.ExecuteArguments[^1][4]);
        Assert.Equal(3, operations.Count(operation => operation.Kind == "assertPropertyEquals"));
        Assert.Equal(2, operations.Count(operation => operation.Kind == "add"
            && operation.ObjectStoreName == "fsEntries"));
        Assert.Equal(2, operations.Count(operation => operation.Kind == "add"
            && operation.ObjectStoreName == "fsLinks"));
    }

    private static FileSystemAuthorizationContext Context()
    {
        AppOperationContext operation = new()
        {
            AppId = "org.hackeros.test",
            UserId = "user",
            UserAuthority = AppAuthority.Administrator,
            GrantedCapabilities = new HashSet<string>(AppCapabilities.All, StringComparer.Ordinal),
            IsSystemOperation = true
        };
        return new FileSystemAuthorizationContext(
            operation,
            ["users"],
            new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
    }

    private static JsonElement[] One(string json) => [JsonDocument.Parse(json).RootElement.Clone()];

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static string Link(string name, string entryId, string parentId = RootId) =>
        $"{{\"parentId\":\"{parentId}\",\"name\":\"{name}\",\"entryId\":\"{entryId}\"}}";

    private static string Entry(
        string id,
        FileSystemEntryKind kind,
        long revision = 1,
        string groupId = "system",
        long length = 0,
        string? contentHash = null,
        FileSystemContentKind contentKind = FileSystemContentKind.Binary,
        string mediaType = "application/octet-stream",
        string? encodingName = null) => $$"""
        {
          "id":"{{id}}",
          "kind":{{(int)kind}},
          "ownerId":"system",
          "groupId":"{{groupId}}",
          "permissionsMode":493,
          "createdUtcMs":1785664800000,
          "contentModifiedUtcMs":1785664800000,
          "metadataChangedUtcMs":1785664800000,
          "revision":{{revision}},
          "length":{{length}},
          "symbolicLinkTarget":null,
          "contentHash":{{(contentHash is null ? "null" : $"\"{contentHash}\"")}},
          "contentKind":{{(int)contentKind}},
          "mediaType":"{{mediaType}}",
          "encodingName":{{(encodingName is null ? "null" : $"\"{encodingName}\"")}}
        }
        """;

    private sealed class FakeJsRuntime(IJSObjectReference module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) => ValueTask.FromResult((TValue)module);
    }

    private sealed class ScriptedModule(params JsonElement[][] results) : IJSObjectReference
    {
        private readonly Queue<JsonElement[]> _results = new(results);

        public List<object?[]> ExecuteArguments { get; } = [];

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            if (identifier == "executeTransaction")
            {
                ExecuteArguments.Add(args ?? []);
                return ValueTask.FromResult((TValue)(object)_results.Dequeue());
            }

            return ValueTask.FromResult(default(TValue)!);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class TextSource(string value) : IFileSystemContentSource
    {
        public FileSystemContentDescriptor Descriptor { get; } = FileSystemContentDescriptor.Text();

        public long? Length => System.Text.Encoding.UTF8.GetByteCount(value);

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Stream>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(value), writable: false));
    }
}
