using System.Diagnostics;
using HackerOs.Platform.Core.FileSystem;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace HackerOs.UI.E2E.Tests;

public sealed class TextEditorFileTests(ITestOutputHelper output)
{
    private const string NewContent = "Rewritten by the HackerOS E2E Text Editor test.\n";

    /// <summary>
    /// Drives Text Editor through the real platform Open-file dialog to load a seeded
    /// fixture file, edits and saves it, then verifies — via a File Explorer download,
    /// independent of the editor itself — that the new content actually reached the
    /// virtual filesystem. This exercises the same <c>FileDialogCoordinator</c> /
    /// <c>FileDialogWindowAdapter</c> / <c>DesktopShell</c> wiring that makes the
    /// platform Open/Save dialogs work at all (see docs/file-dialogs.md).
    /// </summary>
    [Fact]
    public async Task Text_editor_opens_edits_and_saves_a_seeded_file_via_the_open_dialog()
    {
        string solutionDirectory = E2ESupport.FindSolutionDirectory();
        int port = E2ESupport.ReservePort();
        string address = $"http://127.0.0.1:{port}";
        using Process server = E2ESupport.StartHarness(solutionDirectory, address, testDemo: true, output);

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Headless = true });

            await using IBrowserContext context = await browser.NewContextAsync();
            IPage page = await context.NewPageAsync();
            List<string> consoleErrors = E2ESupport.AttachDiagnostics(page, output);
            await E2ESupport.InstallBlobCaptureAsync(page);

            await E2ESupport.NavigateWhenReadyAsync(page, address, output);
            await E2ESupport.CreateLocalProfileAndReachDesktopAsync(page, output);
            await E2ESupport.SeedFixturesAsync(page, output);

            await E2ESupport.OpenAppAsync(page, "Text Editor", output);

            ILocator textarea = page.Locator("#editor-textarea");
            await textarea.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

            // --- Open the seeded file through the real platform Open dialog ---
            // The "File" menu is a hover-revealed CSS dropdown (TextEditorWindow.razor.css:
            // .menu-group:hover .menu-dropdown) — #btn-open is in the DOM but not visible
            // until the group is hovered.
            await page.GetByText("File", new() { Exact = true }).HoverAsync();
            await page.Locator("#btn-open").ClickAsync();

            ILocator openDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Open Text File" });
            await openDialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
            output.WriteLine("[test] Open File dialog is visible.");

            await openDialog.GetByRole(AriaRole.Option, new() { NameString = "Documents", Exact = false }).DblClickAsync();
            await openDialog.GetByRole(AriaRole.Option, new() { NameString = TestDemoFixtureSeeder.FixtureRootName, Exact = false }).DblClickAsync();

            ILocator editableOption = openDialog.GetByRole(AriaRole.Option, new() { NameString = TestDemoFixtureSeeder.EditableFileName, Exact = false });
            await editableOption.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await editableOption.ClickAsync();

            await openDialog.GetByRole(AriaRole.Button, new() { Name = "Open" }).ClickAsync();
            await openDialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 15000 });
            output.WriteLine("[test] Open File dialog closed after selecting the fixture file.");

            string loadedContent = await textarea.InputValueAsync();
            Assert.Equal(TestDemoFixtureSeeder.EditableOriginalContent, loadedContent);

            // --- Edit and save ---
            await textarea.FillAsync(NewContent);
            ILocator saveButton = page.Locator("#btn-save");
            await Assertions.Expect(saveButton).ToBeEnabledAsync();

            // #btn-save only renders visible while its ancestor .menu-group is
            // CSS-hovered (TextEditorWindow.razor.css), so hover "File" first.
            await page.GetByText("File", new() { Exact = true }).HoverAsync();
            await saveButton.ClickAsync();

            ILocator titleDisplay = page.Locator(".editor-title-display");
            await Assertions.Expect(titleDisplay).ToContainTextAsync(TestDemoFixtureSeeder.EditableFileName);
            output.WriteLine($"[test] title after save: '{await titleDisplay.InnerTextAsync()}'; " +
                $"status bar: '{await page.Locator(".editor-statusbar").InnerTextAsync()}'.");
            await Assertions.Expect(saveButton).ToBeDisabledAsync();
            output.WriteLine("[test] Save completed (dirty marker cleared, Save disabled again).");

            // --- Verify the write actually reached the VFS, independent of the editor ---
            await E2ESupport.OpenAppAsync(page, "File Explorer", output);

            ILocator addressInput = page.GetByLabel("Current path address");
            await addressInput.FillAsync($"{E2ESupport.HomePath}/Documents/{TestDemoFixtureSeeder.FixtureRootName}");
            await addressInput.PressAsync("Enter");

            await E2ESupport.SelectFileExplorerEntryAsync(page, TestDemoFixtureSeeder.EditableFileName, output);

            ILocator downloadBtn = page.GetByRole(AriaRole.Button, new() { Name = "Download" });
            (string fileName, byte[] savedBytes) = await E2ESupport.CaptureNextDownloadAsync(
                page, () => downloadBtn.ClickAsync(), output);

            Assert.Equal(TestDemoFixtureSeeder.EditableFileName, fileName);
            string savedContent = System.Text.Encoding.UTF8.GetString(savedBytes);
            Assert.Equal(NewContent, savedContent);
            Assert.Empty(consoleErrors);
        }
        finally
        {
            E2ESupport.StopProcess(server);
        }
    }
}
