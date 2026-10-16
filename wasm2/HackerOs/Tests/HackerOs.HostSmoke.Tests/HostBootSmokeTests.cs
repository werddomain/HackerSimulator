using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace HackerOs.HostSmoke.Tests;

/// <summary>
/// The boot smoke test every <c>rebuild/5-*</c> PR runs against the three hosts
/// (execution-isolation-plan.md section 5.2): sign-in screen, desktop, terminal with <c>ps</c>
/// and <c>cat /proc/self/status</c>, task manager, then everything closed again, with no
/// console error at any point.
/// </summary>
/// <remarks>
/// The host is already running (see <see cref="HostSmokeFactAttribute"/>); the test only drives
/// a fresh, isolated browser context, so the first visit always lands on first-run onboarding
/// for a WebAssembly host. A reload then checks the real sign-in screen.
/// </remarks>
public sealed partial class HostBootSmokeTests(ITestOutputHelper output)
{
    private const string LoginName = "smoke";
    private const string DisplayName = "Smoke Test";
    private const string Password = "Smoke-Test-1";

    /// <summary>Generous: the first visit downloads and starts the whole .NET WebAssembly runtime.</summary>
    private const float BootTimeoutMs = 120_000;
    private const float StepTimeoutMs = 30_000;

    /// <summary>Boots the host to the desktop, runs the terminal and the task manager, closes everything.</summary>
    [HostSmokeFact]
    public async Task Host_boots_to_the_desktop_and_runs_the_terminal_and_task_manager()
    {
        HostSmokeSettings settings = HostSmokeSettings.FromEnvironment()!;
        Log($"host '{settings.HostName}' at {settings.Url}");

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await ChromiumLauncher.LaunchAsync(
            playwright, new BrowserTypeLaunchOptions { Headless = true });
        await using IBrowserContext context = await browser.NewContextAsync(
            new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = 1440, Height = 900 } });
        IPage page = await context.NewPageAsync();
        page.SetDefaultTimeout(StepTimeoutMs);
        List<string> errors = AttachDiagnostics(page);

        try
        {
            await NavigateWhenReadyAsync(page, settings.Url);
            await SignInAsync(page, firstVisit: true);
            AssertNoErrors(errors, "first sign-in");

            // Back to the sign-in screen: a reload ends the browser-side session as a power cycle would.
            await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await SignInAsync(page, firstVisit: false);
            AssertNoErrors(errors, "sign-in after reload");

            await OpenAppAsync(page, "Terminal");
            // Read the rendered rows, but click the screen: xterm.js stacks it over the rows.
            ILocator rows = page.Locator("[data-terminal-panel] .xterm-rows").First;
            ILocator screen = page.Locator("[data-terminal-panel] .xterm-screen").First;
            await WaitForTerminalTextAsync(rows, text => PromptPattern().IsMatch(text), "the shell prompt");

            await RunCommandAsync(page, screen, "ps");
            string psOutput = await WaitForTerminalTextAsync(
                rows, text => text.Contains("PID", StringComparison.Ordinal), "the ps header");
            Assert.Contains("hsh", psOutput, StringComparison.Ordinal);
            Log("ps listed the processes.");

            await RunCommandAsync(page, screen, "cat /proc/self/status");
            string status = await WaitForTerminalTextAsync(
                rows, text => text.Contains("Pid:", StringComparison.Ordinal) && text.Contains("VmRSS:", StringComparison.Ordinal),
                "/proc/self/status");
            Assert.Contains("Name:", status, StringComparison.Ordinal);
            AssertSource(status, "CpuSource", settings.ExpectedCpuSource);
            AssertSource(status, "MemSource", settings.ExpectedMemSource);
            Log($"/proc/self/status read ({Sources(status)}).");
            AssertNoErrors(errors, "terminal");

            await OpenAppAsync(page, "System Monitor");
            ILocator filter = page.GetByLabel("Filter processes");
            await filter.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await filter.FillAsync("terminal");
            await page.Locator("table.proc-table tbody tr").First.WaitForAsync(
                new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            Log("task manager lists the terminal process.");
            AssertNoErrors(errors, "task manager");

            await CloseAllWindowsAsync(page);
            AssertNoErrors(errors, "closing every window");
            Log("all windows closed; no console error.");
        }
        catch
        {
            await SaveScreenshotAsync(page, settings);
            throw;
        }
    }

    /// <summary>
    /// Completes onboarding (first visit) or the sign-in form, then waits for the desktop. A host
    /// that keeps its session across a reload (the server host's circuit can) may go straight to
    /// the desktop; that is accepted.
    /// </summary>
    private async Task SignInAsync(IPage page, bool firstVisit)
    {
        ILocator onboarding = page.GetByRole(AriaRole.Heading, new() { Name = "Create administrator" });
        ILocator signIn = page.GetByRole(AriaRole.Heading, new() { Name = "Sign in" });
        ILocator launcher = page.GetByRole(AriaRole.Button, new() { Name = "App launcher" });
        ILocator fatal = page.Locator(".fatal-surface, .recovery-surface");

        await onboarding.Or(signIn).Or(launcher).Or(fatal).First.WaitForAsync(
            new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = BootTimeoutMs });

        if (await fatal.IsVisibleAsync())
        {
            throw new InvalidOperationException($"The host stopped on an error screen: {await fatal.InnerTextAsync()}");
        }

        if (await onboarding.IsVisibleAsync())
        {
            Log("first-run onboarding shown.");
            await page.GetByLabel("Login name").FillAsync(LoginName);
            await page.GetByLabel("Display name").FillAsync(DisplayName);
            await page.GetByLabel("Password", new() { Exact = true }).FillAsync(Password);
            await page.GetByLabel("Confirm password").FillAsync(Password);
            await page.GetByRole(AriaRole.Button, new() { Name = "Create local profile" }).ClickAsync();
        }
        else if (await signIn.IsVisibleAsync())
        {
            Log("sign-in screen shown.");
            ILocator user = page.GetByLabel("Local user");
            if (await user.Locator($"option[value='{LoginName}']").CountAsync() > 0)
            {
                await user.SelectOptionAsync(LoginName);
            }

            await page.GetByLabel("Password").FillAsync(Password);
            await page.GetByRole(AriaRole.Button, new() { Name = "Start session" }).ClickAsync();
        }
        else if (firstVisit)
        {
            Log("desktop shown without a sign-in step.");
        }

        await launcher.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = BootTimeoutMs });
        Log("desktop ready.");
    }

    /// <summary>Opens an app from the Start menu by its exact display name.</summary>
    private async Task OpenAppAsync(IPage page, string appName)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "App launcher" }).ClickAsync();
        await page.GetByRole(AriaRole.Combobox, new() { Name = "Search applications" }).FillAsync(appName);
        ILocator launch = page.GetByRole(AriaRole.Button, new() { Name = $"Launch {appName}", Exact = true });
        await launch.ClickAsync();
        Log($"launched '{appName}'.");
    }

    /// <summary>Types a command line into the xterm.js screen, as a person would.</summary>
    private static async Task RunCommandAsync(IPage page, ILocator screen, string commandLine)
    {
        await screen.ClickAsync();
        await page.Keyboard.TypeAsync(commandLine);
        await page.Keyboard.PressAsync("Enter");
    }

    /// <summary>Polls the terminal's rendered rows until <paramref name="done"/> holds.</summary>
    private async Task<string> WaitForTerminalTextAsync(ILocator rows, Func<string, bool> done, string what)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        string text = string.Empty;
        while (elapsed.ElapsedMilliseconds < StepTimeoutMs)
        {
            text = (await rows.InnerTextAsync()).Replace(' ', ' ');
            if (done(text))
            {
                return text;
            }

            await Task.Delay(250);
        }

        Log($"terminal text when giving up:\n{text}");
        throw new TimeoutException($"The terminal never showed {what}.");
    }

    /// <summary>Clicks every window's close button until none is left.</summary>
    private static async Task CloseAllWindowsAsync(IPage page)
    {
        ILocator closeButtons = page.Locator("button.chrome-button-close");
        Stopwatch elapsed = Stopwatch.StartNew();
        while (await closeButtons.CountAsync() > 0)
        {
            if (elapsed.ElapsedMilliseconds > StepTimeoutMs)
            {
                throw new TimeoutException($"{await closeButtons.CountAsync()} window(s) did not close.");
            }

            int before = await closeButtons.CountAsync();
            await closeButtons.First.ClickAsync();
            Stopwatch wait = Stopwatch.StartNew();
            while (await closeButtons.CountAsync() >= before && wait.ElapsedMilliseconds < 5_000)
            {
                await Task.Delay(100);
            }
        }
    }

    private static void AssertSource(string status, string field, string? expected)
    {
        if (expected is null)
        {
            return;
        }

        Match match = Regex.Match(status, $@"{field}:\s*(\w+)");
        Assert.True(match.Success, $"/proc/self/status has no '{field}:' line.");
        Assert.Equal(expected, match.Groups[1].Value);
    }

    /// <summary>The <c>CpuSource:</c>/<c>MemSource:</c> values for the log, when present.</summary>
    private static string Sources(string status)
    {
        Match cpu = Regex.Match(status, @"CpuSource:\s*(\w+)");
        Match mem = Regex.Match(status, @"MemSource:\s*(\w+)");
        return $"CpuSource {(cpu.Success ? cpu.Groups[1].Value : "-")}, MemSource {(mem.Success ? mem.Groups[1].Value : "-")}";
    }

    private static void AssertNoErrors(List<string> errors, string step)
    {
        lock (errors)
        {
            if (errors.Count > 0)
            {
                Assert.Fail($"Console errors by the end of '{step}':\n  {string.Join("\n  ", errors)}");
            }
        }
    }

    /// <summary>Records console errors and uncaught page errors; logs everything else for diagnosis.</summary>
    private List<string> AttachDiagnostics(IPage page)
    {
        List<string> errors = [];
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                lock (errors)
                {
                    errors.Add($"console: {message.Text}");
                }
            }

            if (message.Type is "error" or "warning")
            {
                Log($"[console:{message.Type}] {message.Text}");
            }
        };
        page.PageError += (_, error) =>
        {
            lock (errors)
            {
                errors.Add($"page error: {error}");
            }

            Log($"[pageerror] {error}");
        };
        page.RequestFailed += (_, request) => Log($"[requestfailed] {request.Method} {request.Url} — {request.Failure}");
        // The console only says "Failed to load resource: 404"; the response names the URL.
        // (Chromium reports no event for its implicit /favicon.ico request.)
        page.Response += (_, response) =>
        {
            if (response.Status >= 400)
            {
                Log($"[http {response.Status}] {response.Request.Method} {response.Url}");
            }
        };
        return errors;
    }

    /// <summary>Retries the first navigation while the host is still binding its port.</summary>
    private static async Task NavigateWhenReadyAsync(IPage page, string url)
    {
        Exception? last = null;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
                return;
            }
            catch (PlaywrightException exception)
            {
                last = exception;
                await Task.Delay(1_000);
            }
        }

        throw new InvalidOperationException($"{url} never answered.", last);
    }

    private async Task SaveScreenshotAsync(IPage page, HostSmokeSettings settings)
    {
        if (settings.ArtifactsDirectory is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(settings.ArtifactsDirectory);
            string path = Path.Combine(settings.ArtifactsDirectory, $"{settings.HostName}-failure.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
            Log($"screenshot: {path}");
        }
        catch (PlaywrightException exception)
        {
            Log($"no screenshot: {exception.Message}");
        }
    }

    private void Log(string message) => output.WriteLine($"[smoke] {message}");

    /// <summary>The end of an hsh prompt: <c>$</c> or <c>#</c> at the end of a rendered line.</summary>
    [GeneratedRegex(@"[$#]\s*$", RegexOptions.Multiline)]
    private static partial Regex PromptPattern();
}
