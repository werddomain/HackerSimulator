using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.Infrastructure.Browser.Interop;
using HackerOs.Infrastructure.Browser.Schema;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Settings;
using Microsoft.JSInterop;

namespace HackerOs.Infrastructure.Browser.Settings;

/// <summary>Persists canonical settings documents in IndexedDB with optimistic revision checks.</summary>
public sealed class IndexedDbSettingsDocumentService : ISettingsDocumentService, IAsyncDisposable
{
    private const string TransactionBoundary = "SettingsDocumentWrite";
    private readonly IndexedDbInteropAdapter _adapter;
    private readonly IReadOnlyDictionary<string, DefinitionState> _definitions;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private bool _initialized;

    /// <summary>Initializes the service from canonical validated settings definitions.</summary>
    public IndexedDbSettingsDocumentService(
        IJSRuntime runtime,
        IEnumerable<SettingsDocumentDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(definitions);
        _adapter = new IndexedDbInteropAdapter(runtime);

        Dictionary<string, DefinitionState> byPath = new(StringComparer.Ordinal);
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (SettingsDocumentDefinition definition in definitions)
        {
            IReadOnlyList<string> errors = definition.Validator.Validate(definition.InitialContent);
            if (errors.Count > 0)
            {
                throw new ArgumentException(
                    $"Initial settings document '{definition.Path}' is invalid: {string.Join("; ", errors)}",
                    nameof(definitions));
            }

            string id = FormatKey(definition.Key);
            if (!ids.Add(id) || !byPath.TryAdd(definition.Path.Value, new DefinitionState(id, definition)))
            {
                throw new ArgumentException("Settings document paths and keys must be unique.", nameof(definitions));
            }
        }

        _definitions = byPath;
    }

    /// <inheritdoc />
    public async ValueTask<SettingsReadResult> ReadAsync(
        VirtualPath path,
        AppOperationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_definitions.TryGetValue(path.Value, out DefinitionState? state))
        {
            return new SettingsReadResult(SettingsReadStatus.NotFound, ErrorCode: "settings.not-found");
        }

        if (!IsAuthorized(context, state.Definition.ReadCapability, state.Definition.MinimumReadAuthority))
        {
            return new SettingsReadResult(SettingsReadStatus.Denied, ErrorCode: "settings.read-denied");
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        SettingsRecord record = await ReadRecordAsync(state, cancellationToken).ConfigureAwait(false);
        return new SettingsReadResult(SettingsReadStatus.Success, ToSnapshot(state.Definition, record));
    }

