using HackerOs.App.Abstractions;
using HackerOs.Platform.Blazor.Windows;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Blazor.Tests;

/// <summary>Verifies successful app processes are represented by desktop and taskbar windows.</summary>
public sealed class WindowLaunchCoordinatorTests
{
    [Fact]
    public void Present_creates_window_for_successful_window_launch()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 1280, 720));
        WindowLaunchCoordinator coordinator = new(runtime);
        AppManifest manifest = CreateManifest();
        ProcessRecord process = CreateProcess();

        bool presented = coordinator.Present(
            manifest,
            new AppLaunchResult(AppLaunchStatus.Launched, process));

        Assert.True(presented);
        WindowRuntimeState window = Assert.Single(runtime.Windows);
        Assert.Equal(manifest.Id, window.AppId);
        Assert.Equal(process.Pid, window.ProcessId);
        Assert.Equal(process.AppInstanceId, window.AppInstanceId);
        Assert.True(window.IsFocused);
    }

    [Fact]
    public void Present_restores_existing_singleton_window_without_duplicating_it()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 1280, 720));
        WindowLaunchCoordinator coordinator = new(runtime);
        AppManifest manifest = CreateManifest();
        ProcessRecord process = CreateProcess();
        AppLaunchResult launch = new(AppLaunchStatus.Launched, process);
        coordinator.Present(manifest, launch);
        WindowId windowId = Assert.Single(runtime.Windows).Id;
        runtime.Apply(new MinimizeWindowCommand(windowId));

        bool presented = coordinator.Present(
            manifest,
            new AppLaunchResult(AppLaunchStatus.FocusedExisting, process));

        Assert.True(presented);
        WindowRuntimeState window = Assert.Single(runtime.Windows);
        Assert.Equal(WindowVisualState.Normal, window.VisualState);
        Assert.True(window.IsFocused);
    }

    private static AppManifest CreateManifest() => new()
    {
        Id = "org.hackeros.test-window",
        Name = "Test Window",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Window launch coordinator test app.",
        Kind = AppKind.Window,
        EntryPoint = new AppEntryPointManifest("Test.dll", "Test.Window"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        SingleInstancePerUser = true
    };

    private static ProcessRecord CreateProcess()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new ProcessRecord(
            ProcessId.FromInt64(42),
            parentPid: null,
            "org.hackeros.test-window",
            AppInstanceId.FromGuid(Guid.NewGuid()),
            AppKind.Window,
            LocalUserId.FromGuid(Guid.NewGuid()),
            SessionId.FromGuid(Guid.NewGuid()),
            ProcessState.Running,
            ResourceProfile.None,
            serviceHealth: null,
            now,
            startedAtUtc: now,
            stoppedAtUtc: null,
            exitCode: null,
            exitReason: null);
    }
}
