using System.Diagnostics;
using System.Globalization;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace HackerOs.UI.E2E.Tests;

/// <summary>
/// ADR 0116: a window moved and resized with the pointer reopens at the same place after being
/// closed, and again after the page is reloaded and the user signs back in, because the geometry
/// lives in <c>~/.local/state/hackeros/windows/&lt;appId&gt;.config</c> on the virtual disk.
/// </summary>
public sealed class WindowGeometryRestoreTests(ITestOutputHelper output)
{
    [Fact]
    public async Task A_moved_and_resized_window_reopens_at_the_same_place_after_close_and_reload()
    {
        string solutionDirectory = E2ESupport.FindSolutionDirectory();
        int port = E2ESupport.ReservePort();
        string address = $"http://127.0.0.1:{port}";
        using Process server = E2ESupport.StartHarness(solutionDirectory, address, testDemo: false, output);

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await ChromiumLauncher.LaunchAsync(playwright,
                new BrowserTypeLaunchOptions { Headless = true });

            await using IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1400, Height = 900 },
            });
            IPage page = await context.NewPageAsync();
            List<string> consoleErrors = E2ESupport.AttachDiagnostics(page, output);

            await E2ESupport.NavigateWhenReadyAsync(page, address, output);
            await E2ESupport.CreateLocalProfileAndReachDesktopAsync(page, output);

            ILocator window = page.Locator("article.window-host");
            await E2ESupport.OpenAppAsync(page, "Text Editor", output);
            await Assertions.Expect(window).ToHaveCountAsync(1, new() { Timeout = 15000 });
            (double x, double y, double width, double height) opened = await BoundsAsync(window);
            output.WriteLine($"[test] opened at {opened}.");

            // Drag the title bar 120 px right and 60 px down, then the bottom-right corner 80 px out.
            ILocator title = window.Locator("header.window-chrome h2");
            LocatorBoundingBoxResult titleBox = (await title.BoundingBoxAsync())!;
            await DragAsync(page, titleBox.X + 10, titleBox.Y + (titleBox.Height / 2), 120, 60);
            await Assertions.Expect(window).ToHaveAttributeAsync("data-window-x", Format(opened.x + 120));

            LocatorBoundingBoxResult cornerBox = (await window.Locator(".resize-handle.resize-bottom-right").BoundingBoxAsync())!;
            await DragAsync(page, cornerBox.X + (cornerBox.Width / 2), cornerBox.Y + (cornerBox.Height / 2), 80, 40);
            await Assertions.Expect(window).ToHaveAttributeAsync("data-window-width", Format(opened.width + 80));
            (double x, double y, double width, double height) moved = await BoundsAsync(window);
            output.WriteLine($"[test] moved and resized to {moved}.");

            await window.Locator("button.chrome-button-close").ClickAsync();
            await Assertions.Expect(window).ToHaveCountAsync(0, new() { Timeout = 15000 });

            await E2ESupport.OpenAppAsync(page, "Text Editor", output);
            await Assertions.Expect(window).ToHaveCountAsync(1, new() { Timeout = 15000 });
            Assert.Equal(moved, await BoundsAsync(window));
            output.WriteLine("[test] the window reopened where it was closed.");

            // Give the grouped write time to land, then reload: the session ends, the file stays.
            await page.WaitForTimeoutAsync(1500);
            await page.ReloadAsync();
            ILocator passwordField = page.GetByLabel("Password", new() { Exact = true });
            await passwordField.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 20000 });
            await passwordField.FillAsync(E2ESupport.Password);
            await page.GetByRole(AriaRole.Button, new() { Name = "Start session" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "App launcher" })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });

            await E2ESupport.OpenAppAsync(page, "Text Editor", output);
            await Assertions.Expect(window).ToHaveCountAsync(1, new() { Timeout = 15000 });
            Assert.Equal(moved, await BoundsAsync(window));
            output.WriteLine("[test] the window reopened at the same place after a reload.");

            Assert.Empty(consoleErrors);
        }
        finally
        {
            E2ESupport.StopProcess(server);
        }
    }

    private static async Task DragAsync(IPage page, double fromX, double fromY, double deltaX, double deltaY)
    {
        await page.Mouse.MoveAsync((float)fromX, (float)fromY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(fromX + deltaX), (float)(fromY + deltaY), new MouseMoveOptions { Steps = 8 });
        await page.Mouse.UpAsync();
    }

    private static async Task<(double X, double Y, double Width, double Height)> BoundsAsync(ILocator window) => (
        await ReadAsync(window, "data-window-x"),
        await ReadAsync(window, "data-window-y"),
        await ReadAsync(window, "data-window-width"),
        await ReadAsync(window, "data-window-height"));

    private static async Task<double> ReadAsync(ILocator window, string attribute) =>
        double.Parse((await window.GetAttributeAsync(attribute))!, CultureInfo.InvariantCulture);

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);
}
