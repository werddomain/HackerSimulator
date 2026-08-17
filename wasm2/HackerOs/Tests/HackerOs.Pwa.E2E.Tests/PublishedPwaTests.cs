using System.Text.Json;
using Microsoft.Playwright;

namespace HackerOs.Pwa.E2E.Tests;

/// <summary>
/// Exercises the real published Release output of OS/HackerOs.Ecosystem — the exact static
/// site <c>deploy-wasm.yml</c> publishes to GitHub Pages — against the actual
/// <c>service-worker.published.js</c> and <c>manifest.webmanifest</c>. This is the matrix
/// described by <c>docs/integration-audit-remediation.md</c> Wave C / <c>P2-PWA-007</c>:
/// first online visit, installability, service-worker activation, server-unavailable
/// reload, real browser-offline reload, file/settings persistence, update-waiting/safe
/// activation, and release-asset consistency (P2-ACC-015 / P2-ACC-016).
///
/// Browser-support policy: Chromium only (same as every other Playwright suite in this
/// repo, launched with <c>Channel = "chrome"</c>). Firefox/Safari are not covered by
/// automated or manual evidence.
/// </summary>
[Collection(PwaPublishCollection.Name)]
public sealed class PublishedPwaTests(PwaPublishFixture fixture)
{
    [Fact]
    public async Task First_online_visit_registers_and_activates_the_service_worker_with_a_populated_cache()
    {
        int port = PublishedAppHost.ReservePort();
        await using PublishedAppHost host = new();
        await host.StartAsync(fixture.V1WwwrootPath, port);

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true });
        IPage page = await browser.NewPageAsync();
        List<string> failures = PwaTestSupport.AttachDiagnostics(page);

        await PwaTestSupport.NavigateWhenReadyAsync(page, host.Address);

        // Installability: a real <link rel="manifest"> pointing at a valid, standalone-display manifest.
        string? manifestHref = await page.EvaluateAsync<string?>(
            "() => document.querySelector('link[rel=manifest]')?.getAttribute('href')");
        Assert.Equal("manifest.webmanifest", manifestHref);
        IAPIResponse manifestResponse = await page.APIRequest.GetAsync($"{host.Address}/manifest.webmanifest");
        Assert.True(manifestResponse.Ok);
        using JsonDocument manifest = JsonDocument.Parse(await manifestResponse.TextAsync());
        Assert.Equal("standalone", manifest.RootElement.GetProperty("display").GetString());

        // WaitForServiceWorkerControllerAsync absorbs index.html's self-reload (controllerchange
        // fires even for the first null->controller transition). That reload legitimately aborts
        // whatever the abandoned pre-reload document had in flight (e.g. a lazily-imported JS
        // interop module) with net::ERR_ABORTED — collateral of a real, already-documented
        // behavior, not a defect in the reloaded page this test actually evaluates below.
        await PwaTestSupport.WaitForServiceWorkerControllerAsync(page);
        failures.Clear();

        string version = await PwaTestSupport.GetAssetsManifestVersionAsync(page);
        string[] cacheKeys = await PwaTestSupport.GetCacheKeysAsync(page);
        Assert.Equal([$"hackeros-cache-{version}"], cacheKeys);

        Assert.Empty(failures);
    }

    [Fact]
    public async Task Reload_after_the_server_stops_still_serves_the_app_and_committed_profile_from_cache()
    {
        int port = PublishedAppHost.ReservePort();
        PublishedAppHost host = new();
        try
        {
            await host.StartAsync(fixture.V1WwwrootPath, port);

            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true });
            IPage page = await browser.NewPageAsync();
            PwaTestSupport.AttachDiagnostics(page);

            await PwaTestSupport.NavigateWhenReadyAsync(page, host.Address);
            await PwaTestSupport.WaitForServiceWorkerControllerAsync(page);
            await PwaTestSupport.CreateLocalProfileAndReachDesktopAsync(page);

            // P2-ACC-015 (server unavailable): stop the origin entirely, then reload.
            await host.StopAsync();
            await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            // The cached shell booted and the committed local profile was read back from
            // IndexedDB even though the origin server is down: it lands on Sign In (not the
            // onboarding form, which only appears with zero existing profiles), and signing
            // back in reaches the desktop with no network involved.
            await PwaTestSupport.SignInAsync(page);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Reload_fully_offline_boots_the_app_and_preserves_a_saved_file()
    {
        int port = PublishedAppHost.ReservePort();
        await using PublishedAppHost host = new();
        await host.StartAsync(fixture.V1WwwrootPath, port);

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true });
        await using IBrowserContext context = await browser.NewContextAsync();
        IPage page = await context.NewPageAsync();
        await PwaTestSupport.InstallBlobCaptureAsync(page);
        PwaTestSupport.AttachDiagnostics(page);

        await PwaTestSupport.NavigateWhenReadyAsync(page, host.Address);
        await PwaTestSupport.WaitForServiceWorkerControllerAsync(page);
        await PwaTestSupport.CreateLocalProfileAndReachDesktopAsync(page);

        const string content = "Written while online, read back offline.\n";
        string directory = $"/home/{PwaTestSupport.LoginName}";
        await PwaTestSupport.CreateAndSaveFileAsync(page, directory, "offline-proof.txt", content);

        // Real offline (P2-ACC-015): not just "server stopped" — the browser's own network
        // stack is cut, exactly as docs/adr/0017-pwa-cache-and-offline-strategy.md requires.
        await context.SetOfflineAsync(true);
        await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        // The profile already exists (committed before going offline), so this is Sign In,
        // not onboarding — and it, and the desktop after it, work with zero network access.
        await PwaTestSupport.SignInAsync(page);

        await PwaTestSupport.AssertFileContentAsync(page, directory, "offline-proof.txt", content);
    }

    [Fact]
    public async Task Update_to_a_new_release_purges_the_old_cache_and_preserves_compatible_data()
    {
        int port = PublishedAppHost.ReservePort();
        PublishedAppHost host = new();
        await host.StartAsync(fixture.V1WwwrootPath, port);

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true });
        IPage page = await browser.NewPageAsync();
        await PwaTestSupport.InstallBlobCaptureAsync(page);
        PwaTestSupport.AttachDiagnostics(page);

        await PwaTestSupport.NavigateWhenReadyAsync(page, host.Address);
        await PwaTestSupport.WaitForServiceWorkerControllerAsync(page);
        await PwaTestSupport.CreateLocalProfileAndReachDesktopAsync(page);

        const string content = "Present before and after the update.\n";
        string directory = $"/home/{PwaTestSupport.LoginName}";
        await PwaTestSupport.CreateAndSaveFileAsync(page, directory, "update-proof.txt", content);

        string v1Version = await PwaTestSupport.GetAssetsManifestVersionAsync(page);

        // Swap the origin to serve v2's published output on the same address, then force the
        // browser to check for a new service-worker script (updateViaCache:'none' on
        // registration means the script itself is always revalidated over the network).
        await host.StopAsync();
        await host.StartAsync(fixture.V2WwwrootPath, port);

        await page.EvaluateAsync("""
            () => {
                window.__pwaUpdateAvailable = false;
                window.addEventListener('hackeros-pwa-update-available', () => { window.__pwaUpdateAvailable = true; });
            }
            """);
        await page.EvaluateAsync(
            "async () => { const reg = await navigator.serviceWorker.getRegistration(); await reg.update(); }");
        await page.WaitForFunctionAsync(
            "() => window.__pwaUpdateAvailable === true",
            new PageWaitForFunctionOptions { Timeout = 30000 });

        TaskCompletionSource reloaded = new();
        void OnLoad(object? _, IPage _1) => reloaded.TrySetResult();
        page.Load += OnLoad;
        try
        {
            // Safe activation: post the same SKIP_WAITING message a future "update" UI would
            // send once the user accepts — index.html's own controllerchange listener then
            // reloads the page.
            await page.EvaluateAsync("""
                async () => {
                    const reg = await navigator.serviceWorker.getRegistration();
                    reg.waiting.postMessage({ type: 'SKIP_WAITING' });
                }
                """);
            await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            page.Load -= OnLoad;
        }

        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        // Already-committed profile survives the update (no onboarding form) — Sign In, then desktop.
        await PwaTestSupport.SignInAsync(page);

        string v2Version = await PwaTestSupport.GetAssetsManifestVersionAsync(page);
        Assert.NotEqual(v1Version, v2Version);
        string[] cacheKeys = await PwaTestSupport.GetCacheKeysAsync(page);
        Assert.Equal([$"hackeros-cache-{v2Version}"], cacheKeys);

        await PwaTestSupport.AssertFileContentAsync(page, directory, "update-proof.txt", content);

        await host.DisposeAsync();
    }

    /// <summary>
    /// Documents actual behavior rather than an assumed one: the service worker's cache-first
    /// fetch handler (service-worker.published.js) performs no runtime integrity re-check —
    /// integrity is only verified once, at `install` time — so a cache entry corrupted after
    /// activation is served as-is. This proves that observed reality instead of asserting an
    /// unimplemented "corrupt-cache recovery" behavior.
    /// </summary>
    [Fact]
    public async Task Corrupting_a_cached_asset_offline_serves_the_corrupted_bytes_verbatim()
    {
        int port = PublishedAppHost.ReservePort();
        await using PublishedAppHost host = new();
        await host.StartAsync(fixture.V1WwwrootPath, port);

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true });
        await using IBrowserContext context = await browser.NewContextAsync();
        IPage page = await context.NewPageAsync();

        await PwaTestSupport.NavigateWhenReadyAsync(page, host.Address);
        await PwaTestSupport.WaitForServiceWorkerControllerAsync(page);
        string version = await PwaTestSupport.GetAssetsManifestVersionAsync(page);
        string cacheName = $"hackeros-cache-{version}";

        await page.EvaluateAsync(
            """
            async (cacheName) => {
                const cache = await caches.open(cacheName);
                await cache.put('manifest.webmanifest', new Response('CORRUPTED-BY-TEST', { status: 200, headers: { 'Content-Type': 'application/manifest+json' } }));
            }
            """,
            cacheName);

        await context.SetOfflineAsync(true);
        // Fetch from inside the page (not page.APIRequest, which uses Playwright's own HTTP
        // client and is never intercepted by the service worker) so the request actually
        // goes through the SW's cache-first onFetch handler.
        string body = await page.EvaluateAsync<string>(
            "async () => (await fetch('manifest.webmanifest')).text()");
        Assert.Equal("CORRUPTED-BY-TEST", body);
    }
}
