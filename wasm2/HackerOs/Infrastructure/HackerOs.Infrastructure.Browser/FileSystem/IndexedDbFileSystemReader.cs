using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.Infrastructure.Browser.Interop;
using HackerOs.Infrastructure.Browser.Schema;
using HackerOs.Simulation.Abstractions.FileSystem;
using Microsoft.JSInterop;

namespace HackerOs.Infrastructure.Browser.FileSystem;

/// <summary>Resolves filesystem paths and directory children from canonical IndexedDB records.</summary>
internal sealed class IndexedDbFileSystemReader : IAsyncDisposable
{
    private const string Boundary = "FileSystemMetadataMutation";
    private readonly IndexedDbInteropAdapter _adapter;

    /// <summary>Initializes a browser filesystem metadata reader.</summary>
    internal IndexedDbFileSystemReader(IJSRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _adapter = new IndexedDbInteropAdapter(runtime);
    }

    /// <summary>Resolves a canonical path to its persisted entry, or null when a link is absent.</summary>
    internal async ValueTask<IndexedDbFileSystemEntryRecord?> ResolveAsync(
        VirtualPath path,
        CancellationToken cancellationToken = default)
    {
        await _adapter.OpenAsync(IndexedDbMigrationPlan.CreateCurrent(), cancellationToken).ConfigureAwait(false);
        string currentId = IndexedDbFileSystemBootstrapper.RootEntryId.ToString();
        IndexedDbFileSystemEntryRecord? current = await GetEntryAsync(currentId, cancellationToken).ConfigureAwait(false);
        if (current is null || path.Value == "/")
        {
            return current;
        }

        foreach (string segment in path.Value.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            IReadOnlyList<JsonElement> linkResults = await _adapter.ExecuteAsync(
                Boundary,
                IndexedDbTransactionMode.ReadOnly,
                [new IndexedDbOperation(
                    "get",
                    HackerOsIndexedDbSchema.FileSystemLinkStoreName,
                    Key: new object[] { currentId, segment })],
                cancellationToken).ConfigureAwait(false);
            JsonElement link = linkResults.Single();
            if (IsMissing(link))
            {
                return null;
            }

            currentId = RequiredString(link, "entryId");
            current = await GetEntryAsync(currentId, cancellationToken).ConfigureAwait(false);
            if (current is null)
            {
                throw new InvalidDataException($"Filesystem link '{segment}' references missing entry '{currentId}'.");
            }
        }

        return current;
    }

    /// <summary>Returns immediate child records in ordinal name order, or null when the path is absent.</summary>
    internal async ValueTask<IReadOnlyList<(FileSystemEntryName Name, IndexedDbFileSystemEntryRecord Entry)>?> EnumerateAsync(
        VirtualPath path,
        CancellationToken cancellationToken = default)
    {
        IndexedDbFileSystemDirectoryRead? read =
            await ReadDirectoryAsync(path, cancellationToken).ConfigureAwait(false);
        return read?.Children;
    }

    /// <summary>Returns the observed directory and immediate child records in ordinal name order.</summary>
    internal async ValueTask<IndexedDbFileSystemDirectoryRead?> ReadDirectoryAsync(
        VirtualPath path,
        CancellationToken cancellationToken = default)
    {
        IndexedDbFileSystemEntryRecord? directory = await ResolveAsync(path, cancellationToken).ConfigureAwait(false);
        if (directory is null)
        {
            return null;
        }

        return await ReadChildrenAsync(directory, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns immediate children for an already resolved directory record.</summary>
    internal async ValueTask<IndexedDbFileSystemDirectoryRead> ReadChildrenAsync(
        IndexedDbFileSystemEntryRecord directory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directory);

        if (directory.Kind != (int)FileSystemEntryKind.Directory)
        {
            throw new InvalidDataException($"Filesystem entry '{directory.Id}' is not a directory.");
        }

        IReadOnlyList<JsonElement> linkResults = await _adapter.ExecuteAsync(
            Boundary,
            IndexedDbTransactionMode.ReadOnly,
            [new IndexedDbOperation(
                "getAll",
                HackerOsIndexedDbSchema.FileSystemLinkStoreName,
                IndexName: "parentId",
                Query: directory.Id)],
            cancellationToken).ConfigureAwait(false);
        IndexedDbFileSystemLinkRecord[] links =
        [..
            linkResults.Single()
                .EnumerateArray()
                .Select(ReadLink)
                .OrderBy(link => link.Name, StringComparer.Ordinal)
        ];
        if (links.Length == 0)
        {
            return new IndexedDbFileSystemDirectoryRead(directory, []);
        }

        IReadOnlyList<JsonElement> entryResults = await _adapter.ExecuteAsync(
            Boundary,
            IndexedDbTransactionMode.ReadOnly,
            [.. links.Select(link => new IndexedDbOperation(
                "get",
                HackerOsIndexedDbSchema.FileSystemEntryStoreName,
                Key: link.EntryId))],
            cancellationToken).ConfigureAwait(false);

        List<(FileSystemEntryName Name, IndexedDbFileSystemEntryRecord Entry)> children = [];
        for (int index = 0; index < links.Length; index++)
        {
            if (IsMissing(entryResults[index]))
            {
                throw new InvalidDataException(
                    $"Filesystem link '{links[index].Name}' references missing entry '{links[index].EntryId}'.");
            }

            children.Add((FileSystemEntryName.Parse(links[index].Name), IndexedDbFileSystemEntryRecord.FromJson(entryResults[index])));
        }

        return new IndexedDbFileSystemDirectoryRead(directory, children);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _adapter.DisposeAsync();

    private async ValueTask<IndexedDbFileSystemEntryRecord?> GetEntryAsync(
        string id,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<JsonElement> results = await _adapter.ExecuteAsync(
            Boundary,
            IndexedDbTransactionMode.ReadOnly,
            [new IndexedDbOperation("get", HackerOsIndexedDbSchema.FileSystemEntryStoreName, Key: id)],
            cancellationToken).ConfigureAwait(false);
        return IsMissing(results.Single()) ? null : IndexedDbFileSystemEntryRecord.FromJson(results.Single());
    }

    private static IndexedDbFileSystemLinkRecord ReadLink(JsonElement element) => new(
        RequiredString(element, "parentId"),
        RequiredString(element, "name"),
        RequiredString(element, "entryId"));

    private static bool IsMissing(JsonElement element) =>
        element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

    private static string RequiredString(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetString()
        ?? throw new InvalidDataException($"Persisted filesystem property '{propertyName}' is missing.");
}

/// <summary>Contains one directory record and the child records observed with it.</summary>
internal sealed record IndexedDbFileSystemDirectoryRead(
    IndexedDbFileSystemEntryRecord Directory,
    IReadOnlyList<(FileSystemEntryName Name, IndexedDbFileSystemEntryRecord Entry)> Children);
