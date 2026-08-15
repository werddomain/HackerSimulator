using System.Text.Json;
using HackerOs.Infrastructure.Browser.Schema;
using HackerOs.Infrastructure.Browser.Storage;
using Microsoft.JSInterop;

namespace HackerOs.Infrastructure.Browser.Interop;

/// <summary>
/// Owns the browser-only JavaScript module and exposes transaction-oriented IndexedDB primitives
/// to repositories in this assembly.
/// </summary>
internal sealed class IndexedDbInteropAdapter : IAsyncDisposable
{
    internal const string ModulePath = "./_content/HackerOs.Infrastructure.Browser/indexedDb.js";

    private readonly IJSRuntime _runtime;
    private readonly SemaphoreSlim _moduleGate = new(1, 1);
    private IJSObjectReference? _module;
    private bool _disposed;

    /// <summary>Initializes the adapter without importing JavaScript until the first operation.</summary>
    public IndexedDbInteropAdapter(IJSRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    /// <summary>Opens the canonical database and applies a C#-owned declarative migration plan.</summary>
    public async ValueTask OpenAsync(
        IndexedDbMigrationPlan migrationPlan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migrationPlan);
        IJSObjectReference module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        await module.InvokeVoidAsync(
            "openDatabase",
            cancellationToken,
            HackerOsIndexedDbSchema.DatabaseName,
            HackerOsIndexedDbSchema.CurrentVersion,
            migrationPlan).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an ordered operation batch inside one named schema transaction boundary.
    /// </summary>
    public async ValueTask<IReadOnlyList<JsonElement>> ExecuteAsync(
        string boundaryName,
        IndexedDbTransactionMode mode,
        IReadOnlyList<IndexedDbOperation> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryName);
        ArgumentNullException.ThrowIfNull(operations);

        IndexedDbTransactionBoundary boundary = HackerOsIndexedDbSchema.TransactionBoundaries
            .SingleOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Name, boundaryName))
            ?? throw new ArgumentException(
                $"Unknown IndexedDB transaction boundary '{boundaryName}'.", nameof(boundaryName));

        foreach (IndexedDbOperation operation in operations)
        {
            if (!boundary.ObjectStoreNames.Contains(operation.ObjectStoreName, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    $"Operation store '{operation.ObjectStoreName}' is outside boundary '{boundaryName}'.",
                    nameof(operations));
            }

            if (operation.ReferenceObjectStoreName is not null
                && !boundary.ObjectStoreNames.Contains(
                    operation.ReferenceObjectStoreName,
                    StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    $"Reference store '{operation.ReferenceObjectStoreName}' is outside boundary '{boundaryName}'.",
                    nameof(operations));
            }
        }

        IJSObjectReference module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        JsonElement[] results;
        try
        {
            results = await module.InvokeAsync<JsonElement[]>(
                "executeTransaction",
                cancellationToken,
                HackerOsIndexedDbSchema.DatabaseName,
                HackerOsIndexedDbSchema.CurrentVersion,
                boundary.ObjectStoreNames,
                mode == IndexedDbTransactionMode.ReadOnly ? "readonly" : "readwrite",
                operations).ConfigureAwait(false);
        }
        catch (JSException exception) when (exception.Message.Contains("QuotaExceededError", StringComparison.Ordinal))
        {
            throw new BrowserStorageQuotaException(
                "Browser storage quota was exhausted; the IndexedDB transaction was not committed.",
                exception);
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync().ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // Browser shutdown can disconnect JS before the DI scope is disposed.
            }
        }

        _moduleGate.Dispose();
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_module is not null)
        {
            return _module;
        }

        await _moduleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _module ??= await _runtime.InvokeAsync<IJSObjectReference>(
                "import", cancellationToken, ModulePath).ConfigureAwait(false);
            return _module;
        }
        finally
        {
            _moduleGate.Release();
        }
    }
}

/// <summary>Specifies the IndexedDB transaction mode passed to the browser adapter.</summary>
internal enum IndexedDbTransactionMode
{
    /// <summary>Allows reads only.</summary>
    ReadOnly,

    /// <summary>Allows reads and mutations.</summary>
    ReadWrite
}

/// <summary>Describes one operation in an ordered IndexedDB transaction batch.</summary>
internal sealed record IndexedDbOperation(
    string Kind,
    string ObjectStoreName,
    object? Key = null,
    object? Value = null,
    string? IndexName = null,
    object? Query = null,
    int? Count = null,
    string? CompareProperty = null,
    object? ExpectedValue = null,
    string? FailureCode = null,
    string? ReferenceObjectStoreName = null,
    string? ReferenceIndexName = null);

/// <summary>Describes the ordered schema migration chain up to the current database version.</summary>
internal sealed record IndexedDbMigrationPlan
{
    /// <summary>Initializes and validates a contiguous migration chain beginning at version one.</summary>
    public IndexedDbMigrationPlan(IReadOnlyList<IndexedDbMigrationStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0 || steps[^1].TargetVersion != HackerOsIndexedDbSchema.CurrentVersion)
        {
            throw new ArgumentException(
                "The migration chain must end at the current schema version.", nameof(steps));
        }

        for (int index = 0; index < steps.Count; index++)
        {
            if (steps[index].TargetVersion != index + 1)
            {
                throw new ArgumentException(
                    "Migration target versions must be contiguous and begin at version one.", nameof(steps));
            }
        }

        Steps = steps;
    }

    /// <summary>Gets the migration steps ordered by their target schema version.</summary>
    public IReadOnlyList<IndexedDbMigrationStep> Steps { get; }

    /// <summary>Creates the complete migration chain through the current schema version.</summary>
    public static IndexedDbMigrationPlan CreateCurrent() => new(
    [
        new(
            1,
            HackerOsIndexedDbSchema.ObjectStores,
            [],
            [],
            []),
        new(
            2,
            [],
            [],
            [new IndexedDbIndexChange(
                HackerOsIndexedDbSchema.FileSystemEntryStoreName,
                new IndexedDbIndexDefinition("contentHash", ["contentHash"]))],
            []),
        new(
            3,
            [HackerOsIndexedDbSchema.ObjectStores.Single(
                store => store.Name == HackerOsIndexedDbSchema.ServerConnectionStoreName)],
            [],
            [],
            [])
    ]);
}

/// <summary>Describes schema changes applied while upgrading to one exact version.</summary>
internal sealed record IndexedDbMigrationStep(
    int TargetVersion,
    IReadOnlyList<IndexedDbObjectStoreDefinition> CreateObjectStores,
    IReadOnlyList<string> DeleteObjectStores,
    IReadOnlyList<IndexedDbIndexChange> CreateIndexes,
    IReadOnlyList<IndexedDbIndexRemoval> DeleteIndexes);

/// <summary>Describes one index addition to an existing object store.</summary>
internal sealed record IndexedDbIndexChange(
    string ObjectStoreName,
    IndexedDbIndexDefinition Index);

/// <summary>Describes one index removal from an existing object store.</summary>
internal sealed record IndexedDbIndexRemoval(
    string ObjectStoreName,
    string IndexName);
