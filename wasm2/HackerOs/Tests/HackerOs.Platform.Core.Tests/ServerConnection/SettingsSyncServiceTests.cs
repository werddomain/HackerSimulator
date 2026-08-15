using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Appearance;
using HackerOs.Platform.Core.ServerConnection;
using HackerOs.Server.Contracts.Sync;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.ServerConnection;
using HackerOs.Simulation.Abstractions.Sync;

namespace HackerOs.Platform.Core.Tests.ServerConnection;

/// <summary>
/// Exercises <see cref="SettingsSyncService"/> (ADR 0029) against fakes for every dependency —
/// mirrors the fake-based approach <c>Wave4NetworkTests</c> already uses for <c>IProxyClient</c>.
/// </summary>
public sealed class SettingsSyncServiceTests
{
    private static readonly SettingsDocumentDefinition AppearanceDefinition = AppearanceSettingsDocuments.CreateDefinition();
    private static readonly Uri ServerBaseUrl = new("https://example.test/");
    private static readonly ServerConnectionState Connected = new(
        Guid.NewGuid(), Guid.NewGuid(), ServerBaseUrl.ToString(), "fingerprint", "refresh-token", DateTimeOffset.UtcNow.AddDays(30));

    [Fact]
    public async Task PushAsync_WhenDisconnected_NeverCallsSyncClient()
    {
        var settings = new FakeSettingsDocumentService();
        var syncClient = new FakeSyncClient();
        var service = CreateService(settings, syncClient, connectionState: null);

        await service.PushAsync();

        Assert.Empty(syncClient.PushCalls);
    }

    [Fact]
    public async Task PushAsync_FirstPush_SendsRevisionOne()
    {
        var settings = new FakeSettingsDocumentService();
        settings.Seed(AppearanceDefinition.Path, "{\"accent\":\"cyan\"}", revision: 1);
        var syncClient = new FakeSyncClient();
        var service = CreateService(settings, syncClient, Connected);

        await service.PushAsync();

        SyncRecordEnvelope envelope = Assert.Single(Assert.Single(syncClient.PushCalls).Records);
        Assert.Equal(SyncDomain.Settings, envelope.Domain);
        Assert.Equal(1, envelope.Revision);
        Assert.Equal("{\"accent\":\"cyan\"}", envelope.PayloadJson);
    }

    [Fact]
    public async Task PushAsync_UnchangedSinceLastSync_SendsNothing()
    {
        var settings = new FakeSettingsDocumentService();
        settings.Seed(AppearanceDefinition.Path, "{\"accent\":\"cyan\"}", revision: 1);
        var syncClient = new FakeSyncClient();
        var service = CreateService(settings, syncClient, Connected);

        await service.PushAsync();
        await service.PushAsync(); // Nothing changed locally between calls.

        Assert.Single(syncClient.PushCalls); // Only the first push actually sent anything.
    }

    [Fact]
    public async Task PushAsync_ChangedContent_SendsIncrementedRevision()
    {
        var settings = new FakeSettingsDocumentService();
        settings.Seed(AppearanceDefinition.Path, "{\"accent\":\"cyan\"}", revision: 1);
        var syncClient = new FakeSyncClient();
        var service = CreateService(settings, syncClient, Connected);

        await service.PushAsync();
        settings.Seed(AppearanceDefinition.Path, "{\"accent\":\"purple\"}", revision: 2);
        await service.PushAsync();

        Assert.Equal(2, syncClient.PushCalls.Count);
        SyncRecordEnvelope secondEnvelope = Assert.Single(syncClient.PushCalls[1].Records);
        Assert.Equal(2, secondEnvelope.Revision);
        Assert.Equal("{\"accent\":\"purple\"}", secondEnvelope.PayloadJson);
    }

    [Fact]
    public async Task PushAsync_SameKey_AlwaysProducesTheSameRecordId()
    {
        var settings = new FakeSettingsDocumentService();
        settings.Seed(AppearanceDefinition.Path, "{\"accent\":\"cyan\"}", revision: 1);
        var syncClient = new FakeSyncClient();
        var service = CreateService(settings, syncClient, Connected);
        await service.PushAsync();
        Guid firstRecordId = syncClient.PushCalls[0].Records[0].RecordId;

        // A brand-new service instance (simulating a second device/session) computes the same
        // RecordId for the same document key, per ADR 0029 Decision 3.
        var secondSettings = new FakeSettingsDocumentService();
        secondSettings.Seed(AppearanceDefinition.Path, "{\"accent\":\"cyan\"}", revision: 1);
        var secondSyncClient = new FakeSyncClient();
        var secondService = CreateService(secondSettings, secondSyncClient, Connected);
        await secondService.PushAsync();

        Assert.Equal(firstRecordId, secondSyncClient.PushCalls[0].Records[0].RecordId);
    }

