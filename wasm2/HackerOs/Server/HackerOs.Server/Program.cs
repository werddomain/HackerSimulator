using Microsoft.Extensions.DependencyInjection.Extensions;
using HackerOs.Ecosystem;
using HackerOs.Platform.Blazor.Hosting;
using HackerOs.Platform.Blazor.LazyLoading;
using HackerOs.Platform.Core.ServerConnection;
using HackerOs.Server;
using HackerOs.Server.Components;
using HackerOs.Server.Data;
using HackerOs.Server.Endpoints;
using HackerOs.Server.ServerConnection;
using HackerOs.Server.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

// =============================================================================
// HackerOs Optional Server
//
// This process is entirely optional. The browser PWA continues to function
// fully when this server is not reachable. The server provides:
//   1. Sync — record-level versioned push/pull with explicit conflict resolution.
//   2. Identity — account registration, device management, token refresh.
//   3. Network relay — server-side validated HTTP requests (with their bodies), a single-port
//      TCP probe, and TCP/UDP streams over WebSockets, for apps the server grants
//      network.real.access to (ADR 0035, ADR 0102). Its policy and quotas are the
//      HackerOs:Proxy section of appsettings.json; risky settings are logged at startup.
//
// Security: The server never trusts any client-side claims. It validates
// every request against its own stored device policy and capability grants.
// =============================================================================

var builder = WebApplication.CreateBuilder(args);

// The server-hosted Blazor UI (ADR 0027) reuses AddHackerOsEcosystem, whose composition
// root registers every IJSRuntime-touching service as Scoped (per-circuit) rather than
// Singleton — Singleton was the original shape and caused a real bug: the first circuit
// to resolve one permanently captured whichever IJSRuntime existed at that moment
// (including an unattached one from the host page's own static-SSR pass), so JS interop
// failed for every later circuit too. ValidateScopes is explicitly kept on so a future
// singleton-consuming-a-scoped-service regression fails loudly instead of quietly
// reintroducing that bug. ValidateOnBuild stays off as it was before ADR 0045 removed the
// IndexedDbDiagnosticRepository registration that originally required it; turning it on is a
// separate server-composition change (ADR 0045 leaves this host's composition untouched).
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = false;
});

// ── Configuration ─────────────────────────────────────────────────────────────
// WebApplication.CreateBuilder already loads appsettings.json, the environment
// variant, standard environment variables, and command-line arguments. Add only
// the documented HACKEROS_ override layer so deployments can use scoped variables
// without re-adding JSON sources after host/test configuration.
builder.Configuration.AddEnvironmentVariables(prefix: "HACKEROS_");

// ── Database ──────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("HackerOsDb")
    ?? "Data Source=hackeros.db";
builder.Services.AddDbContext<HackerOsServerDbContext>(options =>
    options.UseSqlite(connectionString));

// ── Core server services ──────────────────────────────────────────────────────
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<IPasswordHashService, Pbkdf2PasswordHashService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ISyncService, SyncService>();
// ADR 0101: tombstones are kept 90 days, then purged by a daily job.
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddHostedService<SyncTombstonePurgeService>();
builder.Services.AddScoped<IProxyService, ProxyService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IContentBlobService, ContentBlobService>();
builder.Services.AddSingleton<IServerDatabaseBackupService, ServerDatabaseBackupService>();
builder.Services.AddSingleton<IProxyAddressResolver, SystemProxyAddressResolver>();
builder.Services.AddSingleton<IProxyConnectionPinAccessor, ProxyConnectionPinAccessor>();
builder.Services.AddSingleton<IProxyTcpConnector, SocketProxyTcpConnector>();
builder.Services.AddSingleton<IProxyStreamConnector, SocketProxyStreamConnector>();
builder.Services.AddScoped<IProxyGrantAuthority, ProxyGrantAuthority>();

// ── Network relay policy (ADR 0102 §5) ────────────────────────────────────────
// HackerOs:Proxy is read once at startup; ProxyRelayPolicy fills the defaults the binder cannot
// (it appends to lists initialized in code), and ProxyPolicyStartupReporter logs a warning per
// setting that weakens them.
builder.Services.AddOptions<ProxyOptions>().Bind(builder.Configuration.GetSection(ProxyOptions.SectionName));
builder.Services.AddSingleton(provider =>
    ProxyRelayPolicy.From(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProxyOptions>>().Value));
builder.Services.AddHostedService<ProxyPolicyStartupReporter>();

// ── HTTP client for proxy outbound requests ───────────────────────────────────
// The proxy service uses a named client with strict socket timeouts.
builder.Services.AddHttpClient("proxy", client =>
{
    client.Timeout = TimeSpan.FromSeconds(35); // slightly > max proxy duration
})
.ConfigurePrimaryHttpMessageHandler(services => new SocketsHttpHandler
{
    // Never follow redirects automatically — the proxy service handles them manually
    // so it can enforce the server-side redirect limit and DNS rebinding check.
    AllowAutoRedirect = false,
    ConnectTimeout = TimeSpan.FromSeconds(10),
    // Disable automatic decompression to pass content hashes correctly.
    AutomaticDecompression = System.Net.DecompressionMethods.None,
    ConnectCallback = async (context, cancellationToken) =>
    {
        var pin = services.GetRequiredService<IProxyConnectionPinAccessor>().Address
            ?? throw new InvalidOperationException("A proxy connection was attempted without a validated address pin.");
        var socket = new System.Net.Sockets.Socket(pin.AddressFamily, System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(new System.Net.IPEndPoint(pin, context.DnsEndPoint.Port), cancellationToken);
            return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
});

// ── Authentication — Bearer token ─────────────────────────────────────────────
// Token validation uses the server's own token store, not a JWT public key.
// This avoids shipping a signing secret to the client.
builder.Services.AddAuthentication("HackerOsBearer")
    .AddScheme<HackerOsBearerOptions, HackerOsBearerHandler>("HackerOsBearer", _ => { });
builder.Services.AddAuthorization();

// ── CORS — allow the PWA origin ───────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .AllowCredentials());
});

