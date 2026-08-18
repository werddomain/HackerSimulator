using System.Diagnostics;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace HackerOs.UI.E2E.Tests;

public sealed class ClockPanelMobileToggleTests(ITestOutputHelper output)
{
    /// <summary>
    /// End-to-end proof of the Phase 0 mobile-platform toggle location
    /// (docs/mobile-interface-platform-plan.md Phase 0): clicking the taskbar clock opens a
    /// panel whose Auto/Desktop/Mobile choice persists across a reload, while the Desktop shell's
    /// own rendering stays unchanged — this phase is persistence-only, no real Mobile shell exists
    /// yet.
    /// </summary>
    [Fact]
    public async Task Clock_panel_opens_and_the_mobile_choice_persists_across_reload_without_changing_the_desktop_shell()
    {
        string solutionDirectory = E2ESupport.FindSolutionDirectory();
        int port = E2ESupport.ReservePort();
        string address = $"http://127.0.0.1:{port}";
        using Process server = E2ESupport.StartHarness(solutionDirectory, address, testDemo: false, output);

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Headless = true });

            await using IBrowserContext context = await browser.NewContextAsync();
            IPage page = await context.NewPageAsync();
            List<string> consoleErrors = E2ESupport.AttachDiagnostics(page, output);

            await E2ESupport.NavigateWhenReadyAsync(page, address, output);
            await E2ESupport.CreateLocalProfileAndReachDesktopAsync(page, output);

            ILocator clockButton = page.GetByRole(AriaRole.Button, new() { Name = "System clock, notifications, and calendar" });
            await clockButton.ClickAsync();

            ILocator panel = page.GetByRole(AriaRole.Dialog, new() { Name = "Notifications and calendar" });
            await Assertions.Expect(panel).ToBeVisibleAsync();
            output.WriteLine("[test] clock panel opened.");

            ILocator mobileOption = panel.GetByRole(AriaRole.Radio, new() { Name = "Mobile" });
            await mobileOption.ClickAsync();
            await Assertions.Expect(mobileOption).ToHaveAttributeAsync("aria-checked", "true");
            output.WriteLine("[test] selected explicit Mobile platform preference.");

            await page.ReloadAsync();

            // The local profile persists across reload, but the session does not — reload lands
            // on the sign-in view for the existing account, not straight back on the desktop.
            ILocator passwordField = page.GetByLabel("Password", new() { Exact = true });
            await passwordField.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 20000 });
            await passwordField.FillAsync(E2ESupport.Password);
            await page.GetByRole(AriaRole.Button, new() { Name = "Start session" }).ClickAsync();

            ILocator appLauncherAfterReload = page.GetByRole(AriaRole.Button, new() { Name = "App launcher" });
            await appLauncherAfterReload.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
            output.WriteLine("[test] desktop shell reached the same App launcher after reload — no Mobile shell swap.");

            await page.GetByRole(AriaRole.Button, new() { Name = "System clock, notifications, and calendar" }).ClickAsync();
            ILocator mobileOptionAfterReload = page
                .GetByRole(AriaRole.Dialog, new() { Name = "Notifications and calendar" })
                .GetByRole(AriaRole.Radio, new() { Name = "Mobile" });
            await Assertions.Expect(mobileOptionAfterReload).ToHaveAttributeAsync("aria-checked", "true", new() { Timeout = 15000 });
            output.WriteLine("[test] explicit Mobile preference survived the reload.");

            Assert.Empty(consoleErrors);
        }
        finally
        {
            E2ESupport.StopProcess(server);
        }
    }
}
