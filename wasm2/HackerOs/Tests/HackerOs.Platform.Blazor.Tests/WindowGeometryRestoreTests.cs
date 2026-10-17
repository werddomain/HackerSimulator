using HackerOs.App.Abstractions;
using HackerOs.Platform.Blazor.Windows;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Windowing.Abstractions;
using HackerOs.Windowing.Core;
using static HackerOs.Platform.Blazor.Tests.WindowGeometryPersistenceTests;

namespace HackerOs.Platform.Blazor.Tests;

/// <summary>
/// Verifies ADR 0116: app windows remember their size, position and maximized state, are corrected
/// when the screen or the app's declared sizes changed, and never remember when the app opts out.
/// </summary>
public sealed class WindowGeometryRestoreTests
{
    private const string AppId = "org.hackeros.test-window";
    private const string StateFile = "windows/org.hackeros.test-window.config";
    private static readonly WindowBounds Screen = new(0, 0, 1280, 720);

    [Fact]
    public async Task A_moved_and_resized_window_reopens_at_the_same_place()
    {
        Desktop desktop = new();
        WindowId first = await desktop.OpenAsync();
        desktop.Runtime.Apply(new MoveWindowCommand(first, 300, 120));
        desktop.Runtime.Apply(new ResizeWindowCommand(first, new WindowBounds(300, 120, 640, 420)));
        desktop.Runtime.Apply(new CompleteWindowResizeCommand(first));
        await desktop.CloseAsync(first);

        Assert.Equal(
            "# Window geometry of org.hackeros.test-window (ADR 0116)\nx=300\ny=120\nwidth=640\nheight=420\nmaximized=false\n",
            desktop.Settings.Files[StateFile]);

        WindowId reopened = await desktop.OpenAsync();
        Assert.Equal(new WindowBounds(300, 120, 640, 420), desktop.Get(reopened).Bounds);
    }

    [Fact]
    public async Task A_new_desktop_session_reads_the_file_written_by_the_previous_one()
    {
        // Reloading the page recreates the runtime, the recorder and the coordinator.
        Desktop before = new();
        WindowId window = await before.OpenAsync();
        before.Runtime.Apply(new MoveWindowCommand(window, 200, 90));
        await before.Recorder.WhenIdleAsync();

        Desktop after = new(before.Settings);
        WindowId reopened = await after.OpenAsync();

        Assert.Equal(200, after.Get(reopened).Bounds.X);
        Assert.Equal(90, after.Get(reopened).Bounds.Y);
    }

    [Fact]
    public async Task A_maximized_window_reopens_maximized_with_its_restore_geometry()
    {
        Desktop desktop = new();
        WindowId window = await desktop.OpenAsync();
        desktop.Runtime.Apply(new MoveWindowCommand(window, 150, 60));
        desktop.Runtime.Apply(new MaximizeWindowCommand(window));
        await desktop.CloseAsync(window);

        Assert.EndsWith("x=150\ny=60\nwidth=800\nheight=560\nmaximized=true\n", desktop.Settings.Files[StateFile], StringComparison.Ordinal);

        WindowRuntimeState reopened = desktop.Get(await desktop.OpenAsync());
        Assert.Equal(WindowVisualState.Maximized, reopened.VisualState);
        Assert.Equal(Screen, reopened.Bounds);
        Assert.Equal(new WindowBounds(150, 60, 800, 560), reopened.RestoreBounds);
    }