    /// <inheritdoc />
    public async ValueTask<SettingsWriteResult> WriteAsync(
        SettingsWriteRequest request,
        AppOperationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (!_definitions.TryGetValue(request.Path.Value, out DefinitionState? state))
        {
            return new SettingsWriteResult(SettingsWriteStatus.NotFound, Errors: ["settings.not-found"]);
        }

        if (!IsAuthorized(context, state.Definition.WriteCapability, state.Definition.MinimumWriteAuthority))
        {
            return new SettingsWriteResult(SettingsWriteStatus.Denied, Errors: ["settings.write-denied"]);
        }

        IReadOnlyList<string> errors = state.Definition.Validator.Validate(request.Content);
        if (errors.Count > 0)
        {
            return new SettingsWriteResult(SettingsWriteStatus.Invalid, Errors: errors);
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        SettingsRecord replacement = SettingsRecord.Create(state, request.Content, request.ExpectedRevision + 1);
        IReadOnlyList<JsonElement> results = await _adapter.ExecuteAsync(
            TransactionBoundary,
            IndexedDbTransactionMode.ReadWrite,
            [new IndexedDbOperation(
                "compareAndPut",
                HackerOsIndexedDbSchema.SettingsStoreName,
                Key: state.Id,
                Value: replacement,
                CompareProperty: "revision",
                ExpectedValue: request.ExpectedRevision)],
            cancellationToken).ConfigureAwait(false);

        if (!results.Single().GetProperty("committed").GetBoolean())
        {
            return new SettingsWriteResult(
                SettingsWriteStatus.Conflict,
                Errors: ["settings.revision-conflict"]);
        }

        return new SettingsWriteResult(
            SettingsWriteStatus.Success,
            ToSnapshot(state.Definition, replacement));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _initializationGate.Dispose();
        await _adapter.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Opens storage and creates every missing canonical settings document.</summary>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) =>
        EnsureInitializedAsync(cancellationToken);

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            await _adapter.OpenAsync(IndexedDbMigrationPlan.CreateCurrent(), cancellationToken).ConfigureAwait(false);
            IndexedDbOperation[] seedOperations =
            [
                .. _definitions.Values.Select(state => new IndexedDbOperation(
                    "addIfAbsent",
                    HackerOsIndexedDbSchema.SettingsStoreName,
                    Key: state.Id,
                    Value: SettingsRecord.Create(state, state.Definition.InitialContent, 1)))
            ];
            if (seedOperations.Length > 0)
            {
                await _adapter.ExecuteAsync(
                    TransactionBoundary,
                    IndexedDbTransactionMode.ReadWrite,
                    seedOperations,
                    cancellationToken).ConfigureAwait(false);
            }

            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private async ValueTask<SettingsRecord> ReadRecordAsync(
        DefinitionState state,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<JsonElement> results = await _adapter.ExecuteAsync(
            TransactionBoundary,
            IndexedDbTransactionMode.ReadOnly,
            [new IndexedDbOperation("get", HackerOsIndexedDbSchema.SettingsStoreName, Key: state.Id)],
            cancellationToken).ConfigureAwait(false);
        JsonElement record = results.Single();
        return new SettingsRecord(
            record.GetProperty("id").GetString()
                ?? throw new InvalidDataException($"Settings document '{state.Id}' has no id."),
            record.GetProperty("scope").GetInt32(),
            GetOptionalString(record, "appId"),
            GetOptionalString(record, "userId"),
            GetOptionalString(record, "installationId"),
            record.GetProperty("documentId").GetString()
                ?? throw new InvalidDataException($"Settings document '{state.Id}' has no document id."),
            record.GetProperty("content").GetString()
                ?? throw new InvalidDataException($"Settings document '{state.Id}' has no content."),
            record.GetProperty("revision").GetInt64());
    }

    private static string? GetOptionalString(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).ValueKind is JsonValueKind.Null
            ? null
            : element.GetProperty(propertyName).GetString();

    private static bool IsAuthorized(
        AppOperationContext context,
        string capability,
        AppAuthority authority) =>
        context.HasCapability(capability)
        && AppAuthorityPolicy.Satisfies(context.EffectiveAuthority, authority);

    private static SettingsDocumentSnapshot ToSnapshot(
        SettingsDocumentDefinition definition,
        SettingsRecord record) =>
        new(definition.Path, record.Content, definition.MediaType, record.Revision);

    private static string FormatKey(SettingsDocumentKey key) => string.Join(
        '|',
        (int)key.Scope,
        Uri.EscapeDataString(key.AppId ?? string.Empty),
        Uri.EscapeDataString(key.UserId ?? string.Empty),
        Uri.EscapeDataString(key.InstallationId ?? string.Empty),
        Uri.EscapeDataString(key.DocumentId));

    private sealed record DefinitionState(string Id, SettingsDocumentDefinition Definition);

    private sealed record SettingsRecord(
        string Id,
        int Scope,
        string? AppId,
        string? UserId,
        string? InstallationId,
        string DocumentId,
        string Content,
        long Revision)
    {
        internal static SettingsRecord Create(DefinitionState state, string content, long revision) => new(
            state.Id,
            (int)state.Definition.Key.Scope,
            state.Definition.Key.AppId,
            state.Definition.Key.UserId,
            state.Definition.Key.InstallationId,
            state.Definition.Key.DocumentId,
            content,
            revision);
    }
}
