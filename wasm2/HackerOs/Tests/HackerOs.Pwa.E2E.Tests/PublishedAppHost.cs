using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace HackerOs.Pwa.E2E.Tests;

/// <summary>
/// Serves a real `dotnet publish` output of OS/HackerOs.Ecosystem as static files over
/// a Kestrel-bound TCP port, so Playwright can exercise the actual
/// <c>service-worker.published.js</c> and <c>manifest.webmanifest</c> — nothing else in
/// this repo does that; every other harness runs a dev-mode host instead (see
/// docs/phase-2-acceptance.md for the audit that found this gap).
///
/// <c>OS/HackerOs.Ecosystem/wwwroot/index.html</c> deliberately refuses to register the
/// service worker whenever <c>window.location.hostname</c> contains "localhost" or
/// "127.0.0.1" (so local dev can never silently claim offline support). Every existing
/// Playwright harness in this repo navigates to <c>127.0.0.1:{port}</c>, so the worker
/// would never register there even against published output. This host is bound to the
/// IPv6 loopback address instead: <c>http://[::1]:{port}/</c> resolves to hostname
/// literal <c>"::1"</c> in the browser, which contains neither excluded substring, while
/// still being loopback traffic (no firewall prompt, no dependency on a real LAN/NIC
/// address, works identically on a sandboxed CI runner and a dev workstation). The
/// production gate itself is never touched.
/// </summary>
internal sealed class PublishedAppHost : IAsyncDisposable
{
    private WebApplication? _app;

    public string Address { get; private set; } = string.Empty;

    public async Task StartAsync(string wwwrootPath, int port)
    {
        if (!Directory.Exists(wwwrootPath))
        {
            throw new DirectoryNotFoundException(
                $"Published output not found at '{wwwrootPath}'. Run 'dotnet publish' " +
                "OS/HackerOs.Ecosystem/HackerOs.Ecosystem.csproj before running this suite.");
        }

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://[::1]:{port}");
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        WebApplication app = builder.Build();

        FileExtensionContentTypeProvider contentTypes = new();
        contentTypes.Mappings[".blat"] = "application/octet-stream";
        contentTypes.Mappings[".dat"] = "application/octet-stream";
        contentTypes.Mappings[".webmanifest"] = "application/manifest+json";

        PhysicalFileProvider fileProvider = new(wwwrootPath);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
            ContentTypeProvider = contentTypes,
            ServeUnknownFileTypes = true,
        });

        await app.StartAsync();
        _app = app;
        Address = $"http://[::1]:{port}";
    }

    /// <summary>Simulates "server unavailable" (P2-ACC-015) without touching the browser's network stack.</summary>
    public async Task StopAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
    }

    public static int ReservePort()
    {
        TcpListener listener = new(IPAddress.IPv6Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