    [Fact]
    public async Task A_minimized_or_snapped_window_keeps_its_last_normal_geometry()
    {
        Desktop desktop = new();
        WindowId window = await desktop.OpenAsync();
        desktop.Runtime.Apply(new MoveWindowCommand(window, 100, 100));
        desktop.Runtime.Apply(new ResizeWindowCommand(window, new WindowBounds(0, 0, 640, 720)));
        desktop.Runtime.Apply(new MinimizeWindowCommand(window));
        await desktop.CloseAsync(window);

        Assert.EndsWith("x=100\ny=100\nwidth=800\nheight=560\nmaximized=false\n", desktop.Settings.Files[StateFile], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Moves_of_one_drag_are_grouped_into_one_write()
    {
        Desktop desktop = new(debounce: TimeSpan.FromMilliseconds(50));
        WindowId window = await desktop.OpenAsync();
        for (int x = 100; x < 200; x += 10)
        {
            desktop.Runtime.Apply(new MoveWindowCommand(window, x, 80));
        }

        await desktop.Recorder.WhenIdleAsync();

        Assert.Equal(1, desktop.Settings.Writes);
        Assert.Contains("x=190\n", desktop.Settings.Files[StateFile], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Opening_a_window_flushes_a_write_still_waiting_for_quiet()
    {
        // A second window opened before the grouping delay elapsed still sees the first one's move.
        Desktop desktop = new(manifest: Manifest(null) with { SingleInstancePerUser = false }, debounce: TimeSpan.FromHours(1));
        WindowId first = await desktop.OpenAsync();
        desktop.Runtime.Apply(new MoveWindowCommand(first, 250, 70));

        WindowId second = await desktop.OpenAsync();

        Assert.Equal(new WindowBounds(278, 98, 800, 560), desktop.Get(second).Bounds);
        Assert.Equal(1, desktop.Settings.Writes);
    }

    [Fact]
    public async Task An_app_that_opts_out_always_opens_at_its_cascade_slot()
    {
        Desktop desktop = new(manifest: Manifest(new WindowOptions { RememberGeometry = false }));
        desktop.Settings.Files[StateFile] = "x=400\ny=300\nwidth=500\nheight=400\n";
        WindowId window = await desktop.OpenAsync();
        desktop.Runtime.Apply(new MoveWindowCommand(window, 10, 10));
        await desktop.CloseAsync(window);

        Assert.Equal(WindowLaunchCoordinator.InitialBounds(WindowOptions.Default, 0, Screen), desktop.Get(await desktop.OpenAsync()).Bounds);
        Assert.Equal("x=400\ny=300\nwidth=500\nheight=400\n", desktop.Settings.Files[StateFile]);
        Assert.Equal(0, desktop.Settings.Writes);
    }

    [Fact]
    public async Task An_invalid_file_opens_the_window_at_its_cascade_slot()
    {
        Desktop desktop = new();
        desktop.Settings.Files[StateFile] = "x=garbage\n";

        WindowRuntimeState window = desktop.Get(await desktop.OpenAsync());

        Assert.Equal(WindowLaunchCoordinator.InitialBounds(WindowOptions.Default, 0, Screen), window.Bounds);
    }

    [Fact]
    public async Task A_second_window_of_the_same_app_opens_offset_from_the_saved_place()
    {
        Desktop desktop = new(manifest: Manifest(null) with { SingleInstancePerUser = false });
        desktop.Settings.Files[StateFile] = "x=200\ny=100\nwidth=600\nheight=400\n";

        WindowId first = await desktop.OpenAsync();
        WindowId second = await desktop.OpenAsync();

        Assert.Equal(new WindowBounds(200, 100, 600, 400), desktop.Get(first).Bounds);
        Assert.Equal(new WindowBounds(228, 128, 600, 400), desktop.Get(second).Bounds);
    }

    [Fact]
    public async Task Presenting_the_same_process_twice_while_reading_opens_one_window()
    {
        Desktop desktop = new();
        ProcessRecord process = CreateProcess();

        Task<bool> first = desktop.Coordinator.PresentAsync(desktop.Manifest, process);
        Task<bool> second = desktop.Coordinator.PresentAsync(desktop.Manifest, process);

        Assert.True(await first);
        Assert.True(await second);
        Assert.Single(desktop.Runtime.Windows);
    }

    [Fact]
    public void A_smaller_screen_brings_the_window_back_on_screen()
    {
        // Saved on a 1920x1080 screen, reopened on 1024x600: the size shrinks and the title bar stays reachable.
        WindowBounds bounds = WindowLaunchCoordinator.RestoredBounds(
            WindowOptions.Default,
            new WindowBounds(1500, 900, 1200, 800),
            sameAppWindows: 0,
            new WindowBounds(0, 0, 1024, 600));

        Assert.Equal(1024, bounds.Width);
        Assert.Equal(600, bounds.Height);
        Assert.Equal(1024 - WindowLaunchCoordinator.MinimumVisibleTitleBarWidth, bounds.X);
        Assert.Equal(600 - 38, bounds.Y);
    }

    [Fact]
    public void A_position_left_of_the_screen_keeps_part_of_the_title_bar_visible()
    {
        WindowBounds bounds = WindowLaunchCoordinator.RestoredBounds(
            WindowOptions.Default, new WindowBounds(-2000, -50, 500, 400), 0, Screen);

        Assert.Equal(-500 + WindowLaunchCoordinator.MinimumVisibleTitleBarWidth, bounds.X);
        Assert.Equal(0, bounds.Y);
    }

    [Fact]
    public void Saved_sizes_follow_the_sizes_the_app_declares_today()
    {
        WindowOptions options = new() { DefaultWidth = 700, DefaultHeight = 500, MinWidth = 600, MinHeight = 450, MaxWidth = 900, MaxHeight = 650 };

        Assert.Equal(
            new WindowBounds(10, 10, 600, 650),
            WindowLaunchCoordinator.RestoredBounds(options, new WindowBounds(10, 10, 400, 1000), 0, Screen));
        Assert.Equal(
            new WindowBounds(10, 10, 700, 500),
            WindowLaunchCoordinator.RestoredBounds(options with { Resizable = false }, new WindowBounds(10, 10, 800, 600), 0, Screen));
    }

    [Fact]
    public async Task A_fixed_size_window_never_reopens_maximized()
    {
        Desktop desktop = new(manifest: Manifest(new WindowOptions { Resizable = false }));
        desktop.Settings.Files[StateFile] = "x=20\ny=30\nwidth=800\nheight=560\nmaximized=true\n";

        WindowRuntimeState window = desktop.Get(await desktop.OpenAsync());

        Assert.Equal(WindowVisualState.Normal, window.VisualState);
        Assert.Equal(new WindowBounds(20, 30, 800, 560), window.Bounds);
    }

    [Fact]
    public async Task Os_windows_that_are_not_tracked_are_never_written()
    {
        Desktop desktop = new();
        WindowRuntimeState dialog = new(
            WindowId.FromGuid(Guid.NewGuid()),
            "org.hackeros.file-dialog",
            WindowOwnerId.FromGuid(Guid.NewGuid()),
            "Open",
            null,
            new WindowBounds(10, 10, 400, 300),
            restoreBounds: null,
            zOrder: 0,
            visualState: WindowVisualState.Normal,
            constraints: new WindowConstraints(true, 200, 150, null, null),
            modality: WindowModality.Modeless);
        desktop.Runtime.Apply(new CreateWindowCommand(dialog));
        desktop.Runtime.Apply(new MoveWindowCommand(dialog.Id, 50, 50));
        desktop.Runtime.Apply(new ForceWindowCloseCommand(dialog.Id));
        await desktop.Recorder.WhenIdleAsync();

        Assert.Empty(desktop.Settings.Files);
    }

    private static AppManifest Manifest(WindowOptions? window) => new()
    {
        Id = AppId,
        Name = "Test Window",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Window geometry test app.",
        Kind = AppKind.Window,
        EntryPoint = new AppEntryPointManifest("Test.dll", "Test.Window"),
        SdkCompatibility = new AppSdkCompatibilityManifest(HackerOsSdk.Version),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        Window = window,
    };

    private static ProcessRecord CreateProcess()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new ProcessRecord(
            ProcessId.FromInt64(42),
            parentPid: null,
            AppId,
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

    /// <summary>One desktop session: runtime, recorder and coordinator over a shared settings store.</summary>
    private sealed class Desktop
    {
        public Desktop(FakeOsSettingsService? settings = null, AppManifest? manifest = null, TimeSpan? debounce = null)
        {
            Settings = settings ?? new FakeOsSettingsService();
            Manifest = manifest ?? WindowGeometryRestoreTests.Manifest(null);
            Runtime = new WindowRuntime(Screen);
            Recorder = new WindowGeometryRecorder(Runtime, new WindowGeometryPersistence(Settings), debounce ?? TimeSpan.Zero);
            Coordinator = new WindowLaunchCoordinator(Runtime, Recorder);
        }

        public FakeOsSettingsService Settings { get; }

        public AppManifest Manifest { get; }

        public WindowRuntime Runtime { get; }

        public WindowGeometryRecorder Recorder { get; }

        public WindowLaunchCoordinator Coordinator { get; }

        public async Task<WindowId> OpenAsync()
        {
            ProcessRecord process = CreateProcess();
            Assert.True(await Coordinator.PresentAsync(Manifest, process));
            WindowOwnerId owner = WindowOwnerId.FromGuid(process.AppInstanceId.Value);
            return Runtime.Windows.Single(window => window.OwnerInstanceId == owner).Id;
        }

        public async Task CloseAsync(WindowId window)
        {
            Runtime.Apply(new ForceWindowCloseCommand(window));
            await Recorder.WhenIdleAsync();
        }

        public WindowRuntimeState Get(WindowId window) => Runtime.Windows.Single(candidate => candidate.Id == window);
    }
}
