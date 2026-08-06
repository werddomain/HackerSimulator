using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;
using Xunit;

namespace HackerOs.UI.E2E.Tests;

public sealed class FileExplorerDownloadTests
{
    [Fact]
    public async Task File_explorer_can_download_folders_as_zip()
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
            
            // Allow downloads for the context
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions { AcceptDownloads = true });
            IPage page = await context.NewPageAsync();
            
            List<string> failures = [];
            page.Console += (_, message) =>
            {
                if (message.Type == "error") failures.Add($"console: {message.Text}");
            };
            
            await NavigateWhenReadyAsync(page, address);
            
            // Initial Setup
            ILocator setupLogin = page.GetByLabel("Login name");
            await setupLogin.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            
            await setupLogin.FillAsync("admin");
            await page.GetByLabel("Display name").FillAsync("Administrator");
            await page.GetByLabel("Password", new() { Exact = true }).FillAsync("Admin123");
            await page.GetByLabel("Confirm password").FillAsync("Admin123");
            
            await page.GetByRole(AriaRole.Button, new() { Name = "Create local profile" }).ClickAsync();
            
            // Wait for desktop to boot up (Taskbar should be visible)
            ILocator appLauncherBtn = page.GetByRole(AriaRole.Button, new() { Name = "App launcher" });
            await appLauncherBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
            
            // Open App Launcher
            await appLauncherBtn.ClickAsync();
            
            // Search for File Explorer
            await page.GetByRole(AriaRole.Textbox, new() { Name = "Search applications" }).FillAsync("File Explorer");
            
            // Select File Explorer
            ILocator fileExplorerOption = page.GetByRole(AriaRole.Option, new() { NameString = "File Explorer", Exact = false });
            await fileExplorerOption.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await fileExplorerOption.ClickAsync();
            
            // Wait for File Explorer window to appear (wait for Actions toolbar or download button)
            ILocator downloadBtn = page.GetByRole(AriaRole.Button, new() { Name = "Download" });
            await downloadBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            
            await Task.Delay(2000);
            await page.ScreenshotAsync(new() { Path = "screenshot_before.png" });

            // Navigate to root (/) by clicking "Up" button until we see "bin"
            ILocator upBtn = page.GetByTitle("Up");
            await upBtn.ClickAsync(); // Go to /home
            await upBtn.ClickAsync(); // Go to /
            await Task.Delay(1000);
            
            await page.ScreenshotAsync(new() { Path = "screenshot.png" });
            
            // Select the bin directory just to download something small
            ILocator targetDirItem = page.GetByText("bin", new() { Exact = false }).First;
            await targetDirItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await targetDirItem.ClickAsync();
            
            // Run download
            var download = await page.RunAndWaitForDownloadAsync(async () =>
            {
                await downloadBtn.ClickAsync();
            });
            
            // Save to temp file
            string tempFile = Path.GetTempFileName();
            await download.SaveAsAsync(tempFile);
            
            // Inspect zip file
            Assert.True(File.Exists(tempFile));
            using (FileStream fs = new FileStream(tempFile, FileMode.Open))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                // Verify it's a valid zip archive, and it should contain at least an empty structure or files depending on the default OS state.
                Assert.NotNull(archive);
            }
            
            File.Delete(tempFile);
            Assert.Empty(failures);
        }
        finally
        {
            StopProcess(server);
        }
    }

    private static async Task NavigateWhenReadyAsync(IPage page, string address)
    {
        Exception? lastFailure = null;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                await page.GotoAsync(address, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
                return;
            }
            catch (PlaywrightException exception)
            {
                lastFailure = exception;
                await Task.Delay(500);
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
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add("test/test/test.csproj");
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