    [Fact]
    public async Task PullAsync_AppliesEnvelope_AndAdvancesCursorOnlyWhenGiven()
    {
        var settings = new FakeSettingsDocumentService();
        settings.Seed(AppearanceDefinition.Path, "{\"accent\":\"green\"}", revision: 1);
        Guid recordId = ComputeExpectedRecordId(AppearanceDefinition);
        var syncClient = new FakeSyncClient
        {
            PullResponses = new Queue<PullResponse>(
            [
                new PullResponse(
                    SyncDomain.Settings,
                    [new SyncRecordEnvelope(recordId, SyncDomain.Settings, 1, Connected.AccountId, Connected.DeviceId,
                        5, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "hash", false, "{\"accent\":\"purple\"}")],
                    NextCursor: null,
                    HasMore: false,
                    DateTimeOffset.UtcNow)
            ])
        };
        var cursorRepository = new InMemorySyncCursorRepository();
        var service = CreateService(settings, syncClient, Connected, cursorRepository);

        await service.PullAsync();

        Assert.Equal("{\"accent\":\"purple\"}", settings.CurrentContent(AppearanceDefinition.Path));
        // The server never returns NextCursor when HasMore is false (ADR 0029 Implementation notes) —
        // the cursor must stay unset here, not be advanced to a null-coalesced value.
        Assert.Null(await cursorRepository.GetCursorAsync(SyncDomain.Settings));
    }

    [Fact]
    public async Task PullAsync_WhenDisconnected_NeverCallsSyncClient()
    {
        var settings = new FakeSettingsDocumentService();
        var syncClient = new FakeSyncClient();
        var service = CreateService(settings, syncClient, connectionState: null);

        await service.PullAsync();

        Assert.Empty(syncClient.PullCalls);
    }

    private static Guid ComputeExpectedRecordId(SettingsDocumentDefinition definition)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(definition.Key.ToString()));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static SettingsSyncService CreateService(
        FakeSettingsDocumentService settings,
        FakeSyncClient syncClient,
        ServerConnectionState? connectionState,
        InMemorySyncCursorRepository? cursorRepository = null) =>
        new(
            [AppearanceDefinition],
            settings,
            new FakeServerConnectionService(connectionState),
            syncClient,
            cursorRepository ?? new InMemorySyncCursorRepository(),
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

    private sealed class FakeSettingsDocumentService : ISettingsDocumentService
    {
        private readonly Dictionary<VirtualPath, (string Content, long Revision)> _documents = [];

        public void Seed(VirtualPath path, string content, long revision) => _documents[path] = (content, revision);

        public string? CurrentContent(VirtualPath path) => _documents.TryGetValue(path, out var doc) ? doc.Content : null;

        public ValueTask<SettingsReadResult> ReadAsync(
            VirtualPath path, AppOperationContext context, CancellationToken cancellationToken = default)
        {
            if (!_documents.TryGetValue(path, out var doc))
            {
                return ValueTask.FromResult(new SettingsReadResult(SettingsReadStatus.NotFound, null, "settings.not-found"));
            }

            return ValueTask.FromResult(new SettingsReadResult(
                SettingsReadStatus.Success, new SettingsDocumentSnapshot(path, doc.Content, "application/json", doc.Revision), null));
        }

        public ValueTask<SettingsWriteResult> WriteAsync(
            SettingsWriteRequest request, AppOperationContext context, CancellationToken cancellationToken = default)
        {
            _documents.TryGetValue(request.Path, out var existing);
            if (existing.Revision != request.ExpectedRevision)
            {
                return ValueTask.FromResult(new SettingsWriteResult(SettingsWriteStatus.Conflict, null, ["settings.revision-conflict"]));
            }

            long newRevision = request.ExpectedRevision + 1;
            _documents[request.Path] = (request.Content, newRevision);
            return ValueTask.FromResult(new SettingsWriteResult(
                SettingsWriteStatus.Success, new SettingsDocumentSnapshot(request.Path, request.Content, "application/json", newRevision), null));
        }
    }
}
