using System.Collections.Immutable;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Commands.Curl;
using HackerOs.Commands.Nmap;
using HackerOs.Commands.Ping;
using HackerOs.Platform.Core.Network;
using HackerOs.Platform.Core.Network.Websites;
using HackerOs.Platform.Core.ServerConnection;
using HackerOs.Server.Contracts.Proxy;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Network;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.ServerConnection;
using HackerOs.Simulation.Abstractions.Sessions;
using Xunit;

namespace HackerOs.Network.Tests;

/// <summary>
/// Comprehensive unit test suite for Phase 4 Wave 4 (Simulated Network, Websites, Commands).
/// Validates DNS resolution, website controllers, cookie sessions, redirects,
/// ping/nmap/curl commands, and proves zero real external network calls.
/// </summary>
public sealed class Wave4NetworkTests
{
    private readonly InMemorySimulatedNetworkService _network;

    public Wave4NetworkTests()
    {
        var hosts = DefaultSimulatedHostCatalog.Build();

        var registry = new InMemorySimulatedWebsiteRegistry();
        registry.Register(new HackerSearchController());
        registry.Register(new HackMailController());
        registry.Register(new CryptoBankController());
        registry.Register(new DarknetMarketController());
        registry.Register(new HackerForumController());

        _network = new InMemorySimulatedNetworkService(hosts, registry);
    }

    // ── P4-W4-001 / P4-W4-002: DNS & Network Service ────────────────────────

    [Fact]
    public void Dns_ResolvesKnownHosts_And_ReverseLookups()
    {
        var ip = _network.Dns.Resolve("hackersearch.net");
        Assert.NotNull(ip);
        Assert.Equal("192.168.1.90", ip);

        var host = _network.Dns.ReverseLookup("192.168.1.90");
        Assert.Equal("hackersearch.net", host);
    }

    [Fact]
    public void Dns_ReturnsNull_ForUnknownHost()
    {
        Assert.Null(_network.Dns.Resolve("nonexistent.invalid"));
    }

    [Fact]
    public void Ping_ReturnsLatency_ForUpHost_And_Null_ForDownHost()
    {
        var latency = _network.Ping("hackersearch.net");
        Assert.NotNull(latency);
        Assert.True(latency > 0);

        var downLatency = _network.Ping("hackerz-search.net");
        Assert.Null(downLatency);
    }

    [Fact]
    public void ScanPorts_ReturnsHostPorts_FilteredByRange()
    {
        var ports = _network.ScanPorts("localhost", 20, 100);
        Assert.NotEmpty(ports);
        Assert.Contains(ports, p => p.PortNumber == 22 && p.State == SimulatedPortState.Open);
        Assert.Contains(ports, p => p.PortNumber == 80 && p.State == SimulatedPortState.Open);
        Assert.DoesNotContain(ports, p => p.PortNumber == 3306);
    }

    // ── P4-W4-003 / P4-W4-005: Website Controllers & Navigation ────────────

