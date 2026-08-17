using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Platform.Core.ServerConnection;
using HackerOs.Server.Contracts.Sync;
using HackerOs.Simulation.Abstractions.ServerConnection;
using HackerOs.Simulation.Abstractions.Sync;

namespace HackerOs.Platform.Core.Tests.ServerConnection;

/// <summary>
/// Exercises <see cref="AppCatalogSyncService"/> (ADR 0033) against fakes for every dependency —
/// mirrors <c>SettingsSyncServiceTests</c>'s/<c>FileSystemSyncServiceTests</c>'s approach.
/// </summary>
public sealed class AppCatalogSyncServiceTests
{
    private static readonly Uri ServerBaseUrl = new("https://example.test/");
    private static readonly ServerConnectionState Connected = new(
        Guid.NewGuid(), Guid.NewGuid(), ServerBaseUrl.ToString(), "fingerprint", "refresh-token", DateTimeOffset.UtcNow.AddDays(30));

    [Fact]
    public async Task PushAsync_WhenDisconnected_NeverCallsSyncClient()
    {
        var catalog = new FakeAppCatalogRepository();
        var syncClient = new FakeSyncClient();
        var service = CreateService(catalog, syncClient, connectionState: null);

        await service.PushAsync();

        Assert.Empty(syncClient.PushCalls);
    }

