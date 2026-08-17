using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HackerOs.Infrastructure.Browser.Interop;
using HackerOs.Infrastructure.Browser.Schema;
using Microsoft.JSInterop;

namespace HackerOs.Infrastructure.Browser.Backup;

/// <summary>Exports and atomically restores validated HackerOS IndexedDB backups.</summary>
public sealed class IndexedDbBackupService : IAsyncDisposable
{
    private const int FormatVersion = 1;
    private const string TransactionBoundary = "BackupRestore";
    private readonly IndexedDbInteropAdapter _adapter;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a backup service using the browser IndexedDB adapter.</summary>
    public IndexedDbBackupService(IJSRuntime runtime, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _adapter = new IndexedDbInteropAdapter(runtime);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Exports every store in one read-only snapshot with a SHA-256 integrity digest.</summary>
    public async ValueTask<string> ExportAsync(CancellationToken cancellationToken = default)
    {
        await _adapter.OpenAsync(IndexedDbMigrationPlan.CreateCurrent(), cancellationToken).ConfigureAwait(false);
        IndexedDbOperation[] operations = HackerOsIndexedDbSchema.ObjectStores
            .Select(store => new IndexedDbOperation("getAll", store.Name))
            .ToArray();
        IReadOnlyList<JsonElement> results = await _adapter.ExecuteAsync(
            TransactionBoundary,
            IndexedDbTransactionMode.ReadOnly,
            operations,
            cancellationToken).ConfigureAwait(false);

        JsonObject payload = BuildPayload(
            _timeProvider.GetUtcNow(),
            HackerOsIndexedDbSchema.ObjectStores.Select((store, index) => (store.Name, results[index])));
        JsonObject envelope = JsonNode.Parse(payload.ToJsonString())!.AsObject();
        envelope["sha256"] = ComputeHash(payload);
        return envelope.ToJsonString();
    }

    /// <summary>Validates a backup completely, then restores it using the explicitly selected mode.</summary>
    public async ValueTask<IndexedDbRestoreResult> RestoreAsync(
        string backupJson,
        IndexedDbRestoreMode mode,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        ValidatedBackup backup = Validate(backupJson);
        List<IndexedDbOperation> operations = [];
        if (mode == IndexedDbRestoreMode.Replace)
        {
            operations.AddRange(HackerOsIndexedDbSchema.ObjectStores
                .Select(store => new IndexedDbOperation("clear", store.Name)));
        }

        foreach ((string storeName, JsonElement records) in backup.Stores)
        {
            string kind = mode == IndexedDbRestoreMode.Replace ? "put" : "addIfAbsentOrEqual";
            operations.AddRange(records.EnumerateArray().Select(record =>
                new IndexedDbOperation(kind, storeName, Value: record.Clone(), FailureCode: "backup.merge-conflict")));
        }

        await _adapter.OpenAsync(IndexedDbMigrationPlan.CreateCurrent(), cancellationToken).ConfigureAwait(false);
        try
        {
            await _adapter.ExecuteAsync(
                TransactionBoundary,
                IndexedDbTransactionMode.ReadWrite,
                operations,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JSException exception) when (exception.Message.Contains("backup.merge-conflict", StringComparison.Ordinal))
        {
            throw new IndexedDbBackupValidationException(
                "Merge found an existing record with different content; no records were changed.", exception);
        }

        return new IndexedDbRestoreResult(mode, backup.RecordCount);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _adapter.DisposeAsync();

    private static ValidatedBackup Validate(string backupJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupJson);
        JsonObject envelope;
        try
        {
            envelope = JsonNode.Parse(backupJson)?.AsObject()
                ?? throw new JsonException("Backup root must be an object.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new IndexedDbBackupValidationException("Backup JSON is malformed.", exception);
        }

        int formatVersion = RequiredInt(envelope, "formatVersion");
        string databaseName = RequiredString(envelope, "databaseName");
        int schemaVersion = RequiredInt(envelope, "databaseSchemaVersion");
        string createdAt = RequiredString(envelope, "createdAtUtc");
        string expectedHash = RequiredString(envelope, "sha256");
        if (formatVersion != FormatVersion
            || databaseName != HackerOsIndexedDbSchema.DatabaseName
            || schemaVersion != HackerOsIndexedDbSchema.CurrentVersion
            || !DateTimeOffset.TryParse(createdAt, out DateTimeOffset createdAtUtc)
            || expectedHash.Length != 64)
        {
            throw new IndexedDbBackupValidationException("Backup format or database schema is incompatible.");
        }

        JsonObject storesNode = envelope["stores"] as JsonObject
            ?? throw new IndexedDbBackupValidationException("Backup stores object is missing.");
        string[] expectedStores = HackerOsIndexedDbSchema.ObjectStores.Select(store => store.Name).ToArray();
        if (!storesNode.Select(item => item.Key).Order().SequenceEqual(expectedStores.Order(), StringComparer.Ordinal))
        {
            throw new IndexedDbBackupValidationException("Backup store set does not match the current schema.");
        }

        List<(string Name, JsonElement Records)> stores = [];
        foreach (IndexedDbObjectStoreDefinition definition in HackerOsIndexedDbSchema.ObjectStores)
        {
            JsonArray recordsNode = storesNode[definition.Name] as JsonArray
                ?? throw new IndexedDbBackupValidationException($"Store '{definition.Name}' must be an array.");
            JsonElement records = JsonDocument.Parse(recordsNode.ToJsonString()).RootElement.Clone();
            ValidateRecordIdentities(definition, records);
            stores.Add((definition.Name, records));
        }

        JsonObject payload = BuildPayload(createdAtUtc, stores.Select(store => (store.Name, store.Records)));
        string actualHash = ComputeHash(payload);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedHash.ToLowerInvariant()),
            Encoding.ASCII.GetBytes(actualHash)))
        {
            throw new IndexedDbBackupValidationException("Backup SHA-256 integrity validation failed.");
        }

        return new ValidatedBackup(stores, stores.Sum(store => store.Records.GetArrayLength()));
    }

