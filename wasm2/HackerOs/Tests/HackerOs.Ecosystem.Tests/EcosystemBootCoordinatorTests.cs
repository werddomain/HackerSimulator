using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Infrastructure.Browser.FileSystem;
using HackerOs.Infrastructure.Browser.Settings;
using HackerOs.Platform.Core;
using HackerOs.Simulation.Abstractions.Sessions;
using Microsoft.JSInterop;

namespace HackerOs.Ecosystem.Tests;

public sealed class EcosystemBootCoordinatorTests
{
    private readonly IndexedDbFileSystemBootstrapper _fsBootstrapper;
    private readonly IndexedDbSettingsDocumentService _settings;
    private readonly FakeCapabilityGrantRepository _grants = new();
    private readonly FakeAppCatalogRepository _catalog = new();
    private readonly AppCatalog _selectedCatalog = CreateEmptyCatalog();
    private readonly FakeLocalGroupRepository _groups = new();
    private readonly FakeLocalUserRepository _users = new();

    public EcosystemBootCoordinatorTests()
    {
        IJSRuntime jsRuntime = new TestJsRuntime();
        _fsBootstrapper = new IndexedDbFileSystemBootstrapper(jsRuntime);
        _settings = new IndexedDbSettingsDocumentService(jsRuntime, []);
    }

