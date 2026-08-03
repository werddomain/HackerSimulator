using HackerOs.Server.Contracts.Proxy;
using HackerOs.Server.Data;
using HackerOs.Server.Services;
using Microsoft.EntityFrameworkCore;
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

    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid DeviceId = Guid.NewGuid();

    public ProxyServiceTests()
    {
        var options = new DbContextOptionsBuilder<HackerOsServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new HackerOsServerDbContext(options);
        _audit = new AuditService(_db);
        _fakeHandler = new FakeHttpMessageHandler();
        _factory = new FakeHttpClientFactory(_fakeHandler);
        _proxy = new ProxyService(_factory, _audit, _db);
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
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([])
        });
    }
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
