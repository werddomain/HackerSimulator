using HackerOs.Server.Data;
using HackerOs.Server.Endpoints;
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

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "HACKEROS_");

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
builder.Services.AddSingleton<IProxyAddressResolver, SystemProxyAddressResolver>();
builder.Services.AddSingleton<IProxyConnectionPinAccessor, ProxyConnectionPinAccessor>();

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

// ── Database migration on startup ─────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HackerOsServerDbContext>();
    await db.Database.MigrateAsync();
}

// ── Endpoint groups ───────────────────────────────────────────────────────────
app.MapVersionEndpoints();    // GET /api/version, POST /api/version/check
app.MapIdentityEndpoints();   // POST /api/account, POST /api/auth/login, ...
app.MapSyncEndpoints();       // POST /api/sync/pull, POST /api/sync/push, ...
app.MapProxyEndpoints();      // POST /api/proxy/http, GET /api/proxy/policy
app.MapAdminEndpoints();      // GET /health, GET /api/account/data-summary, ...

await app.RunAsync();
