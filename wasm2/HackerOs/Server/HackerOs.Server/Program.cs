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
//   3. Proxy — server-side validated HTTP/TCP/UDP proxy for authorized apps.
//
// Security: The server never trusts any client-side claims. It validates
// every request against its own stored device policy and capability grants.
// =============================================================================

var builder = WebApplication.CreateBuilder(args);

// The server-hosted Blazor UI (ADR 0027) reuses AddHackerOsEcosystem unmodified,
// whose composition root registers browser-storage repositories as process-wide
// singletons that construct-inject the circuit-scoped IJSRuntime — the same
// single-tenant captive-dependency shape test/test already accepts for the same
// composition root. Default scope validation would reject that at Build() time;
// disabling it here mirrors test/test/Program.cs exactly.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = false;
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
builder.Services.AddScoped<IProxyService, ProxyService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IContentBlobService, ContentBlobService>();
builder.Services.AddSingleton<IServerDatabaseBackupService, ServerDatabaseBackupService>();
builder.Services.AddSingleton<IProxyAddressResolver, SystemProxyAddressResolver>();
builder.Services.AddSingleton<IProxyConnectionPinAccessor, ProxyConnectionPinAccessor>();
builder.Services.AddSingleton<IProxyTcpConnector, SocketProxyTcpConnector>();

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
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
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

int serviceCountBeforeEcosystem = builder.Services.Count;
builder.Services.AddHackerOsEcosystem(
    BuildKnownLazyApps.Catalog,
    provider => provider.GetRequiredService<BuildKnownLazyAppDescriptorRegistry>().Descriptors,
    provider => provider.GetRequiredService<BuildKnownLazyAppDescriptorRegistry>());

// AddHackerOsEcosystem registers HackerOsDiagnosticLoggerProvider as ILoggerProvider. That
// provider construct-injects the browser-storage diagnostic repository, which construct-injects
// IJSRuntime — safe in WASM hosts (IJSRuntime is available immediately, no circuit concept) but
// not here: ASP.NET Core eagerly constructs every registered ILoggerProvider while building the
// host's ILoggerFactory, before any Blazor Server circuit exists, and IJSRuntime has no live
// circuit to attach to that early. This is the single-tenant captive-dependency limitation ADR
// 0027 documents, surfacing at its worst — a startup hang rather than a leaked-instance quirk.
// Removing only the descriptor AddHackerOsEcosystem just added (not any framework-registered
// ILoggerProvider) keeps the diagnostic sink itself intact; only its ILoggerFactory bridge is
// skipped for this host. IndexedDb*-backed singletons are unaffected — they stay lazy and
// resolve correctly on first real circuit use, exactly as intended for single-tenant hosting.
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
app.MapProxyEndpoints();      // POST /api/proxy/http, GET /api/proxy/policy
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
