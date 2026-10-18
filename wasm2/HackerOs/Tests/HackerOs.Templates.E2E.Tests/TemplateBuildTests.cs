using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Playwright;

namespace HackerOs.Templates.E2E.Tests;

/// <summary>ADR 0098 §6: each template, generated with <c>dotnet new</c> and built from the local feed.</summary>
[Collection(TemplateWorkspaceCollection.Name)]
public sealed class TemplateBuildTests(TemplateWorkspace workspace)
{
    /// <summary>An app template builds and its manifest names the app id and the SDK it was built against.</summary>
    [Theory]
    [InlineData("hackeros-window", "window", "Contoso.WindowApp")]
    [InlineData("hackeros-command", "terminal", "Contoso.CommandApp")]
    [InlineData("hackeros-service", "service", "Contoso.ServiceApp")]
    [InlineData("hackeros-site", "web", "Contoso.SiteApp")]
    public async Task An_app_template_builds_against_the_local_feed(string template, string kind, string name)
    {
        string appId = "com.contoso." + template["hackeros-".Length..];
        string directory = Path.Combine(workspace.Root, name);

        await workspace.RunCheckedAsync(workspace.Root, "new", template, "-n", name, "-o", directory, "--appId", appId);
        await workspace.RunCheckedAsync(directory, "build");

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
        Assert.Equal(appId, manifest.RootElement.GetProperty("id").GetString());
        Assert.Equal(kind, manifest.RootElement.GetProperty("kind").GetString());
        Assert.Equal(workspace.Version, manifest.RootElement.GetProperty("sdkCompatibility").GetProperty("minimumVersion").GetString());
    }