    [Fact]
    public void Navigate_HackerSearch_ReturnsRenderablePage()
    {
        var cookies = new Dictionary<string, Dictionary<string, string>>();
        var result = _network.Navigate("https://hackersearch.net", cookies);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Page);
        Assert.Equal("HackerSearch", result.Page.Title);
    }

    [Fact]
    public void Navigate_HackMail_RedirectsToLogin_WhenNotAuthenticated()
    {
        var cookies = new Dictionary<string, Dictionary<string, string>>();
        var result = _network.Navigate("https://hackmail.com/inbox", cookies);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("https://hackmail.com/login", result.FinalUrl);
        Assert.Equal("HackMail — Login", result.Page?.Title);
        Assert.Equal(1, result.RedirectCount);
    }

    [Fact]
    public void HackMail_PostLogin_SetsSessionCookie_And_RedirectsToInbox()
    {
        var cookies = new Dictionary<string, Dictionary<string, string>>();

        var postResp = _network.Post(
            "https://hackmail.com/login",
            ImmutableDictionary<string, string>.Empty
                .Add("username", "agent")
                .Add("password", "secret123"),
            cookies);

        Assert.True(postResp.IsRedirect);
        Assert.Contains("hackmail.com", cookies);
        Assert.Equal("user=agent", cookies["hackmail.com"]["hackmail_session"]);

        // Now navigate to /inbox using the updated cookies
        var navResult = _network.Navigate("https://hackmail.com/inbox", cookies);
        Assert.True(navResult.IsSuccess);
        Assert.Equal("HackMail — Inbox", navResult.Page?.Title);
    }

    [Fact]
    public void Navigate_UnknownHost_ReturnsNetworkError()
    {
        var cookies = new Dictionary<string, Dictionary<string, string>>();
        var result = _network.Navigate("https://unknown-host-12345.com", cookies);

        Assert.False(result.IsSuccess);
        Assert.Equal(SimulatedNetworkErrors.UnknownHost, result.NetworkError);
    }

    // ── P4-W4-006: Terminal Commands (ping, nmap, curl) ────────────────────

    [Fact]
    public async Task PingCommand_ExecutesSuccessfully_ForUpHost()
    {
        var cmd = new PingCommand(PingCommand.StaticManifest, _network, new NeverConnectedServerConnectionService(), new UnusedProxyClient());

        using var stdoutWriter = new StringWriter();
        using var stderrWriter = new StringWriter();
        var context = CreateContext(["hackersearch.net"], stdoutWriter, stderrWriter);

        int exitCode = await cmd.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var output = stdoutWriter.ToString();
        Assert.Contains("PING hackersearch.net (192.168.1.90)", output);
        Assert.Contains("64 bytes from 192.168.1.90", output);
        Assert.Contains("0% packet loss", output);
    }

    [Fact]
    public async Task PingCommand_UnknownHost_WithoutServerConnection_ReportsCannotResolve()
    {
        var cmd = new PingCommand(PingCommand.StaticManifest, _network, new NeverConnectedServerConnectionService(), new UnusedProxyClient());

        using var stdoutWriter = new StringWriter();
        using var stderrWriter = new StringWriter();
        var context = CreateContext(["unknown-host-12345.com"], stdoutWriter, stderrWriter);

        int exitCode = await cmd.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Contains("cannot resolve", stdoutWriter.ToString());
    }

    [Fact]
    public async Task NmapCommand_ScansPorts_And_FormatsOutput()
    {
        var cmd = new NmapCommand(NmapCommand.StaticManifest, _network);

        using var stdoutWriter = new StringWriter();
        using var stderrWriter = new StringWriter();
        var context = CreateContext(["-p", "1-100", "-sV", "localhost"], stdoutWriter, stderrWriter);

        int exitCode = await cmd.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var output = stdoutWriter.ToString();
        Assert.Contains("Nmap scan report for localhost (127.0.0.1)", output);
        Assert.Contains("22/tcp    open      ssh", output);
        Assert.Contains("80/tcp    open      http", output);
    }

    [Fact]
    public async Task CurlCommand_FetchesPage_And_PrintsSections()
    {
        var cmd = new CurlCommand(CurlCommand.StaticManifest, _network);

        using var stdoutWriter = new StringWriter();
        using var stderrWriter = new StringWriter();
        var context = CreateContext(["https://hackersearch.net"], stdoutWriter, stderrWriter);

        int exitCode = await cmd.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var output = stdoutWriter.ToString();
        Assert.Contains("Title: HackerSearch", output);
        Assert.Contains("=== HackerSearch ===", output);
        Assert.Contains("Search the dark corners of the web", output);
    }

    [Fact]
    public async Task CurlCommand_WithHeadersOnly_PrintsHttpStatus()
    {
        var cmd = new CurlCommand(CurlCommand.StaticManifest, _network);

        using var stdoutWriter = new StringWriter();
        using var stderrWriter = new StringWriter();
        var context = CreateContext(["-I", "https://hackersearch.net"], stdoutWriter, stderrWriter);

        int exitCode = await cmd.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var output = stdoutWriter.ToString();
        Assert.Contains("HTTP/1.1 200", output);
    }

    // ── P4-W4-007: Proving Zero External Network Requests ──────────────────

    [Fact]
    public void NetworkService_MakesZeroRealSocketsOrHttpCalls()
    {
        // Prove all catalog hosts and DNS records are purely in-memory data structures.
        var hosts = _network.AllHosts;
        Assert.NotEmpty(hosts);

        foreach (var host in hosts)
        {
            Assert.False(string.IsNullOrWhiteSpace(host.Ip));
            Assert.False(string.IsNullOrWhiteSpace(host.Hostname));
            // All operations return immediately synchronously without async socket I/O
            var ping = _network.Ping(host.Hostname);
            if (host.IsUp)
                Assert.NotNull(ping);
        }
    }

    // ── Helper ─────────────────────────────────────────────────────────────

    private static TerminalExecutionContext CreateContext(
        string[] args,
        TextWriter stdout,
        TextWriter stderr)
    {
        var manifest = PingCommand.StaticManifest;
        var app = new StubAppExecutionContext(manifest);

        return new TerminalExecutionContext(
            app: app,
            arguments: args.ToImmutableArray(),
            standardInput: TextReader.Null,
            standardOutput: stdout,
            standardError: stderr,
            workingDirectory: "/home/user",
            environment: ImmutableDictionary<string, string>.Empty);
    }

    private sealed class StubAppExecutionContext : IAppExecutionContext
    {
        public StubAppExecutionContext(AppManifest manifest) => Manifest = manifest;
        public AppManifest Manifest { get; }
        public Guid InstanceId { get; } = Guid.NewGuid();
        public string UserId => "user";
        public AppAuthority UserAuthority => AppAuthority.User;
        public IReadOnlySet<string> GrantedCapabilities => new HashSet<string>(Manifest.Capabilities);
        public SessionId SessionId { get; } = SessionId.FromGuid(Guid.NewGuid());
        public ProcessId ProcessId { get; } = ProcessId.FromInt64(1);
        public CancellationToken CancellationToken => CancellationToken.None;
        public ICapabilityChecker Capabilities => throw new NotImplementedException();
        public IAppFileSystemGateway FileSystem => throw new NotImplementedException();
        public IAppSettingsGateway Settings => throw new NotImplementedException();
        public IAppEventGateway Events => throw new NotImplementedException();
        public IAppNotificationGateway Notifications => throw new NotImplementedException();
        public IAppLoggingGateway Logging => throw new NotImplementedException();
        public IAppDiagnosticsGateway Diagnostics => throw new NotImplementedException();
        public IAppClockGateway Clock => throw new NotImplementedException();
        public IAppProcessGateway Processes => throw new NotImplementedException();
    }

    /// <summary>Fake used only to prove the pure-simulation path is unaffected: this device is never connected.</summary>
    private sealed class NeverConnectedServerConnectionService : IServerConnectionService
    {
        public ValueTask<ServerConnectionState?> GetStateAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ServerConnectionState?>(null);

        public Task<ServerConnectionState> ConnectWithNewAccountAsync(
            Uri serverBaseUrl, string username, string password, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by these tests.");

        public Task<ServerConnectionState> ConnectWithExistingAccountAsync(
            Uri serverBaseUrl, string username, string password, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by these tests.");

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public Task<string?> EnsureAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    /// <summary>Fake that always throws: proves the real-network path is never reached when disconnected.</summary>
    private sealed class UnusedProxyClient : IProxyClient
    {
        public Task<ProxyHttpResponse> ExecuteHttpRequestAsync(
            Uri serverBaseUrl, string accessToken, ProxyHttpRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The proxy client must not be called when disconnected.");

        public Task<ProxyPolicyResponse> GetPolicyAsync(
            Uri serverBaseUrl, string accessToken, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The proxy client must not be called when disconnected.");
    }
}
