using System.Text.Json;
using System.Text.Json.Serialization;
using HackerOs.Infrastructure.Browser.Interop;
using HackerOs.Infrastructure.Browser.Schema;
using HackerOs.Simulation.Abstractions.Sync;
using Microsoft.JSInterop;

namespace HackerOs.Infrastructure.Browser.Sync;

/// <summary>Persists per-(domain, recordId) sync tracking state (ADR 0029) in IndexedDB.</summary>
public sealed class IndexedDbSyncRecordStateRepository : ISyncRecordStateRepository, IAsyncDisposable
{
    private const string TransactionBoundary = "SyncRecordStateUpdate";
    private readonly IndexedDbInteropAdapter _adapter;

    /// <summary>Initializes a browser-backed sync record state repository.</summary>
    /// <param name="runtime">Browser JavaScript runtime used only by the internal storage adapter.</param>
    public IndexedDbSyncRecordStateRepository(IJSRuntime runtime)
    {
        _adapter = new IndexedDbInteropAdapter(runtime);
    }

    /// <inheritdoc />
    public async ValueTask<SyncRecordTrackingState?> GetAsync(
        string domain, Guid recordId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        await EnsureOpenAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<JsonElement> results = await _adapter.ExecuteAsync(
            TransactionBoundary,
            IndexedDbTransactionMode.ReadOnly,
            [new IndexedDbOperation("get", HackerOsIndexedDbSchema.SyncRecordStateStoreName, Key: FormatKey(domain, recordId))],
            cancellationToken).ConfigureAwait(false);

        JsonElement result = results.Single();
        return result.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? null
            : new SyncRecordTrackingState(
                result.GetProperty("revision").GetInt64(),
                result.GetProperty("contentHash").GetString()!);
    }

    /// <inheritdoc />
    public async ValueTask SetAsync(
        string domain, Guid recordId, long revision, string contentHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        await EnsureOpenAsync(cancellationToken).ConfigureAwait(false);
        await _adapter.ExecuteAsync(
            TransactionBoundary,
            IndexedDbTransactionMode.ReadWrite,
            [new IndexedDbOperation(
                "put",
                HackerOsIndexedDbSchema.SyncRecordStateStoreName,
                Value: new SyncRecordStateRecord(FormatKey(domain, recordId), domain, recordId.ToString("N"), revision, contentHash))],
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _adapter.DisposeAsync();

    private ValueTask EnsureOpenAsync(CancellationToken cancellationToken) =>
        _adapter.OpenAsync(IndexedDbMigrationPlan.CreateCurrent(), cancellationToken);

    private static string FormatKey(string domain, Guid recordId) => $"{domain}|{recordId:N}";

    private sealed record SyncRecordStateRecord(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("domain")] string Domain,
        [property: JsonPropertyName("recordId")] string RecordId,
        [property: JsonPropertyName("revision")] long Revision,
        [property: JsonPropertyName("contentHash")] string ContentHash);
}
