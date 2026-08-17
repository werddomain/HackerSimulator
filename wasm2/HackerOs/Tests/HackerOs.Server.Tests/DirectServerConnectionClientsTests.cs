using System.Net;
using HackerOs.Platform.Core.ServerConnection;
using HackerOs.Server.Contracts.Identity;
using HackerOs.Server.Contracts.Proxy;
using HackerOs.Server.Contracts.Sync;
using HackerOs.Server.Data;
using HackerOs.Server.ServerConnection;
using HackerOs.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HackerOs.Server.Tests;

// =============================================================================
// Direct-Injection Client Tests — ADR 0036 / Pass N+5
//
// Wires the real AccountService/SyncService/ProxyService (Scoped, EF-backed) behind
// a real ServiceCollection/IServiceScopeFactory, exactly matching how
// Server/HackerOs.Server/Program.cs wires them in production. This is deliberately
// not a fake/mock of the underlying services — the point of these tests is proving
// the *composition* is correct (a Singleton client resolving a Scoped, DbContext-backed
// service through a fresh per-call scope, with no captive-dependency reuse), which a
// faked IAccountService/ISyncService/IProxyService would not exercise.
// =============================================================================

public sealed class DirectServerConnectionClientsTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly FakeHttpMessageHandler _fakeHandler;

    public DirectServerConnectionClientsTests()
    {
        string dbName = Guid.NewGuid().ToString();
        ServiceCollection services = new();

        services.AddDbContext<HackerOsServerDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordHashService, Pbkdf2PasswordHashService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IProxyService, ProxyService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<IProxyConnectionPinAccessor, ProxyConnectionPinAccessor>();
        services.AddSingleton<IProxyAddressResolver>(_ => new FakeProxyAddressResolver(IPAddress.Parse("93.184.216.34")));
        services.AddSingleton<IProxyTcpConnector, FakeProxyTcpConnector>();

        _fakeHandler = new FakeHttpMessageHandler(new ProxyConnectionPinAccessor());
        services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(_fakeHandler));

        services.AddSingleton<IAccountClient, DirectAccountClient>();
        services.AddSingleton<ISyncClient, DirectSyncClient>();
        services.AddSingleton<IProxyClient, DirectProxyClient>();

        _provider = services.BuildServiceProvider();
    }

    public void Dispose() => _provider.Dispose();

    // ── DirectAccountClient ──────────────────────────────────────────────────

    [Fact]
    public async Task DirectAccountClient_CreateAccount_Succeeds()
    {
        var client = _provider.GetRequiredService<IAccountClient>();

        CreateAccountResponse response = await client.CreateAccountAsync(
            new Uri("https://ignored.example"),
            new CreateAccountRequest("alice", "hash123", "salt456", "Alice's PC", "fp-001"),
            CancellationToken.None);

        Assert.Equal("alice", response.Username);
        Assert.NotEqual(Guid.Empty, response.AccountId);
    }

    [Fact]
    public async Task DirectAccountClient_DuplicateUsername_ThrowsServerConnectionException_NotInvalidOperationException()
    {
        var client = _provider.GetRequiredService<IAccountClient>();
        var request = new CreateAccountRequest("bob", "h", "s", "Bob's Laptop", "fp-002");
        await client.CreateAccountAsync(new Uri("https://ignored.example"), request, CancellationToken.None);

        // Callers across the codebase only ever catch ServerConnectionException -- the raw
        // InvalidOperationException AccountService throws must never leak through the direct client.
        var ex = await Assert.ThrowsAsync<ServerConnectionException>(() =>
            client.CreateAccountAsync(new Uri("https://ignored.example"), request, CancellationToken.None));
        Assert.Contains("USERNAME_TAKEN", ex.Message);
    }

    [Fact]
    public async Task DirectAccountClient_Login_WrongPassword_ThrowsServerConnectionException()
    {
        var client = _provider.GetRequiredService<IAccountClient>();
        await client.CreateAccountAsync(
            new Uri("https://ignored.example"),
            new CreateAccountRequest("carol", "correct-hash", "salt", "Carol's PC", "fp-003"),
            CancellationToken.None);

        await Assert.ThrowsAsync<ServerConnectionException>(() =>
            client.LoginAsync(
                new Uri("https://ignored.example"),
                new LoginRequest("carol", "wrong-hash", "fp-003"),
                CancellationToken.None));
    }

    // ── DirectProxyClient ─────────────────────────────────────────────────────

    [Fact]
    public async Task DirectProxyClient_InvalidToken_ThrowsServerConnectionException()
    {
        var client = _provider.GetRequiredService<IProxyClient>();

        await Assert.ThrowsAsync<ServerConnectionException>(() =>
            client.ExecuteHttpRequestAsync(
                new Uri("https://ignored.example"),
                "not-a-real-token",
                BuildProxyRequest("https://example.com/"),
                CancellationToken.None));

        // Token validation must fail before ever reaching the transport.
        Assert.Equal(0, _fakeHandler.SendCount);
    }

    [Fact]
    public async Task DirectProxyClient_ValidToken_ForwardsToProxyService_AndSucceeds()
    {
        (Guid accountId, Guid deviceId, string accessToken) = await RegisterDeviceAndIssueTokenAsync();
        var client = _provider.GetRequiredService<IProxyClient>();

        ProxyHttpResponse response = await client.ExecuteHttpRequestAsync(
            new Uri("https://ignored.example"), accessToken, BuildProxyRequest("https://example.com/"), CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1, _fakeHandler.SendCount);
    }

    [Fact]
    public async Task DirectProxyClient_BlockedPort_ThrowsServerConnectionException_NotProxyRequestException()
    {
        (Guid accountId, Guid deviceId, string accessToken) = await RegisterDeviceAndIssueTokenAsync();
        var client = _provider.GetRequiredService<IProxyClient>();

        // Callers (CurlCommand, NmapCommand, CatCommand) only catch ServerConnectionException or
        // HttpRequestException -- an unwrapped ProxyRequestException would surface as a confusing
        // EntryPointFault instead of a clean "could not resolve host" message.
        var ex = await Assert.ThrowsAsync<ServerConnectionException>(() =>
            client.ExecuteHttpRequestAsync(
                new Uri("https://ignored.example"), accessToken, BuildProxyRequest("http://example.com:8080/"), CancellationToken.None));
        Assert.Contains("BLOCKED_PORT", ex.Message);
    }

    [Fact]
    public async Task DirectProxyClient_TwoSequentialCalls_BothSucceed_ProvingNoCaptiveDbContextReuse()
    {
        // If DirectProxyClient captured a Scoped IProxyService (and its DbContext) at construction
        // instead of resolving a fresh one per call, a second call after the first scope would have
        // already been disposed elsewhere would misbehave. Two clean successes back to back is the
        // regression signal for that class of bug.
        (Guid accountId, Guid deviceId, string accessToken) = await RegisterDeviceAndIssueTokenAsync();
        var client = _provider.GetRequiredService<IProxyClient>();

        ProxyHttpResponse first = await client.ExecuteHttpRequestAsync(
            new Uri("https://ignored.example"), accessToken, BuildProxyRequest("https://example.com/a"), CancellationToken.None);
        ProxyHttpResponse second = await client.ExecuteHttpRequestAsync(
            new Uri("https://ignored.example"), accessToken, BuildProxyRequest("https://example.com/b"), CancellationToken.None);

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(2, _fakeHandler.SendCount);
    }

    // ── DirectSyncClient ──────────────────────────────────────────────────────

    [Fact]
    public async Task DirectSyncClient_InvalidToken_ThrowsServerConnectionException()
    {
        var client = _provider.GetRequiredService<ISyncClient>();

        await Assert.ThrowsAsync<ServerConnectionException>(() =>
            client.PullAsync(
                new Uri("https://ignored.example"),
                "not-a-real-token",
                new PullRequest("filesystem", null, 100),
                CancellationToken.None));
    }

    [Fact]
    public async Task DirectSyncClient_ValidToken_UnknownDomain_ThrowsServerConnectionException_NotArgumentException()
    {
        (Guid accountId, Guid deviceId, string accessToken) = await RegisterDeviceAndIssueTokenAsync();
        var client = _provider.GetRequiredService<ISyncClient>();

        var ex = await Assert.ThrowsAsync<ServerConnectionException>(() =>
            client.PullAsync(
                new Uri("https://ignored.example"),
                accessToken,
                new PullRequest("not-a-real-domain", null, 100),
                CancellationToken.None));
        Assert.Contains("Unknown sync domain", ex.Message);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(Guid AccountId, Guid DeviceId, string AccessToken)> RegisterDeviceAndIssueTokenAsync()
    {
        Guid accountId = Guid.NewGuid();
        Guid deviceId = Guid.NewGuid();

        using (IServiceScope scope = _provider.CreateScope())
        {
            HackerOsServerDbContext db = scope.ServiceProvider.GetRequiredService<HackerOsServerDbContext>();
            db.Devices.Add(new DeviceEntity
            {
                DeviceId = deviceId,
                AccountId = accountId,
                DeviceName = "Direct client test device",
                DeviceFingerprint = Guid.NewGuid().ToString("N"),
                RegisteredUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        ITokenService tokens = _provider.GetRequiredService<ITokenService>();
        (string accessToken, _) = await tokens.IssueAccessTokenAsync(accountId, deviceId, CancellationToken.None);
        return (accountId, deviceId, accessToken);
    }

    private static ProxyHttpRequest BuildProxyRequest(string url) =>
        new(
            RequestId: Guid.NewGuid(),
            Protocol: ProxyProtocol.Http,
            TargetUrl: url,
            HttpMethod: "GET",
            Headers: [],
            BodyHash: null,
            BodyBytes: 0,
            TimeoutSeconds: 10,
            AppId: "org.hackeros.test");
}
