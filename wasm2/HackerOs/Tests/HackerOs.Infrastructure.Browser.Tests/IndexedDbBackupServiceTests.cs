using System.Text.Json;
using HackerOs.Infrastructure.Browser.Backup;
using HackerOs.Infrastructure.Browser.Interop;
using HackerOs.Infrastructure.Browser.Schema;
using Microsoft.JSInterop;

namespace HackerOs.Infrastructure.Browser.Tests;

/// <summary>Verifies versioned exports and explicit atomic restore semantics.</summary>
public sealed class IndexedDbBackupServiceTests
{
    [Fact]
    public async Task Export_and_replace_use_all_store_snapshot_then_clear_and_put_atomically()
    {
        FakeIndexedDbModule module = new(CreateSnapshot());
        await using IndexedDbBackupService service = new(new FakeJsRuntime(module));

        string backup = await service.ExportAsync();
        IndexedDbRestoreResult result = await service.RestoreAsync(backup, IndexedDbRestoreMode.Replace);

        using JsonDocument document = JsonDocument.Parse(backup);
        Assert.Equal(1, document.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.Equal(64, document.RootElement.GetProperty("sha256").GetString()!.Length);
        Assert.Equal(1, result.RecordCount);
        TransactionInvocation export = module.Transactions[0];
        Assert.Equal("readonly", export.Mode);
        Assert.Equal(HackerOsIndexedDbSchema.ObjectStores.Count, export.Operations.Count);
        Assert.All(export.Operations, operation => Assert.Equal("getAll", operation.Kind));
        TransactionInvocation restore = module.Transactions[1];
        Assert.Equal("readwrite", restore.Mode);
        Assert.Equal(HackerOsIndexedDbSchema.ObjectStores.Count, restore.Operations.Count(operation => operation.Kind == "clear"));
        Assert.Single(restore.Operations, operation => operation.Kind == "put");
    }

    [Fact]
    public async Task Restore_rejects_tampered_backup_before_write_interop()
    {
        FakeIndexedDbModule module = new(CreateSnapshot());
        await using IndexedDbBackupService service = new(new FakeJsRuntime(module));
        string backup = await service.ExportAsync();
        string tampered = backup.Replace("user-one", "user-two", StringComparison.Ordinal);

        await Assert.ThrowsAsync<IndexedDbBackupValidationException>(async () =>
            await service.RestoreAsync(tampered, IndexedDbRestoreMode.Merge));

        Assert.Single(module.Transactions);
        Assert.Equal("readonly", module.Transactions[0].Mode);
    }

    [Fact]
    public async Task Merge_uses_add_if_absent_or_equal_and_reports_atomic_conflict()
    {
        FakeIndexedDbModule module = new(CreateSnapshot());
        await using IndexedDbBackupService service = new(new FakeJsRuntime(module));
        string backup = await service.ExportAsync();
        module.ThrowMergeConflict = true;

        IndexedDbBackupValidationException exception = await Assert.ThrowsAsync<IndexedDbBackupValidationException>(async () =>
            await service.RestoreAsync(backup, IndexedDbRestoreMode.Merge));

        TransactionInvocation restore = module.Transactions[1];
        IndexedDbOperation operation = Assert.Single(restore.Operations);
        Assert.Equal("addIfAbsentOrEqual", operation.Kind);
        Assert.Equal("backup.merge-conflict", operation.FailureCode);
        Assert.Contains("no records were changed", exception.Message, StringComparison.Ordinal);
    }

    private static JsonElement[] CreateSnapshot()
    {
        JsonElement empty = JsonDocument.Parse("[]").RootElement.Clone();
        JsonElement[] stores = Enumerable.Repeat(empty, HackerOsIndexedDbSchema.ObjectStores.Count).ToArray();
        int userIndex = HackerOsIndexedDbSchema.ObjectStores
            .Select((store, index) => (store, index))
            .Single(item => item.store.Name == HackerOsIndexedDbSchema.UserStoreName)
            .index;
        stores[userIndex] = JsonDocument.Parse("[{\"id\":\"user-one\",\"loginName\":\"alice\"}]")
            .RootElement.Clone();
        return stores;
    }

    private sealed class FakeJsRuntime(IJSObjectReference module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) => ValueTask.FromResult((TValue)module);
    }

    private sealed class FakeIndexedDbModule(JsonElement[] snapshot) : IJSObjectReference
    {
        internal List<TransactionInvocation> Transactions { get; } = [];
        internal bool ThrowMergeConflict { get; set; }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            if (identifier != "executeTransaction")
            {
                return ValueTask.FromResult(default(TValue)!);
            }

            object?[] arguments = args ?? [];
            TransactionInvocation invocation = new(
                Assert.IsType<string>(arguments[3]),
                Assert.IsAssignableFrom<IReadOnlyList<IndexedDbOperation>>(arguments[4]));
            Transactions.Add(invocation);
            if (ThrowMergeConflict && invocation.Mode == "readwrite")
            {
                throw new JSException("ConstraintError: backup.merge-conflict.");
            }

            return ValueTask.FromResult((TValue)(object)(invocation.Mode == "readonly" ? snapshot : []));
        }
    }

    private sealed record TransactionInvocation(
        string Mode,
        IReadOnlyList<IndexedDbOperation> Operations);
}