using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.Infrastructure.Browser.Interop;
using HackerOs.Infrastructure.Browser.Sessions;
using HackerOs.Simulation.Abstractions.Sessions;
using Microsoft.JSInterop;

namespace HackerOs.Infrastructure.Browser.Tests;

public sealed class IndexedDbLocalUserRepositoryTests
{
    [Fact]
    public async Task Create_and_queries_round_trip_password_credential()
    {
        FakeIndexedDbModule module = new();
        await using IndexedDbLocalUserRepository repository = new(new FakeJsRuntime(module));
        LocalGroupId groupId = LocalGroupId.FromGuid(Guid.NewGuid());
        LocalPasswordCredential credential = new(
            "pbkdf2-sha256-v1",
            [1, 2, 3, 4],
            10_000,
            [5, 6, 7, 8]);

        LocalUser created = await repository.CreateUserAsync(
            LocalLoginName.Parse("admin"),
            "Administrator",
            AppAuthority.Administrator,
            groupId,
            credential: credential);

        LocalUser? byId = await repository.FindByIdAsync(created.Id);
        LocalUser? byLogin = await repository.FindByLoginNameAsync(LocalLoginName.Parse("ADMIN"));

        Assert.Equal(created.Id, byId!.Id);
        Assert.Equal(created.LoginName, byId.LoginName);
        Assert.Equal(created.Authority, byId.Authority);
        Assert.Equal(created.Id, byLogin!.Id);
        Assert.Equal(credential.Salt, byLogin!.Credential!.Salt);
        Assert.Equal(credential.Verifier, byLogin.Credential.Verifier);
        Assert.Contains(module.Operations, operation =>
            operation.Kind == "get" && operation.IndexName == "loginName" && Equals(operation.Key, "admin"));
    }

    [Fact]
    public async Task GetAll_sorts_users_and_last_administrator_cannot_be_disabled_or_demoted()
    {
        await using IndexedDbLocalUserRepository repository = new(
            new FakeJsRuntime(new FakeIndexedDbModule()));
        LocalGroupId groupId = LocalGroupId.FromGuid(Guid.NewGuid());
        LocalUser zed = await repository.CreateUserAsync(
            LocalLoginName.Parse("zed"), "Zed", AppAuthority.User, groupId);
        LocalUser admin = await repository.CreateUserAsync(
            LocalLoginName.Parse("admin"), "Admin", AppAuthority.Administrator, groupId);

        IReadOnlyList<LocalUser> users = await repository.GetAllAsync();

        Assert.Equal([admin.Id, zed.Id], users.Select(user => user.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await repository.SetEnabledAsync(admin.Id, enabled: false));
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await repository.SetAuthorityAsync(admin.Id, AppAuthority.User));
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

    private sealed class FakeIndexedDbModule : IJSObjectReference
    {
        private readonly Dictionary<string, JsonElement> _users = new(StringComparer.Ordinal);

        internal List<IndexedDbOperation> Operations { get; } = [];

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

            IReadOnlyList<IndexedDbOperation> operations =
                Assert.IsAssignableFrom<IReadOnlyList<IndexedDbOperation>>((args ?? [])[4]);
            Operations.AddRange(operations);
            JsonElement[] results = operations.Select(Execute).ToArray();
            return ValueTask.FromResult((TValue)(object)results);
        }

        private JsonElement Execute(IndexedDbOperation operation)
        {
            if (operation.Kind == "add")
            {
                JsonElement record = Serialize(operation.Value);
                _users.Add(record.GetProperty("id").GetString()!, record);
                return JsonSerializer.SerializeToElement(record.GetProperty("id").GetString());
            }

            if (operation.Kind == "getAll")
            {
                return JsonSerializer.SerializeToElement(_users.Values);
            }

            if (operation.Kind == "get")
            {
                if (operation.IndexName == "loginName")
                {
                    return _users.Values.FirstOrDefault(record =>
                        record.GetProperty("loginName").GetString() == (string)operation.Key!);
                }

                return _users.TryGetValue((string)operation.Key!, out JsonElement record)
                    ? record
                    : NullElement();
            }

            if (operation.Kind == "compareAndPut")
            {
                JsonElement current = _users[(string)operation.Key!];
                bool committed = current.GetProperty(operation.CompareProperty!).GetInt64()
                    == Convert.ToInt64(operation.ExpectedValue, System.Globalization.CultureInfo.InvariantCulture);
                if (committed)
                {
                    _users[(string)operation.Key!] = Serialize(operation.Value);
                }

                return JsonSerializer.SerializeToElement(new { committed });
            }

            return NullElement();
        }

        private static JsonElement Serialize(object? value) => JsonSerializer.SerializeToElement(
            value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        private static JsonElement NullElement() => JsonDocument.Parse("null").RootElement.Clone();
    }
}