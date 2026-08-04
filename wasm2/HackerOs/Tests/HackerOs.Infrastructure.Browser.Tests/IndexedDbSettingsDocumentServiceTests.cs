using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.Infrastructure.Browser.Interop;
using HackerOs.Infrastructure.Browser.Settings;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Settings;
using Microsoft.JSInterop;

namespace HackerOs.Infrastructure.Browser.Tests;

/// <summary>Verifies canonical settings persistence at the browser interop boundary.</summary>
public sealed class IndexedDbSettingsDocumentServiceTests
{
    private static readonly SettingsDocumentKey Key = SettingsDocumentKey.ForAppUser(
        "org.hackeros.editor",
        "user");
    private static readonly VirtualPath Path = SettingsDocumentPathFactory.GetProjectionPath(Key);

    [Fact]
    public async Task WriteAsync_SeedsAndCommitsNextRevisionAtomically()
    {
        ScriptedJsObjectReference module = new(
            Result("{\"added\":true}"),
            Result("{\"committed\":true,\"actualValue\":1}"));
        await using IndexedDbSettingsDocumentService service = CreateService(module);

        SettingsWriteResult result = await service.WriteAsync(
            new SettingsWriteRequest(Path, "updated", 1),
            CreateContext());

        Assert.Equal(SettingsWriteStatus.Success, result.Status);
        Assert.Equal(2, result.Document?.Revision);
        Assert.Equal("updated", result.Document?.Content);
        JsInvocation compareInvocation = module.Invocations.Last();
        IndexedDbOperation operation = Assert.Single(
            Assert.IsAssignableFrom<IReadOnlyList<IndexedDbOperation>>(compareInvocation.Arguments[4]));
        Assert.Equal("compareAndPut", operation.Kind);
        Assert.Equal("revision", operation.CompareProperty);
        Assert.Equal(1L, operation.ExpectedValue);
    }

    [Fact]
    public async Task WriteAsync_StaleRevisionReturnsConflictWithoutRetry()
    {
        ScriptedJsObjectReference module = new(
            Result("{\"added\":false}"),
            Result("{\"committed\":false,\"actualValue\":3}"));
        await using IndexedDbSettingsDocumentService service = CreateService(module);

        SettingsWriteResult result = await service.WriteAsync(
            new SettingsWriteRequest(Path, "stale", 1),
            CreateContext());

        Assert.Equal(SettingsWriteStatus.Conflict, result.Status);
        Assert.Null(result.Document);
        Assert.Equal("settings.revision-conflict", Assert.Single(result.Errors!));
        Assert.Equal(3, module.Invocations.Count);
        Assert.Single(
            Assert.IsAssignableFrom<IReadOnlyList<IndexedDbOperation>>(
                module.Invocations.Last().Arguments[4]));
    }

    [Fact]
    public async Task InitializeAsync_seeds_all_definitions_in_one_transaction()
    {
        SettingsDocumentKey secondKey = SettingsDocumentKey.ForAppUser("org.hackeros.editor", "second-user");
        VirtualPath secondPath = SettingsDocumentPathFactory.GetProjectionPath(secondKey);
        ScriptedJsObjectReference module = new(
            [ResultElement("{\"added\":true}"), ResultElement("{\"added\":true}")]);
        await using IndexedDbSettingsDocumentService service = new(
            new FakeJsRuntime(module),
            [CreateDefinition(Path, Key), CreateDefinition(secondPath, secondKey)]);

        await service.InitializeAsync();

        JsInvocation transaction = Assert.Single(
            module.Invocations,
            invocation => invocation.Identifier == "executeTransaction");
        IReadOnlyList<IndexedDbOperation> operations =
            Assert.IsAssignableFrom<IReadOnlyList<IndexedDbOperation>>(transaction.Arguments[4]);
        Assert.Equal(2, operations.Count);
        Assert.All(operations, operation => Assert.Equal("addIfAbsent", operation.Kind));
    }

    private static IndexedDbSettingsDocumentService CreateService(IJSObjectReference module) => new(
        new FakeJsRuntime(module),
        [CreateDefinition(Path, Key)]);

    private static SettingsDocumentDefinition CreateDefinition(
        VirtualPath path,
        SettingsDocumentKey key) => new(
            path,
            key,
            "initial",
            "text/plain",
            "settings.read",
            "settings.write",
            AppAuthority.User,
            AppAuthority.User,
            new AcceptAllValidator());

    private static AppOperationContext CreateContext() => new()
    {
        AppId = "org.hackeros.editor",
        UserId = "user",
        UserAuthority = AppAuthority.User,
        GrantedCapabilities = new HashSet<string>(["settings.read", "settings.write"], StringComparer.Ordinal)
    };

    private static JsonElement[] Result(string json) => [JsonDocument.Parse(json).RootElement.Clone()];

    private static JsonElement ResultElement(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private sealed class AcceptAllValidator : ISettingsDocumentValidator
    {
        public IReadOnlyList<string> Validate(string content) => [];
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

    private sealed class ScriptedJsObjectReference(params JsonElement[][] transactionResults) : IJSObjectReference
    {
        private readonly Queue<JsonElement[]> _transactionResults = new(transactionResults);

        public List<JsInvocation> Invocations { get; } = [];

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            Invocations.Add(new JsInvocation(identifier, args ?? []));
            if (identifier == "executeTransaction")
            {
                return ValueTask.FromResult((TValue)(object)_transactionResults.Dequeue());
            }

            return ValueTask.FromResult(default(TValue)!);
        }
    }

    private sealed record JsInvocation(string Identifier, object?[] Arguments);
}