// ── OpenAPI (Scalar UI) ───────────────────────────────────────────────────────
builder.Services.AddOpenApi();

// ── Server-hosted Blazor UI (ADR 0027) ────────────────────────────────────────
// Serves the same HackerOs.Ecosystem.App component tree as OS/HackerOs.Ecosystem
// (static WASM) and test/test (WASM debug harness) via Interactive Server render
// mode. Single-tenant/single-active-circuit only for this phase — see ADR 0027.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment());
// SignalR's default 32KB message cap is sized for typical form-post/DOM-diff traffic. This
// app's initial interactive render (desktop shell + taskbar + app launcher, backed by the full
// app catalog/manifest set) produces a render batch larger than that on first connect, so the
// circuit was closing immediately with "The maximum message size of 32768B was exceeded" before
// the desktop ever appeared. WASM hosts never hit this (no SignalR transport involved).
builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024;
});
builder.Services.AddSingleton<IEcosystemHostEnvironment, AspNetCoreEcosystemHostEnvironment>();
builder.Services.AddSingleton<IBuildKnownAssemblyTransport, InProcessAssemblyTransport>();
builder.Services.AddSingleton(provider => new BuildKnownAssemblyLoaderRegistry(
    BuildKnownLazyAssemblies.Names,
    provider.GetRequiredService<IBuildKnownAssemblyTransport>()));
builder.Services.AddSingleton(provider => new BuildKnownLazyAppDescriptorRegistry(
    BuildKnownLazyApps.Catalog,
    provider.GetRequiredService<BuildKnownAssemblyLoaderRegistry>()));

// Direct-injection IAccountClient/IProxyClient/ISyncClient (ADR 0036) — registered before
// AddHackerOsEcosystem below so its TryAddSingleton calls find these already present and skip
// the Http*Client defaults. Calls IAccountService/ISyncService/IProxyService in-process instead
// of looping back through this same process's own HTTP API.
builder.Services.AddSingleton<IAccountClient, DirectAccountClient>();
builder.Services.AddSingleton<IProxyClient, DirectProxyClient>();
builder.Services.AddSingleton<ISyncClient, DirectSyncClient>();

// ADR 0066: optional measured CPU/memory, decided once from HackerOs:ResourceAccounting in
// appsettings.json and what the build compiled in. Both are off by default on this host: several
// threads run app code at once under Interactive Server (ADR 0067, section 6).
builder.Services.AddHackerOsResourceAccounting(builder.Configuration);

int serviceCountBeforeEcosystem = builder.Services.Count;
builder.Services.AddHackerOsEcosystem(
    BuildKnownLazyApps.Catalog,
    provider => provider.GetRequiredService<BuildKnownLazyAppDescriptorRegistry>().Descriptors,
    provider => provider.GetRequiredService<BuildKnownLazyAppDescriptorRegistry>());

// AddHackerOsEcosystem registers HackerOsDiagnosticLoggerProvider as ILoggerProvider. It was
// originally stripped here because it construct-injected the IJSRuntime-backed IndexedDB
// diagnostic repository, which ASP.NET Core cannot build before any circuit exists. Since ADR
// 0045 it only writes to /var/log through LogFileWriter (all singletons), but this API host never
// attaches a filesystem to that writer, so the bridge stays removed: ADR 0045 leaves the server
// composition as it was. Removing only the descriptor AddHackerOsEcosystem just added (not any
// framework-registered ILoggerProvider) keeps the diagnostic sink itself intact.
for (int i = builder.Services.Count - 1; i >= serviceCountBeforeEcosystem; i--)
{
    if (builder.Services[i].ServiceType == typeof(ILoggerProvider))
    {
        builder.Services.RemoveAt(i);
    }
}

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors();
// TCP/UDP relay streams (ADR 0102 §2, §3) are WebSockets; ping the client so an idle stream's
// carrier is not cut by intermediaries before the relay's own idle timeout.
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

// ── Database migration on startup ─────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HackerOsServerDbContext>();
    // The server currently ships its model without generated EF migrations.
    // MigrateAsync is a no-op in that state, so a fresh SQLite file would look
    // connectable while containing no application tables. Use EnsureCreated for
    // this migration-free bootstrap; once migrations are introduced, the normal
    // migration path becomes authoritative.
    string[] migrations = db.Database.GetMigrations().ToArray();
    if (migrations.Length == 0)
    {
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }
}

// ── Endpoint groups ───────────────────────────────────────────────────────────
app.MapVersionEndpoints();    // GET /api/version, POST /api/version/check
app.MapIdentityEndpoints();   // POST /api/account, POST /api/auth/login, ...
app.MapSyncEndpoints();       // POST /api/sync/pull, POST /api/sync/push, ...
app.MapProxyEndpoints();      // POST /api/proxy/http, /tcp-probe; WS /api/proxy/tcp, /udp; GET /api/proxy/policy
app.MapAdminEndpoints();      // GET /health, GET /api/account/data-summary, ...

// ── Server-hosted Blazor UI (ADR 0027) ────────────────────────────────────────
app.MapRazorComponents<HackerOs.Server.Components.App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(HackerOs.Ecosystem.App).Assembly,
        typeof(HackerOs.Platform.Blazor.Shell.DesktopShell).Assembly);

await app.RunAsync();

/// <summary>
/// Public application entry point marker used by the server integration tests.
/// The runtime behavior remains defined by the top-level program above.
/// </summary>
public partial class Program;
