using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.Infrastructure.Browser.FileSystem;
using HackerOs.Infrastructure.Browser.Interop;
using HackerOs.Simulation.Abstractions.FileSystem;
using Microsoft.JSInterop;

namespace HackerOs.Infrastructure.Browser.Tests;

/// <summary>Verifies path resolution and deterministic directory reads at the browser boundary.</summary>
public sealed class IndexedDbFileSystemReaderTests
{
    private const string RootId = "00000000000000000000000000000001";
    private const string HomeId = "11111111111111111111111111111111";
    private const string UserId = "22222222222222222222222222222222";

    [Fact]
    public async Task ResolveAsync_RootReadsStableRootEntryOnly()
    {
        ScriptedJsObjectReference module = new(One(Entry(RootId, FileSystemEntryKind.Directory)));
        await using IndexedDbFileSystemReader reader = new(new FakeJsRuntime(module));

        IndexedDbFileSystemEntryRecord? result = await reader.ResolveAsync(VirtualPath.Parse("/"));

        Assert.Equal(RootId, result?.Id);
        Assert.Single(module.ExecuteInvocations);
        Assert.Equal(RootId, Operation(module.ExecuteInvocations[0]).Key);
    }

    [Fact]
    public async Task ResolveAsync_FollowsCompoundDirectoryLinks()
    {
        ScriptedJsObjectReference module = new(
            One(Entry(RootId, FileSystemEntryKind.Directory)),
            One(Link(RootId, "home", HomeId)),
            One(Entry(HomeId, FileSystemEntryKind.Directory)),
            One(Link(HomeId, "user", UserId)),
            One(Entry(UserId, FileSystemEntryKind.Directory)));
        await using IndexedDbFileSystemReader reader = new(new FakeJsRuntime(module));

        IndexedDbFileSystemEntryRecord? result = await reader.ResolveAsync(VirtualPath.Parse("/home/user"));

        Assert.Equal(UserId, result?.Id);
        IndexedDbOperation homeLookup = Operation(module.ExecuteInvocations[1]);
        Assert.Equal(new object[] { RootId, "home" }, Assert.IsType<object[]>(homeLookup.Key));
        IndexedDbOperation userLookup = Operation(module.ExecuteInvocations[3]);
        Assert.Equal(new object[] { HomeId, "user" }, Assert.IsType<object[]>(userLookup.Key));
    }

    [Fact]
    public async Task ResolveAsync_MissingLinkStopsWithoutEntryRead()
    {
        ScriptedJsObjectReference module = new(
            One(Entry(RootId, FileSystemEntryKind.Directory)),
            One("null"));
        await using IndexedDbFileSystemReader reader = new(new FakeJsRuntime(module));

        IndexedDbFileSystemEntryRecord? result = await reader.ResolveAsync(VirtualPath.Parse("/missing/child"));

        Assert.Null(result);
        Assert.Equal(2, module.ExecuteInvocations.Count);
    }

    [Fact]
    public async Task EnumerateAsync_SortsLinksAndBatchesEntryReads()
    {
        ScriptedJsObjectReference module = new(
            One(Entry(RootId, FileSystemEntryKind.Directory, revision: 7)),
            One($"[{LinkJson(RootId, "zeta", UserId)},{LinkJson(RootId, "Alpha", HomeId)}]"),
            [Parse(Entry(HomeId, FileSystemEntryKind.File)), Parse(Entry(UserId, FileSystemEntryKind.Directory))]);
        await using IndexedDbFileSystemReader reader = new(new FakeJsRuntime(module));

        IReadOnlyList<(FileSystemEntryName Name, IndexedDbFileSystemEntryRecord Entry)>? children =
            await reader.EnumerateAsync(VirtualPath.Parse("/"));

        IReadOnlyList<(FileSystemEntryName Name, IndexedDbFileSystemEntryRecord Entry)> existing =
            Assert.IsAssignableFrom<IReadOnlyList<(FileSystemEntryName, IndexedDbFileSystemEntryRecord)>>(children);
        Assert.Equal(["Alpha", "zeta"], existing.Select(child => child.Name.Value));
        Assert.Equal([HomeId, UserId], existing.Select(child => child.Entry.Id));
        JsInvocation linkQuery = module.ExecuteInvocations[1];
        IndexedDbOperation query = Operation(linkQuery);
        Assert.Equal("parentId", query.IndexName);
        Assert.Equal(RootId, query.Query);
        Assert.Equal(2, Operations(module.ExecuteInvocations[2]).Count);
    }

    [Fact]
    public async Task EnumerateAsync_LinkToMissingEntryReportsCorruption()
    {
        ScriptedJsObjectReference module = new(
            One(Entry(RootId, FileSystemEntryKind.Directory)),
            One($"[{LinkJson(RootId, "lost", HomeId)}]"),
            One("null"));
        await using IndexedDbFileSystemReader reader = new(new FakeJsRuntime(module));

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await reader.EnumerateAsync(VirtualPath.Parse("/")));

        Assert.Contains(HomeId, error.Message, StringComparison.Ordinal);
    }

    private static IndexedDbOperation Operation(JsInvocation invocation) => Assert.Single(Operations(invocation));

    private static IReadOnlyList<IndexedDbOperation> Operations(JsInvocation invocation) =>
        Assert.IsAssignableFrom<IReadOnlyList<IndexedDbOperation>>(invocation.Arguments[4]);

    private static JsonElement[] One(string json) => [Parse(json)];

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static string Link(string parentId, string name, string entryId) => LinkJson(parentId, name, entryId);

    private static string LinkJson(string parentId, string name, string entryId) =>
        $"{{\"parentId\":\"{parentId}\",\"name\":\"{name}\",\"entryId\":\"{entryId}\"}}";

    private static string Entry(string id, FileSystemEntryKind kind, long revision = 1) => $$"""
        {
          "id":"{{id}}",
          "kind":{{(int)kind}},
          "ownerId":"system",
          "groupId":"system",
          "permissionsMode":493,
          "createdUtcMs":1785664800000,
          "contentModifiedUtcMs":1785664800000,
          "metadataChangedUtcMs":1785664800000,
          "revision":{{revision}},
          "length":0,
          "symbolicLinkTarget":null,
          "contentHash":null,
          "contentKind":1,
          "mediaType":"application/octet-stream",
          "encodingName":null
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

    private sealed class ScriptedJsObjectReference(params JsonElement[][] transactionResults) : IJSObjectReference
    {
        private readonly Queue<JsonElement[]> _results = new(transactionResults);

        public List<JsInvocation> ExecuteInvocations { get; } = [];

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
                ExecuteInvocations.Add(new JsInvocation(identifier, args ?? []));
                return ValueTask.FromResult((TValue)(object)_results.Dequeue());
            }

            return ValueTask.FromResult(default(TValue)!);
        }
    }

    private sealed record JsInvocation(string Identifier, object?[] Arguments);
}
