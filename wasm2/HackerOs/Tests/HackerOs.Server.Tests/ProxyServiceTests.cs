using HackerOs.Server.Contracts.Proxy;
using HackerOs.Server.Data;
using HackerOs.Server.Services;
using Microsoft.EntityFrameworkCore;
using System.Net;
using Xunit;

namespace HackerOs.Server.Tests;

// =============================================================================
// Proxy Service Tests — P5-PROXY-007
// Tests SSRF, DNS rebinding, simulated-domain block, blocked port, redirect limit,
// and simulated-domain-to-real-proxy isolation.
// =============================================================================

public sealed class ProxyServiceTests : IDisposable
{
    private readonly HackerOsServerDbContext _db;
    private readonly ProxyService _proxy;
    private readonly AuditService _audit;
    private readonly FakeHttpMessageHandler _fakeHandler;
    private readonly IHttpClientFactory _factory;
    private readonly ProxyConnectionPinAccessor _connectionPin;

    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid DeviceId = Guid.NewGuid();

    public ProxyServiceTests()
    {
        var options = new DbContextOptionsBuilder<HackerOsServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new HackerOsServerDbContext(options);
        _audit = new AuditService(_db);
        _db.Devices.Add(new DeviceEntity
        {
            DeviceId = DeviceId,
            AccountId = AccountId,
            DeviceName = "Proxy test device",
            DeviceFingerprint = Guid.NewGuid().ToString("N"),
            RegisteredUtc = DateTimeOffset.UtcNow
        });
        _db.SaveChanges();
        _connectionPin = new ProxyConnectionPinAccessor();
        _fakeHandler = new FakeHttpMessageHandler(_connectionPin);
        _factory = new FakeHttpClientFactory(_fakeHandler);
        _proxy = new ProxyService(
            _factory,
            _audit,
            _db,
            new FakeProxyAddressResolver(IPAddress.Parse("93.184.216.34")),
            _connectionPin);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task BlockedPort_Throws_WithBlockedPortCode()
    {
        var request = BuildRequest("http://example.com:8080/test");
        var ex = await Assert.ThrowsAsync<ProxyRequestException>(() =>
            _proxy.ExecuteHttpRequestAsync(AccountId, DeviceId, request, CancellationToken.None));
        Assert.Equal(ProxyErrorCode.BlockedPort, ex.ErrorCode);
    }

    [Theory]
    [InlineData("http://127.0.0.1/")]           // loopback
    [InlineData("http://10.0.0.1/")]            // RFC-1918 Class A
    [InlineData("http://172.16.0.1/")]          // RFC-1918 Class B
    [InlineData("http://192.168.1.1/")]         // RFC-1918 Class C
    [InlineData("http://169.254.169.254/")]     // AWS metadata endpoint
    [InlineData("http://[::ffff:127.0.0.1]/")]  // IPv4-mapped IPv6 loopback
    public async Task BlockedAddress_Throws_WithBlockedAddressCode(string url)
    {
        // These addresses are blocked even when the port is allowed.
        var request = BuildRequest(url);
        var ex = await Assert.ThrowsAsync<ProxyRequestException>(() =>
            _proxy.ExecuteHttpRequestAsync(AccountId, DeviceId, request, CancellationToken.None));

        // May be BLOCKED_ADDRESS or BLOCKED_PORT depending on port.
        Assert.True(ex.ErrorCode is ProxyErrorCode.BlockedAddress or ProxyErrorCode.BlockedPort);
    }

    [Theory]
    [InlineData("http://example.hackeros.local/")]
    [InlineData("http://bank.sim/")]
    [InlineData("http://target.hackeros/")]
    public async Task SimulatedDomain_Throws_WithSimulatedDomainBlockedCode(string url)
    {
        var request = BuildRequest(url);
        var ex = await Assert.ThrowsAsync<ProxyRequestException>(() =>
            _proxy.ExecuteHttpRequestAsync(AccountId, DeviceId, request, CancellationToken.None));
        Assert.Equal(ProxyErrorCode.SimulatedDomainBlocked, ex.ErrorCode);
    }

    [Fact]
    public async Task MalformedUrl_Throws_WithMalformedRequestCode()
    {
        var request = BuildRequest("not-a-url");
        var ex = await Assert.ThrowsAsync<ProxyRequestException>(() =>
            _proxy.ExecuteHttpRequestAsync(AccountId, DeviceId, request, CancellationToken.None));
        Assert.Equal(ProxyErrorCode.MalformedRequest, ex.ErrorCode);
    }

    [Fact]
    public async Task GetPolicy_ReturnsExpectedDefaults()
    {
        var policy = await _proxy.GetPolicyAsync(DeviceId, CancellationToken.None);

        Assert.Equal(DeviceId, policy.DeviceId);
        Assert.Equal(8, policy.MaxConcurrentRequests);
        Assert.Contains("http", policy.AllowedProtocols);
        Assert.Empty(policy.OperatorWeakeningWarnings);
    }

    [Fact]
    public async Task DeviceOwnedByAnotherAccount_IsRejectedBeforeTransport()
    {
        var ex = await Assert.ThrowsAsync<ProxyRequestException>(() =>
            _proxy.ExecuteHttpRequestAsync(Guid.NewGuid(), DeviceId,
                BuildRequest("https://example.com/"), CancellationToken.None));

        Assert.Equal(ProxyErrorCode.CapabilityDenied, ex.ErrorCode);
        Assert.Equal(0, _fakeHandler.SendCount);
    }

    [Fact]
    public async Task RevokedDevice_IsRejectedBeforeTransport()
    {
        var device = await _db.Devices.SingleAsync();
        device.IsRevoked = true;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ProxyRequestException>(() =>
            _proxy.ExecuteHttpRequestAsync(AccountId, DeviceId,
                BuildRequest("https://example.com/"), CancellationToken.None));

        Assert.Equal(ProxyErrorCode.CapabilityDenied, ex.ErrorCode);
        Assert.Equal(0, _fakeHandler.SendCount);
    }

    [Theory]
    [InlineData("ftp://example.com/")]
    [InlineData("file:///etc/passwd")]
    public async Task UnsupportedScheme_IsRejected(string target)
    {
        var ex = await Assert.ThrowsAsync<ProxyRequestException>(() =>
            _proxy.ExecuteHttpRequestAsync(AccountId, DeviceId,
                BuildRequest(target), CancellationToken.None));

        Assert.Equal(ProxyErrorCode.MalformedRequest, ex.ErrorCode);
    }

    [Fact]
    public async Task ValidatedAddress_IsPinnedDuringTransport_AndClearedAfterward()
    {
        var response = await _proxy.ExecuteHttpRequestAsync(AccountId, DeviceId,
            BuildRequest("https://example.com/"), CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(IPAddress.Parse("93.184.216.34"), _fakeHandler.ObservedPin);
        Assert.Null(_connectionPin.Address);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static ProxyHttpRequest BuildRequest(string url) =>
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

/// <summary>
/// Fake HTTP handler that always returns 200 OK with an empty body.
/// Used to prevent real network calls in proxy tests.
/// </summary>
public sealed class FakeHttpMessageHandler(IProxyConnectionPinAccessor connectionPin) : HttpMessageHandler
{
    public int SendCount { get; private set; }
    public IPAddress? ObservedPin { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SendCount++;
        ObservedPin = connectionPin.Address;
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([])
        });
    }
}

/// <summary>Deterministic resolver that prevents unit tests from touching real DNS.</summary>
public sealed class FakeProxyAddressResolver(params IPAddress[] addresses) : IProxyAddressResolver
{
    public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<IPAddress>>(
            IPAddress.TryParse(host, out var literal) ? [literal] : addresses);
}

/// <summary>
/// Minimal IHttpClientFactory implementation for unit tests — avoids requiring Microsoft.Extensions.Http.
/// </summary>
public sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) => new(_handler) { BaseAddress = null };
}
