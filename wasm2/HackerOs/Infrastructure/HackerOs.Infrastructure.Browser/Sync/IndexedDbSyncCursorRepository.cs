using System.Text.Json;
using System.Text.Json.Serialization;
using HackerOs.Infrastructure.Browser.Interop;
using HackerOs.Infrastructure.Browser.Schema;
using HackerOs.Simulation.Abstractions.Sync;
using Microsoft.JSInterop;

namespace HackerOs.Infrastructure.Browser.Sync;

/// <summary>Persists per-domain sync pull cursors (ADR 0029) in IndexedDB.</summary>
public sealed class IndexedDbSyncCursorRepository : ISyncCursorRepository, IAsyncDisposable
{
    private const string TransactionBoundary = "SyncCursorUpdate";
    private readonly IndexedDbInteropAdapter _adapter;

    /// <summary>Initializes a browser-backed sync cursor repository.</summary>
    /// <param name="runtime">Browser JavaScript runtime used only by the internal storage adapter.</param>
    public IndexedDbSyncCursorRepository(IJSRuntime runtime)
    {
        _adapter = new IndexedDbInteropAdapter(runtime);
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetCursorAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        await EnsureOpenAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<JsonElement> results = await _adapter.ExecuteAsync(
            TransactionBoundary,
            IndexedDbTransactionMode.ReadOnly,
            [new IndexedDbOperation("get", HackerOsIndexedDbSchema.SyncCursorStoreName, Key: domain)],
            cancellationToken).ConfigureAwait(false);

        JsonElement result = results.Single();
        return result.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? null
            : result.GetProperty("cursor").GetString();
    }

    /// <inheritdoc />
    public async ValueTask SetCursorAsync(string domain, string? cursor, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        await EnsureOpenAsync(cancellationToken).ConfigureAwait(false);
        await _adapter.ExecuteAsync(
            TransactionBoundary,
            IndexedDbTransactionMode.ReadWrite,
            [new IndexedDbOperation("put", HackerOsIndexedDbSchema.SyncCursorStoreName, Value: new SyncCursorRecord(domain, cursor))],
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _adapter.DisposeAsync();

    private ValueTask EnsureOpenAsync(CancellationToken cancellationToken) =>
        _adapter.OpenAsync(IndexedDbMigrationPlan.CreateCurrent(), cancellationToken);

    private sealed record SyncCursorRecord(
        [property: JsonPropertyName("domain")] string Domain,
        [property: JsonPropertyName("cursor")] string? Cursor);
}