    [Fact]
    public void Constructor_validates_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => new EcosystemBootCoordinator(
            null!, _settings, _grants, _catalog, _selectedCatalog, _groups, _users));
        Assert.Throws<ArgumentNullException>(() => new EcosystemBootCoordinator(
            _fsBootstrapper, null!, _grants, _catalog, _selectedCatalog, _groups, _users));
        Assert.Throws<ArgumentNullException>(() => new EcosystemBootCoordinator(
            _fsBootstrapper, _settings, null!, _catalog, _selectedCatalog, _groups, _users));
        Assert.Throws<ArgumentNullException>(() => new EcosystemBootCoordinator(
            _fsBootstrapper, _settings, _grants, null!, _selectedCatalog, _groups, _users));
        Assert.Throws<ArgumentNullException>(() => new EcosystemBootCoordinator(
            _fsBootstrapper, _settings, _grants, _catalog, null!, _groups, _users));
        Assert.Throws<ArgumentNullException>(() => new EcosystemBootCoordinator(
            _fsBootstrapper, _settings, _grants, _catalog, _selectedCatalog, null!, _users));
        Assert.Throws<ArgumentNullException>(() => new EcosystemBootCoordinator(
            _fsBootstrapper, _settings, _grants, _catalog, _selectedCatalog, _groups, null!));
    }

    [Fact]
    public async Task BootAsync_returns_boot_result_on_success()
    {
        EcosystemBootCoordinator bootCoordinator = new(
            _fsBootstrapper,
            _settings,
            _grants,
            _catalog,
            _selectedCatalog,
            _groups,
            _users);

        _grants.PolicyRevision = 1L;
        EcosystemBootResult result = await bootCoordinator.BootAsync();

        Assert.NotNull(result);
        Assert.Equal(1L, result.PolicyRevision);
        Assert.Equal(0, result.CatalogEntryCount);
        Assert.Empty(result.Users);
    }

    [Fact]
    public async Task BootAsync_reconciles_the_host_selected_catalog()
    {
        EcosystemBootCoordinator bootCoordinator = new(
            _fsBootstrapper,
            _settings,
            _grants,
            _catalog,
            BuildKnownLazyApps.Catalog,
            _groups,
            _users);

        await bootCoordinator.BootAsync();

        Assert.Equal(12, _catalog.SelectedManifests.Count);
        Assert.Contains(_catalog.SelectedManifests, manifest => manifest.Id == "org.hackeros.hack-paint");
    }

    [Fact]
    public async Task BootAsync_throws_when_policy_revision_is_invalid()
    {
        EcosystemBootCoordinator bootCoordinator = new(
            _fsBootstrapper,
            _settings,
            _grants,
            _catalog,
            _selectedCatalog,
            _groups,
            _users);

        _grants.PolicyRevision = -1L;
        await Assert.ThrowsAsync<InvalidOperationException>(() => bootCoordinator.BootAsync().AsTask());
    }

    [Fact]
    public async Task BootAsync_throws_on_cancellation()
    {
        EcosystemBootCoordinator bootCoordinator = new(
            _fsBootstrapper,
            _settings,
            _grants,
            _catalog,
            _selectedCatalog,
            _groups,
            _users);

        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => bootCoordinator.BootAsync(cts.Token).AsTask());
    }

    private sealed class TestJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            if (typeof(TValue) == typeof(IJSObjectReference))
            {
                return new ValueTask<TValue>((TValue)(object)new TestJsObjectReference());
            }

            return new ValueTask<TValue>(default(TValue)!);
        }
    }

    private static AppCatalog CreateEmptyCatalog() =>
        Assert.IsType<AppCatalog>(AppCatalog.Build([]).Catalog);

    private sealed class TestJsObjectReference : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            new(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) =>
            new(default(TValue)!);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeCapabilityGrantRepository : IPersistentCapabilityGrantRepository
    {
        public long PolicyRevision { get; set; } = 1L;

        public ValueTask<long> GetCurrentPolicyRevisionAsync(CancellationToken cancellationToken = default) =>
            new(PolicyRevision);

        public ValueTask<CapabilityGrantMutationResult> GrantAsync(
            string appId,
            string userId,
            string capability,
            CapabilityGrantSource source,
            AppAuthority actingAuthority,
            IEnumerable<CapabilityConstraint>? constraints = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public ValueTask<CapabilityGrantMutationResult> RevokeAsync(
            CapabilityGrantId grantId,
            AppAuthority actingAuthority,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public ValueTask<CapabilityGrantMutationResult> ImportAsync(
            CapabilityGrantId id,
            string appId,
            string userId,
            string capability,
            CapabilityGrantSource source,
            IEnumerable<CapabilityConstraint>? constraints,
            bool isRevoked,
            AppAuthority actingAuthority,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public ValueTask<CapabilityPolicyEvaluation> EvaluateAsync(
            string appId,
            string userId,
            string capability,
            AppAuthority actingAuthority,
            AppAuthority requiredAuthority,
            CapabilityResourceCandidate? resourceCandidate = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeAppCatalogRepository : IPersistentAppCatalogRepository
    {
        public IReadOnlyList<AppManifest> SelectedManifests { get; private set; } = [];

        public ValueTask<IReadOnlyList<PersistedAppCatalogEntry>> ReconcileAsync(
            IEnumerable<AppManifest> selectedManifests,
            CancellationToken cancellationToken = default)
        {
            SelectedManifests = [.. selectedManifests];
            return new(new List<PersistedAppCatalogEntry>());
        }

        public ValueTask<IReadOnlyList<PersistedAppCatalogEntry>> ReadAllAsync(
            CancellationToken cancellationToken = default) =>
            new(new List<PersistedAppCatalogEntry>());

        public ValueTask<bool> SetEnabledAsync(
            string appId,
            bool enabled,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeLocalGroupRepository : ILocalGroupRepository
    {
        public ValueTask<LocalGroup?> FindByNameAsync(
            LocalLoginName name,
            CancellationToken cancellationToken = default) =>
            new((LocalGroup?)null);

        public ValueTask<LocalGroup?> FindByIdAsync(
            LocalGroupId id,
            CancellationToken cancellationToken = default) =>
            new((LocalGroup?)null);

        public ValueTask<LocalGroup> CreateGroupAsync(
            LocalLoginName name,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeLocalUserRepository : ILocalUserRepository
    {
        public ValueTask<IReadOnlyList<LocalUser>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            new(new List<LocalUser>());

        public ValueTask<LocalUser?> FindByLoginNameAsync(
            LocalLoginName loginName,
            CancellationToken cancellationToken = default) =>
            new((LocalUser?)null);

        public ValueTask<LocalUser?> FindByIdAsync(
            LocalUserId id,
            CancellationToken cancellationToken = default) =>
            new((LocalUser?)null);

        public ValueTask<LocalUser> CreateUserAsync(
            LocalLoginName loginName,
            string displayName,
            AppAuthority authority,
            LocalGroupId primaryGroupId,
            IReadOnlyCollection<LocalGroupId>? additionalGroupIds = null,
            LocalPasswordCredential? credential = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public ValueTask<LocalUser> SetEnabledAsync(
            LocalUserId id,
            bool enabled,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public ValueTask<LocalUser> SetAuthorityAsync(
            LocalUserId id,
            AppAuthority authority,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