    [Fact]
    public async Task PushAsync_FirstPush_SendsOneRecordPerEntry()
    {
        var catalog = new FakeAppCatalogRepository();
        catalog.Seed("org.hackeros.calculator", isEnabled: true);
        catalog.Seed("org.hackeros.terminal", isEnabled: false);
        var syncClient = new FakeSyncClient();
        var service = CreateService(catalog, syncClient, Connected);

        await service.PushAsync();

        List<SyncRecordEnvelope> records = [.. Assert.Single(syncClient.PushCalls).Records];
        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Equal(SyncDomain.AppCatalog, r.Domain));
        Assert.All(records, r => Assert.Equal(1, r.Revision));
    }

    [Fact]
    public async Task PushAsync_UnchangedSinceLastSync_SendsNothing()
    {
        var catalog = new FakeAppCatalogRepository();
        catalog.Seed("org.hackeros.calculator", isEnabled: true);
        var syncClient = new FakeSyncClient();
        var service = CreateService(catalog, syncClient, Connected);

        await service.PushAsync();
        await service.PushAsync();

        Assert.Single(syncClient.PushCalls);
    }

    [Fact]
    public async Task PushAsync_ChangedFlag_SendsIncrementedRevision()
    {
        var catalog = new FakeAppCatalogRepository();
        catalog.Seed("org.hackeros.calculator", isEnabled: true);
        var syncClient = new FakeSyncClient();
        var service = CreateService(catalog, syncClient, Connected);
        await service.PushAsync();

        catalog.Seed("org.hackeros.calculator", isEnabled: false);
        await service.PushAsync();

        Assert.Equal(2, syncClient.PushCalls.Count);
        SyncRecordEnvelope secondEnvelope = Assert.Single(syncClient.PushCalls[1].Records);
        Assert.Equal(2, secondEnvelope.Revision);
    }

    [Fact]
    public async Task PullAsync_AppliesEnvelope_UpdatesRepositoryAndLiveEnablement()
    {
        var catalog = new FakeAppCatalogRepository();
        catalog.Seed("org.hackeros.calculator", isEnabled: true);
        AppEnablementRegistry enablement = new(CatalogWith("org.hackeros.calculator"));
        Assert.True(enablement.IsEnabled("org.hackeros.calculator")); // sanity: starts enabled
        Guid recordId = ComputeExpectedRecordId("org.hackeros.calculator");
        var payload = new AppCatalogSyncPayload("org.hackeros.calculator", IsEnabled: false);
        var syncClient = new FakeSyncClient
        {
            PullResponses = new Queue<PullResponse>(
            [
                new PullResponse(
                    SyncDomain.AppCatalog,
                    [new SyncRecordEnvelope(recordId, SyncDomain.AppCatalog, 1, Connected.AccountId, Connected.DeviceId,
                        5, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "hash", false,
                        System.Text.Json.JsonSerializer.Serialize(payload, AppCatalogSyncContractsJsonContext.Default.AppCatalogSyncPayload))],
                    NextCursor: null,
                    HasMore: false,
                    DateTimeOffset.UtcNow)
            ])
        };
        var service = CreateService(catalog, syncClient, Connected, enablementOverride: enablement);

        await service.PullAsync();

        Assert.False(Assert.Single(catalog.Entries, e => e.Manifest.Id == "org.hackeros.calculator").IsEnabled);
        Assert.False(enablement.IsEnabled("org.hackeros.calculator"));
    }

    [Fact]
    public async Task PullAsync_AppNotPresentOnThisDevice_IsSkippedWithoutThrowing()
    {
        var catalog = new FakeAppCatalogRepository(); // deliberately empty — app doesn't exist locally
        Guid recordId = ComputeExpectedRecordId("org.hackeros.unknown-app");
        var payload = new AppCatalogSyncPayload("org.hackeros.unknown-app", IsEnabled: false);
        var syncClient = new FakeSyncClient
        {
            PullResponses = new Queue<PullResponse>(
            [
                new PullResponse(
                    SyncDomain.AppCatalog,
                    [new SyncRecordEnvelope(recordId, SyncDomain.AppCatalog, 1, Connected.AccountId, Connected.DeviceId,
                        1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "hash", false,
                        System.Text.Json.JsonSerializer.Serialize(payload, AppCatalogSyncContractsJsonContext.Default.AppCatalogSyncPayload))],
                    NextCursor: null,
                    HasMore: false,
                    DateTimeOffset.UtcNow)
            ])
        };
        var service = CreateService(catalog, syncClient, Connected);

        await service.PullAsync(); // must not throw

        Assert.Empty(catalog.Entries);
    }

    [Fact]
    public async Task PullAsync_WhenDisconnected_NeverCallsSyncClient()
    {
        var catalog = new FakeAppCatalogRepository();
        var syncClient = new FakeSyncClient();
        var service = CreateService(catalog, syncClient, connectionState: null);

        await service.PullAsync();

        Assert.Empty(syncClient.PullCalls);
    }

    private static Guid ComputeExpectedRecordId(string appId)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(appId));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static AppCatalog EmptyCatalog() => Assert.IsType<AppCatalog>(AppCatalog.Build([]).Catalog);

    private static AppCatalog CatalogWith(string appId) =>
        Assert.IsType<AppCatalog>(AppCatalog.Build([MinimalManifest(appId)]).Catalog);

    private static AppManifest MinimalManifest(string appId) => new()
    {
        Id = appId,
        Name = appId,
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "Test app.",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("Test", "Test.EntryPoint"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("testcmd", [], "testcmd")
    };

    private static AppCatalogSyncService CreateService(
        FakeAppCatalogRepository catalog,
        FakeSyncClient syncClient,
        ServerConnectionState? connectionState,
        AppEnablementRegistry? enablementOverride = null) =>
        new(
            catalog,
            enablementOverride ?? new AppEnablementRegistry(EmptyCatalog()),
            new FakeServerConnectionService(connectionState),
            syncClient,
            new InMemorySyncCursorRepository(),
            new InMemorySyncRecordStateRepository());

    private sealed class FakeServerConnectionService(ServerConnectionState? state) : IServerConnectionService
    {
        public ValueTask<ServerConnectionState?> GetStateAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(state);

        public Task<ServerConnectionState> ConnectWithNewAccountAsync(
            Uri serverBaseUrl, string username, string password, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ServerConnectionState> ConnectWithExistingAccountAsync(
            Uri serverBaseUrl, string username, string password, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public Task<string?> EnsureAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(state is null ? null : "access-token");
    }

    private sealed class FakeSyncClient : ISyncClient
    {
        public List<PushRequest> PushCalls { get; } = [];
        public List<PullRequest> PullCalls { get; } = [];
        public Queue<PullResponse> PullResponses { get; set; } = [];

        public Task<PullResponse> PullAsync(
            Uri serverBaseUrl, string accessToken, PullRequest request, CancellationToken cancellationToken = default)
        {
            PullCalls.Add(request);
            return Task.FromResult(PullResponses.Count > 0
                ? PullResponses.Dequeue()
                : new PullResponse(request.Domain, [], null, false, DateTimeOffset.UtcNow));
        }

        public Task<PushResponse> PushAsync(
            Uri serverBaseUrl, string accessToken, PushRequest request, CancellationToken cancellationToken = default)
        {
            PushCalls.Add(request);
            return Task.FromResult(new PushResponse(PushOutcome.Accepted, request.Records.Count, []));
        }

        public Task<ResolveSyncConflictResponse> ResolveConflictAsync(
            Uri serverBaseUrl, string accessToken, ResolveSyncConflictRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by these tests.");
    }

    private sealed class InMemorySyncCursorRepository : ISyncCursorRepository
    {
        private readonly Dictionary<string, string?> _cursors = [];

        public ValueTask<string?> GetCursorAsync(string domain, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_cursors.GetValueOrDefault(domain));

        public ValueTask SetCursorAsync(string domain, string? cursor, CancellationToken cancellationToken = default)
        {
            _cursors[domain] = cursor;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class InMemorySyncRecordStateRepository : ISyncRecordStateRepository
    {
        private readonly Dictionary<(string Domain, Guid RecordId), SyncRecordTrackingState> _state = [];

        public ValueTask<SyncRecordTrackingState?> GetAsync(
            string domain, Guid recordId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_state.GetValueOrDefault((domain, recordId)));

        public ValueTask SetAsync(
            string domain, Guid recordId, long revision, string contentHash, CancellationToken cancellationToken = default)
        {
            _state[(domain, recordId)] = new SyncRecordTrackingState(revision, contentHash);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeAppCatalogRepository : IPersistentAppCatalogRepository
    {
        private readonly Dictionary<string, PersistedAppCatalogEntry> _entries = [];

        public IReadOnlyList<PersistedAppCatalogEntry> Entries => [.. _entries.Values];

        public void Seed(string appId, bool isEnabled) =>
            _entries[appId] = new PersistedAppCatalogEntry(MinimalManifest(appId), isEnabled);

        public ValueTask<IReadOnlyList<PersistedAppCatalogEntry>> ReconcileAsync(
            IEnumerable<AppManifest> selectedManifests, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by these tests.");

        public ValueTask<bool> SetEnabledAsync(string appId, bool enabled, CancellationToken cancellationToken = default)
        {
            if (!_entries.TryGetValue(appId, out PersistedAppCatalogEntry? existing))
            {
                return ValueTask.FromResult(false);
            }

            _entries[appId] = existing with { IsEnabled = enabled };
            return ValueTask.FromResult(true);
        }

        public ValueTask<IReadOnlyList<PersistedAppCatalogEntry>> ReadAllAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Entries);
    }
}