    private static void ValidateRecordIdentities(IndexedDbObjectStoreDefinition definition, JsonElement records)
    {
        HashSet<string> identities = new(StringComparer.Ordinal);
        foreach (JsonElement record in records.EnumerateArray())
        {
            if (record.ValueKind != JsonValueKind.Object)
            {
                throw new IndexedDbBackupValidationException($"Store '{definition.Name}' contains a non-object record.");
            }

            string identity = string.Join("\u001f", definition.KeyPath.Select(property =>
            {
                if (!record.TryGetProperty(property, out JsonElement value)
                    || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                    or JsonValueKind.Object or JsonValueKind.Array)
                {
                    throw new IndexedDbBackupValidationException(
                        $"Store '{definition.Name}' record is missing valid key property '{property}'.");
                }
                return value.GetRawText();
            }));
            if (!identities.Add(identity))
            {
                throw new IndexedDbBackupValidationException($"Store '{definition.Name}' contains duplicate identities.");
            }

            if (definition.Name == HackerOsIndexedDbSchema.CatalogStoreName
                && (!record.TryGetProperty("enabledFlag", out JsonElement enabledFlag)
                    || enabledFlag.ValueKind != JsonValueKind.Number
                    || enabledFlag.GetInt32() is not (0 or 1)))
            {
                throw new IndexedDbBackupValidationException("Catalog enabledFlag must be 0 or 1.");
            }
        }
    }

    private static JsonObject BuildPayload(
        DateTimeOffset createdAtUtc,
        IEnumerable<(string Name, JsonElement Records)> stores)
    {
        JsonObject storesNode = [];
        foreach ((string name, JsonElement records) in stores)
        {
            storesNode[name] = JsonNode.Parse(records.GetRawText());
        }

        return new JsonObject
        {
            ["formatVersion"] = FormatVersion,
            ["databaseName"] = HackerOsIndexedDbSchema.DatabaseName,
            ["databaseSchemaVersion"] = HackerOsIndexedDbSchema.CurrentVersion,
            ["createdAtUtc"] = createdAtUtc.ToUniversalTime().ToString("O"),
            ["stores"] = storesNode
        };
    }

    private static string ComputeHash(JsonObject payload) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToJsonString())));

    private static string RequiredString(JsonObject value, string property) =>
        value[property]?.GetValue<string>()
        ?? throw new IndexedDbBackupValidationException($"Backup property '{property}' is missing.");

    private static int RequiredInt(JsonObject value, string property) =>
        value[property]?.GetValue<int>()
        ?? throw new IndexedDbBackupValidationException($"Backup property '{property}' is missing.");

    private sealed record ValidatedBackup(
        IReadOnlyList<(string Name, JsonElement Records)> Stores,
        int RecordCount);
}

/// <summary>Selects explicit non-destructive merge or destructive replacement restore semantics.</summary>
public enum IndexedDbRestoreMode
{
    /// <summary>Adds missing records and aborts atomically on a different existing record.</summary>
    Merge = 1,

    /// <summary>Clears all stores and writes the validated backup in one transaction.</summary>
    Replace = 2
}

/// <summary>Reports the completed restore mode and number of backup records processed.</summary>
public sealed record IndexedDbRestoreResult(IndexedDbRestoreMode Mode, int RecordCount);

/// <summary>Represents malformed, incompatible, corrupt, or conflicting backup data.</summary>
public sealed class IndexedDbBackupValidationException : Exception
{
    /// <summary>Creates a validation failure.</summary>
    public IndexedDbBackupValidationException(string message) : base(message) { }

    /// <summary>Creates a validation failure preserving its underlying cause.</summary>
    public IndexedDbBackupValidationException(string message, Exception innerException)
        : base(message, innerException) { }
}