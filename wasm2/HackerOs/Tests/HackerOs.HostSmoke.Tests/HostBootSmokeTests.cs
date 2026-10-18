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
            await AssertScopedStylesAppliedAsync(page, ".taskbar-container", "taskbar (HackerOs.Taskbar.Blazor)");
            await CheckServerConnectionAsync(page, settings);
            AssertNoErrors(errors, "server connection");

            await OpenAppAsync(page, "Terminal");
            // Read the rendered rows, but click the screen: xterm.js stacks it over the rows.
            ILocator rows = page.Locator("[data-terminal-panel] .xterm-rows").First;
            ILocator screen = page.Locator("[data-terminal-panel] .xterm-screen").First;
            await WaitForTerminalTextAsync(rows, text => PromptPattern().IsMatch(text), "the shell prompt");
            await AssertScopedStylesAppliedAsync(page, ".window-chrome", "window chrome (HackerOs.Windowing.Blazor)");

            await RunCommandAsync(page, screen, "ps");
            string psOutput = await WaitForTerminalTextAsync(
                rows, text => OutputFinished(text, "PID"), "the ps output and the next prompt");
            Assert.Contains("hsh", psOutput, StringComparison.Ordinal);
            Log("ps listed the processes.");

            await RunCommandAsync(page, screen, "cat /proc/self/status");
            string status = await WaitForTerminalTextAsync(
                rows, text => OutputFinished(text, "Pid:") && text.Contains("VmRSS:", StringComparison.Ordinal),
                "/proc/self/status and the next prompt");
            Assert.Contains("Name:", status, StringComparison.Ordinal);
            AssertSource(status, "CpuSource", settings.ExpectedCpuSource);
            AssertSource(status, "MemSource", settings.ExpectedMemSource);
            Log($"/proc/self/status read ({Sources(status)}).");
            AssertNoErrors(errors, "terminal");

            await CheckWorkersAsync(page, screen, rows, settings);
            AssertNoErrors(errors, "workers");

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
    /// ADR 0134: the OS served by <c>HackerOs.Server</c> connects to it without any question, and
    /// the notification-area icon says so ("Connected, not identified"); identifying from the icon's
    /// panel (a new account) turns it to "Connected". A host that is not a HackerOS server shows no
    /// server icon at all.
    /// </summary>
    private async Task CheckServerConnectionAsync(IPage page, HostSmokeSettings settings)
    {
        ILocator icon = page.Locator(".server-status-trigger");
        if (!settings.ExpectsServerConnection)
        {
            // The discovery answers at once on a static host; give it a moment, then nothing may show.
            await Task.Delay(2_000);
            Assert.Equal(0, await icon.CountAsync());
            Log("no server icon on this host.");
            return;
        }

        await page.Locator(".server-status-trigger.is-anonymous").WaitForAsync(
            new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Log($"server icon: {await icon.GetAttributeAsync("aria-label")}");

        await icon.ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Identify" }).ClickAsync();
        await page.GetByLabel("Account name").FillAsync($"{LoginName}-{Guid.NewGuid():N}"[..24]);
        await page.Locator("#server-status-password").FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create an account" }).ClickAsync();
        await page.Locator(".server-status-trigger.is-connected").WaitForAsync(
            new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Log($"identified from the icon: {await icon.GetAttributeAsync("aria-label")}");
        await icon.ClickAsync(); // close the panel
    }

    /// <summary>
    /// ADR 0070/0071 (plan section 5.2): on every host, commands run in Web Workers — a pipeline
    /// works, <c>/proc/hackeros/workers</c> lists the pool, and a command stuck in a loop
    /// (<c>stress</c>) leaves the desktop responsive, shows "not-responding" and dies on
    /// <c>kill -9</c> and on <c>kill</c> (escalation after the grace period). A host without
    /// workers (none since ADR 0130) has no such file and commands still run.
    /// </summary>
    private async Task CheckWorkersAsync(IPage page, ILocator screen, ILocator rows, HostSmokeSettings settings)
    {
        await RunCommandAsync(page, screen, "clear; seq 30 | grep 1 | wc -l");
        await WaitForTerminalTextAsync(rows, text => Regex.IsMatch(text, @"^\s*12\s*$", RegexOptions.Multiline), "the pipeline's count");
        Log("pipeline ran.");

        // ADR 0124, section 4: a command of a sample project, in an assembly of its own that no worker
        // preloads; a pool worker fetches it on demand (the server host serves the same OS, ADR 0130).
        await RunCommandAsync(page, screen, "clear; sample-cmd loaded-on-demand");
        await WaitForTerminalTextAsync(rows, text => Regex.IsMatch(text, @"^loaded-on-demand\s*$", RegexOptions.Multiline), "the sample command's output");
        Log("sample-cmd ran from its own assembly.");

        await RunCommandAsync(page, screen, "clear; cat /proc/hackeros/workers");
        if (!settings.ExpectsWorkers)
        {
            await WaitForTerminalTextAsync(rows, text => text.Contains("No such file", StringComparison.Ordinal), "no worker on this host");
            Log("no Web Worker on this host (ADR 0070, section 6).");
            return;
        }

        string workers = await WaitForTerminalTextAsync(
            rows, text => text.Contains("pool_size", StringComparison.Ordinal) && Regex.IsMatch(text, @"^\d+\s+pool\s", RegexOptions.Multiline),
            "/proc/hackeros/workers with a pool worker");
        Log($"workers:\n{workers.Trim()}");

        // A command that never yields, in the background: the desktop must keep answering.
        await RunCommandAsync(page, screen, "clear; stress -t 600 &");
        await WaitForTerminalTextAsync(rows, text => text.Contains("dispatching hogs", StringComparison.Ordinal), "stress started");
        Stopwatch answered = Stopwatch.StartNew();
        await RunCommandAsync(page, screen, "echo still-responsive");
        await WaitForTerminalTextAsync(rows, text => Regex.IsMatch(text, @"^still-responsive\s*$", RegexOptions.Multiline), "an echo while stress spins");
        Log($"echo answered in {answered.ElapsedMilliseconds} ms while stress spins.");
        await Task.Delay(6_500); // past the 5 s unresponsive threshold
        await RunCommandAsync(page, screen, "clear; cat /proc/hackeros/workers");
        await WaitForTerminalTextAsync(rows, text => text.Contains("not-responding", StringComparison.Ordinal), "a not-responding worker");
        await RunCommandAsync(page, screen, "clear; kill -9 %1");
        await WaitForTerminalTextAsync(rows, text => text.Contains("Killed", StringComparison.Ordinal), "Killed after kill -9");
        Log("kill -9 ended the stuck command.");

        // SIGTERM is never delivered to a stuck worker: the OS terminates it after the grace period.
        await RunCommandAsync(page, screen, "clear; stress -t 600 &");
        await WaitForTerminalTextAsync(rows, text => text.Contains("dispatching hogs", StringComparison.Ordinal), "stress started again");
        await RunCommandAsync(page, screen, "kill %1; sleep 7; jobs");
        await WaitForTerminalTextAsync(
            rows, text => text.Contains("Terminated", StringComparison.Ordinal) || text.Contains("Killed", StringComparison.Ordinal),
            "the job ended after kill");
        Log("kill (SIGTERM) ended the stuck command after the grace period.");
    }

    /// <summary>
    /// Completes onboarding (first visit) or the sign-in form, then waits for the desktop. A host
    /// that keeps its session across a reload may go straight to the desktop; that is accepted.
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

    /// <summary>
    /// Fails when a component's scoped CSS (CSS isolation) did not reach the page. The probed
    /// elements are a <c>&lt;footer&gt;</c> and a <c>&lt;header&gt;</c>, <c>display: block</c> by
    /// default, that only their scoped stylesheet turns into <c>flex</c>: a host that forgets its
    /// <c>{Project}.styles.css</c> bundle (HackerOs.Server did) renders an unstyled desktop
    /// without any console error.
    /// </summary>
    private async Task AssertScopedStylesAppliedAsync(IPage page, string selector, string what)
    {
        ILocator element = page.Locator(selector).First;
        await element.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
        string display = await element.EvaluateAsync<string>("e => getComputedStyle(e).display");
        Assert.True(display == "flex", $"Scoped styles of the {what} are not applied: '{selector}' has display '{display}', expected 'flex'.");
        Log($"scoped styles applied: {what}.");
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

    private void Log(string message)
    {
        // One prefix per line: verify-hosts.sh keeps only the lines that carry it.
        foreach (string line in message.Split('\n'))
        {
            output.WriteLine($"[smoke] {line}");
        }
    }

    /// <summary>The end of an hsh prompt: <c>$</c> or <c>#</c> at the end of a rendered line.</summary>
    /// <summary>
    /// True once <paramref name="marker"/> is on screen and a shell prompt follows it. A command's
    /// output reaches xterm.js in several writes (from a Web Worker, one message per write), so a
    /// marker from the first line does not mean the rest is rendered; the next prompt does.
    /// </summary>
    private static bool OutputFinished(string text, string marker)
    {
        int at = text.LastIndexOf(marker, StringComparison.Ordinal);
        return at >= 0 && PromptPattern().IsMatch(text[(at + marker.Length)..]);
    }

    [GeneratedRegex(@"[$#]\s*$", RegexOptions.Multiline)]
    private static partial Regex PromptPattern();
}