    /// <summary>
    /// The host template, with a generated window app and command added as project references, builds,
    /// aggregates them with the base app packages, boots in Chromium and runs both.
    /// </summary>
    [Fact]
    public async Task The_host_template_boots_and_runs_generated_apps()
    {
        string root = Path.Combine(workspace.Root, "host-scenario");
        Directory.CreateDirectory(root);
        await workspace.RunCheckedAsync(root, "new", "hackeros-window", "-n", "Contoso.Notes", "-o", "Notes", "--appId", "com.contoso.notes");
        await workspace.RunCheckedAsync(root, "new", "hackeros-command", "-n", "Contoso.Hello", "-o", "Hello", "--appId", "com.contoso.hello");
        await workspace.RunCheckedAsync(root, "new", "hackeros-host", "-n", "Contoso.Os", "-o", "Host");
        string host = Path.Combine(root, "Host");
        await workspace.RunCheckedAsync(host, "add", "reference", "../Notes/Contoso.Notes.csproj", "../Hello/Contoso.Hello.csproj");
        await workspace.RunCheckedAsync(host, "build");

        string aggregated = File.ReadAllText(Path.Combine(host, "obj", "Debug", "net10.0", "BuiltInApplications.json"));
        Assert.Contains("\"com.contoso.notes\"", aggregated, StringComparison.Ordinal);
        Assert.Contains("\"com.contoso.hello\"", aggregated, StringComparison.Ordinal);
        Assert.Contains("\"org.hackeros.terminal\"", aggregated, StringComparison.Ordinal);

        int port = FreePort();
        using System.Diagnostics.Process server = workspace.Start(host, "run", "--no-build", "--urls", $"http://127.0.0.1:{port}");
        try
        {
            await WaitForAsync($"http://127.0.0.1:{port}/");

            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await ChromiumLauncher.LaunchAsync(playwright, new BrowserTypeLaunchOptions());
            IPage page = await browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 1280, Height = 800 } });
            await page.GotoAsync($"http://127.0.0.1:{port}/");

            // First boot: create the administrator, then the desktop.
            await page.Locator("input").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 120_000 });
            ILocator inputs = page.Locator("input");
            await inputs.Nth(0).FillAsync("neo");
            await inputs.Nth(1).FillAsync("Neo");
            await inputs.Nth(2).FillAsync("matrix123");
            await inputs.Nth(3).FillAsync("matrix123");
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create local profile" }).ClickAsync();

            // The generated window app, lazy-loaded from its project reference.
            await page.GetByText("Applications", new PageGetByTextOptions { Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = 60_000 });
            await page.GetByText("Contoso.Notes", new PageGetByTextOptions { Exact = true }).First.ClickAsync();
            await page.GetByText("Clicked 0 times").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
            await page.GetByText("Clicked 0 times").ClickAsync();
            await page.GetByText("Clicked 1 times").WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });

            // The generated command, run from the Terminal package.
            await page.GetByText("Applications", new PageGetByTextOptions { Exact = true }).ClickAsync();
            await page.GetByText("Terminal", new PageGetByTextOptions { Exact = true }).First.ClickAsync();
            await page.GetByText("Type 'help'").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
            await page.Keyboard.TypeAsync("hello Morpheus");
            await page.Keyboard.PressAsync("Enter");
            await page.GetByText("Hello, Morpheus!").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
        }
        finally
        {
            server.Kill(entireProcessTree: true);
        }
    }

    /// <summary>
    /// ADR 0098 §8, ADR 0114, ADR 0132 §1: the host template with the app packages compiled against
    /// SDK 1.0.0 and 1.1.0 (<c>Tests/fixtures/sdk-compat</c>) boots, and the old window apps and
    /// commands run on the current SDK: file writes and reads, arguments, standard output, and for
    /// 1.1.0 the window title and the password rule it added.
    /// </summary>
    [Fact]
    public async Task A_host_runs_app_packages_compiled_against_SDK_1_0_0_and_1_1_0()
    {
        string root = Path.Combine(workspace.Root, "sdk1-scenario");
        Directory.CreateDirectory(root);
        await workspace.RunCheckedAsync(root, "new", "hackeros-host", "-n", "Contoso.Os", "-o", "Host");
        string host = Path.Combine(root, "Host");
        await workspace.RunCheckedAsync(host, "add", "package", "Contoso.Sdk1Compat", "--version", "1.0.0");
        await workspace.RunCheckedAsync(host, "add", "package", "Contoso.Sdk11Compat", "--version", "1.1.0");
        await workspace.RunCheckedAsync(host, "build");

        string aggregated = File.ReadAllText(Path.Combine(host, "obj", "Debug", "net10.0", "BuiltInApplications.json"));
        Assert.Contains("\"com.contoso.sdk1-window\"", aggregated, StringComparison.Ordinal);
        Assert.Contains("\"com.contoso.sdk1-command\"", aggregated, StringComparison.Ordinal);
        Assert.Contains("\"com.contoso.sdk11-window\"", aggregated, StringComparison.Ordinal);
        Assert.Contains("\"com.contoso.sdk11-command\"", aggregated, StringComparison.Ordinal);

        int port = FreePort();
        using System.Diagnostics.Process server = workspace.Start(host, "run", "--no-build", "--urls", $"http://127.0.0.1:{port}");
        try
        {
            await WaitForAsync($"http://127.0.0.1:{port}/");

            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await ChromiumLauncher.LaunchAsync(playwright, new BrowserTypeLaunchOptions());
            IPage page = await browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 1280, Height = 800 } });
            await page.GotoAsync($"http://127.0.0.1:{port}/");

            await page.Locator("input").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 120_000 });
            ILocator inputs = page.Locator("input");
            await inputs.Nth(0).FillAsync("neo");
            await inputs.Nth(1).FillAsync("Neo");
            await inputs.Nth(2).FillAsync("matrix123");
            await inputs.Nth(3).FillAsync("matrix123");
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create local profile" }).ClickAsync();

            // The window app of SDK 1.0.0, lazy-loaded from its package.
            await page.GetByText("Applications", new PageGetByTextOptions { Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = 60_000 });
            await page.GetByText("SDK 1.0 Window", new PageGetByTextOptions { Exact = true }).First.ClickAsync();
            await page.GetByText("Compiled against SDK").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save note" }).ClickAsync();
            await page.GetByText("Read back: saved by SDK 1.0").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });

            // The window app of SDK 1.1.0 names its window (SetWindowTitleAsync, ADR 0117).
            await page.GetByText("Applications", new PageGetByTextOptions { Exact = true }).ClickAsync();
            await page.GetByText("SDK 1.1 Window", new PageGetByTextOptions { Exact = true }).First.ClickAsync();
            await page.GetByText("note.txt — SDK 1.1 Window").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
            ILocator sdk11Window = page.Locator(".compat-window", new PageLocatorOptions { HasText = "SDK 1.1 Window" });
            await sdk11Window.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Save note" }).ClickAsync();
            await page.GetByText("Read back: saved by SDK 1.1").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
            await page.GetByText("sdk11-window.txt — SDK 1.1 Window").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });

            // Its command, run from the Terminal.
            await page.GetByText("Applications", new PageGetByTextOptions { Exact = true }).ClickAsync();
            await page.GetByText("Terminal", new PageGetByTextOptions { Exact = true }).First.ClickAsync();
            await page.GetByText("Type 'help'").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
            await page.Keyboard.TypeAsync("sdk1-compat still binds");
            await page.Keyboard.PressAsync("Enter");
            await page.GetByText($"sdk1-compat: still binds (compiled against 1.0.0, running on {workspace.Version})")
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
            await page.Keyboard.TypeAsync("sdk11-compat still binds");
            await page.Keyboard.PressAsync("Enter");
            await page.GetByText($"sdk11-compat: still binds [valid password] (compiled against 1.1.0, running on {workspace.Version})")
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
        }
        finally
        {
            server.Kill(entireProcessTree: true);
        }
    }

    private static int FreePort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task WaitForAsync(string url)
    {
        using HttpClient http = new();
        for (int attempt = 0; attempt < 120; attempt++)
        {
            try
            {
                using HttpResponseMessage response = await http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"{url} did not answer.");
    }
}
