using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Playwright;

namespace HackerOs.E2E.Tests;

/// <summary>Runs browser persistence contracts against native Chromium IndexedDB.</summary>
public sealed class IndexedDbBrowserContractTests
{
    /// <summary>Verifies save overwrite, folder creation, and filesystem denial dialog branches.</summary>
    [Fact]
    public async Task File_dialog_save_folder_and_denial_flows_render_in_real_browser()
    {
        string solutionDirectory = FindSolutionDirectory();
        int port = ReservePort();
        string address = $"http://127.0.0.1:{port}";
        using Process server = StartHarness(solutionDirectory, address);

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true });
            IPage page = await browser.NewPageAsync();
            List<string> failures = [];
            page.Console += (_, message) =>
            {
                if (message.Type == "error") failures.Add($"console: {message.Text}");
            };
            page.RequestFailed += (_, request) => failures.Add($"network: {request.Method} {request.Url}");
            await NavigateWhenReadyAsync(page, $"{address}/?scenario=dialog");

            await page.GetByRole(AriaRole.Button, new() { Name = "Save existing file" }).ClickAsync();
            ILocator saveDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Save report" });
            await saveDialog.WaitForAsync();
            await saveDialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
            ILocator overwrite = saveDialog.GetByRole(AriaRole.Alertdialog, new() { Name = "Confirm overwrite" });
            await overwrite.WaitForAsync();
            Assert.Contains("/home/user/readme.txt", await overwrite.InnerTextAsync(), StringComparison.Ordinal);
            await overwrite.GetByRole(AriaRole.Button, new() { Name = "Replace" }).ClickAsync();
            await page.GetByText("Saved:/home/user/readme.txt", new() { Exact = true }).WaitForAsync();

            await page.GetByRole(AriaRole.Button, new() { Name = "Select or create folder" }).ClickAsync();
            ILocator folderDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Choose workspace" });
            await folderDialog.WaitForAsync();
            await folderDialog.GetByRole(AriaRole.Textbox, new() { Name = "New folder name" }).FillAsync("Projects");
            await folderDialog.GetByRole(AriaRole.Button, new() { Name = "New folder" }).ClickAsync();
            ILocator project = folderDialog.GetByRole(AriaRole.Option, new() { Name = "Projects Directory" });
            await project.WaitForAsync();
            await project.ClickAsync();
            Assert.Equal("true", (await project.GetAttributeAsync("aria-selected"))?.ToLowerInvariant());
            await folderDialog.GetByRole(AriaRole.Button, new() { Name = "Select", Exact = true }).ClickAsync();
            await page.GetByText("Folder:/home/user/Projects", new() { Exact = true }).WaitForAsync();

            await page.GetByRole(AriaRole.Button, new() { Name = "Open denied folder" }).ClickAsync();
            ILocator deniedDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Denied folder" });
            await deniedDialog.GetByRole(AriaRole.Alert).WaitForAsync();
            Assert.Contains("PermissionDenied", await deniedDialog.GetByRole(AriaRole.Alert).InnerTextAsync(), StringComparison.Ordinal);
            await deniedDialog.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
            await page.GetByText("DeniedCancelled", new() { Exact = true }).WaitForAsync();
            Assert.Empty(failures);
        }
        finally
        {
            StopProcess(server);
        }
    }

    /// <summary>Verifies the rendered file-open dialog, filtering, multi-select, modality, and cancellation.</summary>
    [Fact]
    public async Task File_dialog_open_integrates_filters_selection_modality_and_escape_cancellation()
    {
        string solutionDirectory = FindSolutionDirectory();
        int port = ReservePort();
        string address = $"http://127.0.0.1:{port}";
        using Process server = StartHarness(solutionDirectory, address);

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true });
            IPage page = await browser.NewPageAsync();
            List<string> failures = [];
            page.Console += (_, message) =>
            {
                if (message.Type == "error") failures.Add($"console: {message.Text}");
            };
            page.RequestFailed += (_, request) => failures.Add($"network: {request.Method} {request.Url}");

            await NavigateWhenReadyAsync(page, $"{address}/?scenario=dialog");
            ILocator owner = page.Locator("[data-app-id='org.hackeros.browser.dialog-owner']").Locator("xpath=ancestor::article");
            await owner.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            ILocator trigger = page.GetByRole(AriaRole.Button, new() { Name = "Open filtered files" });

            await trigger.ClickAsync();
            ILocator dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Choose text files" });
            await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            ILocator modalWindow = dialog.Locator("xpath=ancestor::article");
            Assert.Equal("true", await modalWindow.GetAttributeAsync("aria-modal"));
            Assert.Equal("true", await owner.GetAttributeAsync("aria-hidden"));
            Assert.NotNull(await owner.GetAttributeAsync("inert"));
            await dialog.FocusAsync();
            await page.Keyboard.PressAsync("Escape");
            await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
            await page.GetByText("Cancelled", new() { Exact = true }).WaitForAsync();
            Assert.Equal("false", await owner.GetAttributeAsync("aria-hidden"));
            Assert.Contains("is-focused", await owner.GetAttributeAsync("class"), StringComparison.Ordinal);

            await trigger.ClickAsync();
            dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Choose text files" });
            await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            ILocator options = dialog.GetByRole(AriaRole.Option);
            Assert.Equal(3, await options.CountAsync());
            Assert.Equal(".private.txtFile", (await options.Nth(0).InnerTextAsync()).Replace("\n", string.Empty, StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal));
            Assert.Equal("notes.mdFile", (await options.Nth(1).InnerTextAsync()).Replace("\n", string.Empty, StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal));
            Assert.Equal("readme.txtFile", (await options.Nth(2).InnerTextAsync()).Replace("\n", string.Empty, StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal));
            await options.Nth(1).ClickAsync();
            Assert.Equal("true", (await options.Nth(1).GetAttributeAsync("aria-selected"))?.ToLowerInvariant());
            Assert.Equal("/home/user/notes.md", await dialog.Locator(".selection-summary").InnerTextAsync());
            await options.Nth(2).ClickAsync(new LocatorClickOptions { Modifiers = [KeyboardModifier.Control] });
            await page.WaitForFunctionAsync("() => [...document.querySelectorAll('[role=option]')].filter(element => element.getAttribute('aria-selected')?.toLowerCase() === 'true').length === 2");
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Open", Exact = true }).ClickAsync();
            await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
            await page.GetByText("Selected:/home/user/notes.md,/home/user/readme.txt", new() { Exact = true }).WaitForAsync();
            Assert.Empty(failures);
        }
        finally
        {
            StopProcess(server);
        }
    }

    /// <summary>Verifies keyboard controls, taskbar restore, modality, close, and viewport constraints.</summary>
    [Fact]
    public async Task Window_runtime_handles_keyboard_modality_close_and_viewport_changes()
    {
        string solutionDirectory = FindSolutionDirectory();
        int port = ReservePort();
        string address = $"http://127.0.0.1:{port}";
        using Process server = StartHarness(solutionDirectory, address);

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true });
            IPage page = await browser.NewPageAsync();
            await NavigateWhenReadyAsync(page, $"{address}/?scenario=window");
            ILocator primary = page.Locator("[data-app-id='org.hackeros.browser.primary']").Locator("xpath=ancestor::article");
            await primary.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

            ILocator maximize = primary.GetByLabel("Maximize");
            await maximize.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await page.WaitForFunctionAsync("() => document.querySelector('[data-app-id=\"org.hackeros.browser.primary\"]')?.closest('article')?.dataset.windowWidth === '960'");
            ILocator restore = primary.GetByLabel("Restore");
            await restore.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            Assert.Equal("360", await primary.GetAttributeAsync("data-window-width"));

            ILocator minimize = primary.GetByLabel("Minimize");
            await minimize.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await primary.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
            ILocator taskbarRestore = page.GetByRole(AriaRole.Button, new() { Name = "Restore Primary" });
            await taskbarRestore.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await primary.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

            ILocator openModal = page.GetByRole(AriaRole.Button, new() { Name = "Open Primary Modal" });
            await openModal.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            ILocator modal = page.Locator("[data-app-id='org.hackeros.browser.modal']").Locator("xpath=ancestor::article");
            await modal.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            Assert.Equal("true", await modal.GetAttributeAsync("aria-modal"));
            Assert.Equal("true", await primary.GetAttributeAsync("aria-hidden"));
            Assert.NotNull(await primary.GetAttributeAsync("inert"));

            ILocator closeModal = modal.GetByLabel("Close");
            await closeModal.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await modal.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
            Assert.Equal("false", await primary.GetAttributeAsync("aria-hidden"));
            Assert.Contains("is-focused", await primary.GetAttributeAsync("class"), StringComparison.Ordinal);

            ILocator mobileViewport = page.GetByRole(AriaRole.Button, new() { Name = "Use Mobile Viewport" });
            await mobileViewport.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await page.WaitForFunctionAsync("() => Number(document.querySelector('[data-app-id=\"org.hackeros.browser.primary\"]')?.closest('article')?.dataset.windowWidth) <= 480");
            Assert.True(double.Parse((await primary.GetAttributeAsync("data-window-width"))!, System.Globalization.CultureInfo.InvariantCulture) <= 480);
            Assert.True(double.Parse((await primary.GetAttributeAsync("data-window-height"))!, System.Globalization.CultureInfo.InvariantCulture) <= 320);

            ILocator closePrimary = primary.GetByLabel("Close");
            await closePrimary.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await primary.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
        }
        finally
        {
            StopProcess(server);
        }
    }

    /// <summary>Verifies every rendered resize handle reaches the authoritative C# geometry path.</summary>
    [Fact]
    public async Task Window_runtime_handles_every_resize_edge_in_real_browser()
    {
        string solutionDirectory = FindSolutionDirectory();
        int port = ReservePort();
        string address = $"http://127.0.0.1:{port}";
        using Process server = StartHarness(solutionDirectory, address);
        (string Edge, string X, string Y, string Width, string Height)[] cases =
        [
            ("top", "80", "85", "360", "265"),
            ("right", "80", "70", "380", "280"),
            ("bottom", "80", "70", "360", "295"),
            ("left", "100", "70", "340", "280"),
            ("top-left", "100", "85", "340", "265"),
            ("top-right", "80", "85", "380", "265"),
            ("bottom-right", "80", "70", "380", "295"),
            ("bottom-left", "100", "70", "340", "295"),
        ];

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true });
            IPage page = await browser.NewPageAsync();

            foreach ((string edge, string x, string y, string width, string height) in cases)
            {
                await NavigateWhenReadyAsync(page, $"{address}/?scenario=window");
                ILocator primary = page.Locator("[data-app-id='org.hackeros.browser.primary']").Locator("xpath=ancestor::article");
                await primary.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
                await primary.Locator($"[data-resize-edge='{edge}']").EvaluateAsync(
                    """
                    element => {
                        element.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerId: 51, pointerType: 'pen', clientX: 400, clientY: 200, button: 0 }));
                        element.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, pointerId: 51, pointerType: 'pen', clientX: 420, clientY: 215, buttons: 1 }));
                        element.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, pointerId: 51, pointerType: 'pen', clientX: 420, clientY: 215, button: 0 }));
                    }
                    """);
                await page.WaitForFunctionAsync(
                    "expected => document.querySelector('[data-app-id=\"org.hackeros.browser.primary\"]')?.closest('article')?.dataset.windowWidth === expected",
                    width);

                Assert.Equal(x, await primary.GetAttributeAsync("data-window-x"));
                Assert.Equal(y, await primary.GetAttributeAsync("data-window-y"));
                Assert.Equal(width, await primary.GetAttributeAsync("data-window-width"));
                Assert.Equal(height, await primary.GetAttributeAsync("data-window-height"));
            }
        }
        finally
        {
            StopProcess(server);
        }
    }

    /// <summary>Verifies rendered Window geometry and Pointer Events in real Chrome.</summary>
    [Fact]
    public async Task Window_runtime_renders_and_handles_mouse_and_touch_pointer_gestures()
    {
        string solutionDirectory = FindSolutionDirectory();
        int port = ReservePort();
        string address = $"http://127.0.0.1:{port}";
        using Process server = StartHarness(solutionDirectory, address);

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true });
            await using IBrowserContext context = await browser.NewContextAsync(
                new BrowserNewContextOptions { HasTouch = true, ViewportSize = new() { Width = 1100, Height = 760 } });
            IPage page = await context.NewPageAsync();
            List<string> failures = [];
            page.Console += (_, message) =>
            {
                if (message.Type == "error") failures.Add($"console: {message.Text}");
            };
            page.RequestFailed += (_, request) => failures.Add($"network: {request.Method} {request.Url}");

            await NavigateWhenReadyAsync(page, $"{address}/?scenario=window");
            ILocator scenario = page.Locator(".window-browser-scenario");
            await scenario.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            ILocator primary = page.Locator("[data-app-id='org.hackeros.browser.primary']").Locator("xpath=ancestor::article");
            ILocator secondary = page.Locator("[data-app-id='org.hackeros.browser.secondary']").Locator("xpath=ancestor::article");
            await primary.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

            ILocator title = primary.Locator("[data-window-gesture='move']");
            var titleBox = (await title.BoundingBoxAsync())!;
            await page.Mouse.MoveAsync(titleBox.X + 30, titleBox.Y + 20);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync(titleBox.X + 90, titleBox.Y + 60, new MouseMoveOptions { Steps = 4 });
            await page.Mouse.UpAsync();
            await page.WaitForFunctionAsync("() => Number(document.querySelector('[data-app-id=\"org.hackeros.browser.primary\"]')?.closest('article')?.dataset.windowX) > 80");

            float renderedX = float.Parse((await primary.GetAttributeAsync("data-window-x"))!, System.Globalization.CultureInfo.InvariantCulture);
            float renderedY = float.Parse((await primary.GetAttributeAsync("data-window-y"))!, System.Globalization.CultureInfo.InvariantCulture);
            var movedBox = (await primary.BoundingBoxAsync())!;
            string projection = await primary.EvaluateAsync<string>(
                "element => JSON.stringify({ inline: element.getAttribute('style'), top: getComputedStyle(element).top, left: getComputedStyle(element).left, position: getComputedStyle(element).position })");
            Assert.InRange(movedBox.X, renderedX - 0.5F, renderedX + 0.5F);
            Assert.InRange(movedBox.Y, renderedY - 0.5F, renderedY + 0.5F);
            int primaryZ = int.Parse((await primary.GetAttributeAsync("data-window-z"))!);
            int secondaryZ = int.Parse((await secondary.GetAttributeAsync("data-window-z"))!);
            Assert.True(primaryZ > secondaryZ, $"Primary z={primaryZ}; secondary z={secondaryZ}.");

            await primary.Locator("[data-resize-edge='right']").EvaluateAsync(
                """
                element => {
                    element.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerId: 41, pointerType: 'touch', clientX: 499, clientY: 190, button: 0 }));
                    element.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, pointerId: 41, pointerType: 'touch', clientX: 549, clientY: 190, buttons: 1 }));
                    element.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, pointerId: 41, pointerType: 'touch', clientX: 549, clientY: 190, button: 0 }));
                }
                """);
            await page.WaitForFunctionAsync("() => document.querySelector('[data-app-id=\"org.hackeros.browser.primary\"]')?.closest('article')?.dataset.windowWidth === '410'");
            Assert.Equal("410", await primary.GetAttributeAsync("data-window-width"));
            Assert.InRange((await primary.BoundingBoxAsync())!.Width, 409.5, 410.5);

            await primary.GetByLabel("Maximize").ClickAsync();
            Assert.Equal("960", await primary.GetAttributeAsync("data-window-width"));
            await primary.GetByLabel("Restore").ClickAsync();
            Assert.Equal("410", await primary.GetAttributeAsync("data-window-width"));
            Assert.Empty(failures);
        }
        finally
        {
            StopProcess(server);
        }
    }

    /// <summary>Verifies group and settings contracts through the published browser module.</summary>
    [Fact]
    public async Task Group_and_settings_contracts_pass_in_real_browser()
    {
        string solutionDirectory = FindSolutionDirectory();
        int port = ReservePort();
        string address = $"http://127.0.0.1:{port}";
        using Process server = StartHarness(solutionDirectory, address);

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Channel = "chrome",
                    Headless = true
                });
            IPage page = await browser.NewPageAsync();
            await NavigateWhenReadyAsync(page, address);

            ILocator result = page.Locator("#contract-result");
            await result.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await page.WaitForFunctionAsync(
                "() => ['passed', 'failed'].includes(document.querySelector('#contract-result')?.dataset.status)");

            string? status = await result.GetAttributeAsync("data-status");
            string message = (await result.TextContentAsync()) ?? "Harness returned no result message.";
            Assert.True(status == "passed", message);

            await page.EvaluateAsync("() => history.replaceState({}, '', '/?scenario=reload')");
            await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.WaitForFunctionAsync(
                "() => ['passed', 'failed'].includes(document.querySelector('#contract-result')?.dataset.status)");

            status = await result.GetAttributeAsync("data-status");
            message = (await result.TextContentAsync()) ?? "Reload harness returned no result message.";
            Assert.True(status == "passed", message);
            Assert.Contains("survived a page reload", message, StringComparison.Ordinal);
        }
        finally
        {
            StopProcess(server);
        }
    }

    /// <summary>Verifies native rollback for failed writes and failed schema upgrades.</summary>
    [Fact]
    public async Task Failed_transaction_and_migration_preserve_prior_committed_state()
    {
        string solutionDirectory = FindSolutionDirectory();
        int port = ReservePort();
        string address = $"http://127.0.0.1:{port}";
        using Process server = StartHarness(solutionDirectory, address);

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true });
            IPage page = await browser.NewPageAsync();
            await NavigateWhenReadyAsync(page, address);

            JsonElement result = await page.EvaluateAsync<JsonElement>(
                """
                async () => {
                    const module = await import('/_content/HackerOs.Infrastructure.Browser/indexedDb.js');
                    const createV1 = {
                        steps: [{
                            targetVersion: 1,
                            createObjectStores: [{
                                name: 'records',
                                keyPath: ['id'],
                                autoIncrement: false,
                                indexes: []
                            }]
                        }]
                    };

                    const rollbackDatabase = `hackeros-rollback-${crypto.randomUUID()}`;
                    await module.openDatabase(rollbackDatabase, 1, createV1);
                    let transactionRejected = false;
                    try {
                        await module.executeTransaction(
                            rollbackDatabase,
                            1,
                            ['records'],
                            'readwrite',
                            [
                                { kind: 'add', objectStoreName: 'records', value: { id: 'same' } },
                                { kind: 'add', objectStoreName: 'records', value: { id: 'same' } }
                            ]);
                    } catch {
                        transactionRejected = true;
                    }
                    const rollbackCount = (await module.executeTransaction(
                        rollbackDatabase,
                        1,
                        ['records'],
                        'readonly',
                        [{ kind: 'count', objectStoreName: 'records' }]))[0];
                    await module.deleteDatabase(rollbackDatabase);

                    const migrationDatabase = `hackeros-migration-${crypto.randomUUID()}`;
                    await module.openDatabase(migrationDatabase, 1, createV1);
                    await module.executeTransaction(
                        migrationDatabase,
                        1,
                        ['records'],
                        'readwrite',
                        [{ kind: 'add', objectStoreName: 'records', value: { id: 'committed' } }]);
                    let migrationRejected = false;
                    try {
                        await module.openDatabase(migrationDatabase, 2, {
                            steps: [{
                                targetVersion: 2,
                                createIndexes: [{
                                    objectStoreName: 'missing-store',
                                    index: { name: 'invalid', keyPath: ['value'], unique: false }
                                }]
                            }]
                        });
                    } catch {
                        migrationRejected = true;
                    }
                    await module.openDatabase(migrationDatabase, 1, createV1);
                    const retainedCount = (await module.executeTransaction(
                        migrationDatabase,
                        1,
                        ['records'],
                        'readonly',
                        [{ kind: 'count', objectStoreName: 'records' }]))[0];
                    await module.deleteDatabase(migrationDatabase);

                    return { transactionRejected, rollbackCount, migrationRejected, retainedCount };
                }
                """);

            Assert.True(result.GetProperty("transactionRejected").GetBoolean());
            Assert.Equal(0, result.GetProperty("rollbackCount").GetInt32());
            Assert.True(result.GetProperty("migrationRejected").GetBoolean());
            Assert.Equal(1, result.GetProperty("retainedCount").GetInt32());
        }
        finally
        {
            StopProcess(server);
        }
    }

    /// <summary>Verifies that two tabs cannot both commit the same expected revision.</summary>
    [Fact]
    public async Task Multi_tab_revision_conflict_allows_exactly_one_commit()
    {
        string solutionDirectory = FindSolutionDirectory();
        int port = ReservePort();
        string address = $"http://127.0.0.1:{port}";
        using Process server = StartHarness(solutionDirectory, address);

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true });
            IBrowserContext context = await browser.NewContextAsync();
            IPage firstPage = await context.NewPageAsync();
            IPage secondPage = await context.NewPageAsync();
            await Task.WhenAll(
                NavigateWhenReadyAsync(firstPage, $"{address}/?scenario=idle"),
                NavigateWhenReadyAsync(secondPage, $"{address}/?scenario=idle"));

            string databaseName = $"hackeros-tabs-{Guid.NewGuid():N}";
            const string setupScript = """
                async ({ databaseName }) => {
                    const module = await import('/_content/HackerOs.Infrastructure.Browser/indexedDb.js');
                    await module.openDatabase(databaseName, 1, {
                        steps: [{
                            targetVersion: 1,
                            createObjectStores: [{
                                name: 'records',
                                keyPath: ['id'],
                                autoIncrement: false,
                                indexes: []
                            }]
                        }]
                    });
                    await module.executeTransaction(databaseName, 1, ['records'], 'readwrite', [{
                        kind: 'add',
                        objectStoreName: 'records',
                        value: { id: 'shared', revision: 1, value: 'initial' }
                    }]);
                }
                """;
            await firstPage.EvaluateAsync(setupScript, new { databaseName });

            const string updateScript = """
                async ({ databaseName, value }) => {
                    const module = await import('/_content/HackerOs.Infrastructure.Browser/indexedDb.js');
                    await module.openDatabase(databaseName, 1, { steps: [] });
                    const result = await module.executeTransaction(
                        databaseName,
                        1,
                        ['records'],
                        'readwrite',
                        [{
                            kind: 'compareAndPut',
                            objectStoreName: 'records',
                            key: 'shared',
                            value: { id: 'shared', revision: 2, value },
                            compareProperty: 'revision',
                            expectedValue: 1
                        }]);
                    return result[0].committed;
                }
                """;
            Task<bool> firstUpdate = firstPage.EvaluateAsync<bool>(
                updateScript,
                new { databaseName, value = "first" });
            Task<bool> secondUpdate = secondPage.EvaluateAsync<bool>(
                updateScript,
                new { databaseName, value = "second" });
            bool[] commits = await Task.WhenAll(firstUpdate, secondUpdate);

            Assert.Single(commits, committed => committed);
            JsonElement finalRecord = await firstPage.EvaluateAsync<JsonElement>(
                """
                async ({ databaseName }) => {
                    const module = await import('/_content/HackerOs.Infrastructure.Browser/indexedDb.js');
                    const result = await module.executeTransaction(
                        databaseName,
                        1,
                        ['records'],
                        'readonly',
                        [{ kind: 'get', objectStoreName: 'records', key: 'shared' }]);
                    await module.deleteDatabase(databaseName);
                    return result[0];
                }
                """,
                new { databaseName });
            Assert.Equal(2, finalRecord.GetProperty("revision").GetInt32());
            Assert.Contains(
                finalRecord.GetProperty("value").GetString(),
                new[] { "first", "second" });
        }
        finally
        {
            StopProcess(server);
        }
    }

    /// <summary>Verifies native Chromium quota failure reaches the recoverable C# exception.</summary>
    [Fact]
    public async Task Quota_exhaustion_is_reported_as_recoverable_storage_failure()
    {
        string solutionDirectory = FindSolutionDirectory();
        int port = ReservePort();
        string address = $"http://127.0.0.1:{port}";
        using Process server = StartHarness(solutionDirectory, address);

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true });
            IBrowserContext context = await browser.NewContextAsync();
            IPage page = await context.NewPageAsync();
            await NavigateWhenReadyAsync(page, $"{address}/?scenario=idle");

            ICDPSession session = await context.NewCDPSessionAsync(page);
            await session.SendAsync(
                "Storage.overrideQuotaForOrigin",
                new Dictionary<string, object>
                {
                    ["origin"] = address,
                    ["quotaSize"] = 1
                });

            await page.GotoAsync(
                $"{address}/?scenario=quota",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            ILocator result = page.Locator("#contract-result");
            await page.WaitForFunctionAsync(
                "() => ['passed', 'failed'].includes(document.querySelector('#contract-result')?.dataset.status)");

            string? status = await result.GetAttributeAsync("data-status");
            string message = (await result.TextContentAsync()) ?? "Quota harness returned no result message.";
            Assert.True(status == "passed", message);
            Assert.Contains("BrowserStorageQuotaException", message, StringComparison.Ordinal);
        }
        finally
        {
            StopProcess(server);
        }
    }

    /// <summary>Verifies representative MudBlazor wrappers on desktop and mobile viewports.</summary>
    [Fact]
    public async Task Platform_complex_controls_are_interactive_accessible_and_responsive()
    {
        string solutionDirectory = FindSolutionDirectory();
        int port = ReservePort();
        string address = $"http://127.0.0.1:{port}";
        using Process server = StartHarness(solutionDirectory, address);

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true });
            IPage page = await browser.NewPageAsync(new BrowserNewPageOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
            });
            List<string> browserFailures = [];
            page.Console += (_, message) =>
            {
                if (message.Type == "error")
                {
                    browserFailures.Add($"console: {message.Text}");
                }
            };
            page.RequestFailed += (_, request) =>
                browserFailures.Add($"network: {request.Method} {request.Url}");

            await NavigateWhenReadyAsync(page, $"{address}/?scenario=idle");
            await page.GetByRole(AriaRole.Heading, new() { Name = "Control surface" }).WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Actions" }).ClickAsync();
            await page.GetByRole(AriaRole.Menuitem, new() { Name = "Audit storage" }).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Status).Last)
                .ToContainTextAsync("Audit storage selected.");

            await page.GetByRole(AriaRole.Tab, new() { Name = "Operator" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Validate" }).ClickAsync();
            await Assertions.Expect(page.GetByText("Operator form requires attention."))
                .ToBeVisibleAsync();
            await page.GetByLabel("Operator name").FillAsync("root");
            await page.GetByRole(AriaRole.Button, new() { Name = "Validate" }).ClickAsync();
            await Assertions.Expect(page.GetByText("Operator form is valid."))
                .ToBeVisibleAsync();

            await page.SetViewportSizeAsync(375, 812);
            bool hasHorizontalOverflow = await page.EvaluateAsync<bool>(
                "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
            Assert.False(hasHorizontalOverflow);
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Control surface" }))
                .ToBeVisibleAsync();

            string screenshotPath = Path.Combine(
                Path.GetTempPath(),
                $"hackeros-platform-ui-{Guid.NewGuid():N}.png");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });
            Assert.True(new FileInfo(screenshotPath).Length > 0);
            File.Delete(screenshotPath);
            Assert.Empty(browserFailures);
        }
        finally
        {
            StopProcess(server);
        }
    }

    private static async Task NavigateWhenReadyAsync(IPage page, string address)
    {
        Exception? lastFailure = null;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                await page.GotoAsync(address, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
                return;
            }
            catch (PlaywrightException exception)
            {
                lastFailure = exception;
                await Task.Delay(100);
            }
        }

        throw new InvalidOperationException("The browser harness did not become ready.", lastFailure);
    }

    private static Process StartHarness(string solutionDirectory, string address)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = solutionDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add("Tests/HackerOs.BrowserHarness.Tests/HackerOs.BrowserHarness.Tests.csproj");
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(address);

        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the browser harness.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static string FindSolutionDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HackerOs.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate HackerOs.sln.");
    }

    private static int ReservePort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void StopProcess(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
        }
    }
}