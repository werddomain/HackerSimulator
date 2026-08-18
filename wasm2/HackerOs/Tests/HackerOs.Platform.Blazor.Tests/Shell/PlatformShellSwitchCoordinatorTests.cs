using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk.Blazor;
using HackerOs.Platform.Blazor.Shell;
using HackerOs.Platform.Blazor.Windows;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Discovery;
using HackerOs.Platform.Core.Events;
using HackerOs.Platform.Core.Execution;
using HackerOs.Platform.Core.FileSystem;
using HackerOs.Platform.Core.Diagnostics;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Platform.Core.Notifications;
using HackerOs.Platform.Core.Policy;
using HackerOs.Platform.Core.Processes;
using HackerOs.Platform.Core.Sessions;
using HackerOs.Platform.Core.Shell;
using HackerOs.Platform.Core.Time;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Windowing.Core;
using HackerOs.Windowing.Abstractions;

namespace HackerOs.Platform.Blazor.Tests.Shell;

/// <summary>
/// Tests for the <c>MOB-008</c> controlled platform-switch sequence: confirm dirty windows, stop
/// their instances with <see cref="ProcessExitReason.PlatformChanged"/>, then persist.
/// </summary>
public sealed class PlatformShellSwitchCoordinatorTests
{
    [Fact]
    public async Task RequestExplicitAsync_confirms_stops_and_persists_when_every_window_agrees()
    {
        Fixture fixture = new();
        AppLaunchResult launch = await fixture.LaunchWindowAppAsync();
        WindowId windowId = fixture.CreateWindowFor(launch);
        AlwaysConfirmGuard guard = new();
        using IDisposable registration = fixture.CloseGuards.Register(windowId, guard);
        await fixture.PlatformPreference.InitializeAsync();
        PlatformShellSwitchCoordinator coordinator = fixture.CreateCoordinator();

        bool switched = await coordinator.RequestExplicitAsync(WellKnownAppPlatforms.Mobile);

        Assert.True(switched);
        Assert.Empty(fixture.WindowRuntime.Windows);
        Assert.False(fixture.Manager.TryGetActive(launch.Process!.Pid, out _));
        ProcessRecord history = Assert.Single(fixture.Manager.GetHistory(), item => item.Pid == launch.Process.Pid);
        Assert.Equal(ProcessExitReason.PlatformChanged, history.ExitReason);
        Assert.Equal(WellKnownAppPlatforms.Mobile, fixture.PlatformPreference.Current.ActivePlatform);
    }

    [Fact]
    public async Task RequestExplicitAsync_leaves_everything_untouched_when_a_window_rejects()
    {
        Fixture fixture = new();
        AppLaunchResult launch = await fixture.LaunchWindowAppAsync();
        WindowId windowId = fixture.CreateWindowFor(launch);
        RejectingGuard guard = new();
        using IDisposable registration = fixture.CloseGuards.Register(windowId, guard);
        await fixture.PlatformPreference.InitializeAsync();
        PlatformShellSwitchCoordinator coordinator = fixture.CreateCoordinator();

        bool switched = await coordinator.RequestExplicitAsync(WellKnownAppPlatforms.Mobile);

        Assert.False(switched);
        Assert.Single(fixture.WindowRuntime.Windows);
        Assert.True(fixture.Manager.TryGetActive(launch.Process!.Pid, out _));
        Assert.Equal(WellKnownAppPlatforms.Desktop, fixture.PlatformPreference.Current.ActivePlatform);
    }

    [Fact]
    public async Task RequestExplicitAsync_does_not_disturb_windows_when_already_the_active_platform()
    {
        Fixture fixture = new();
        AppLaunchResult launch = await fixture.LaunchWindowAppAsync();
        WindowId windowId = fixture.CreateWindowFor(launch);
        RejectingGuard guard = new();
        using IDisposable registration = fixture.CloseGuards.Register(windowId, guard);
        await fixture.PlatformPreference.InitializeAsync();
        PlatformShellSwitchCoordinator coordinator = fixture.CreateCoordinator();

        // Desktop is already active (default), so re-selecting it explicitly must not consult the
        // (rejecting) guard or touch the window at all.
        bool switched = await coordinator.RequestExplicitAsync(WellKnownAppPlatforms.Desktop);

        Assert.True(switched);
        Assert.Single(fixture.WindowRuntime.Windows);
        Assert.True(fixture.Manager.TryGetActive(launch.Process!.Pid, out _));
        Assert.Equal(UiPlatformSelectionSource.Explicit, fixture.PlatformPreference.Current.SelectionSource);
    }

    [Fact]
    public async Task RequestAutoAsync_confirms_and_stops_windows_before_reverting()
    {
        Fixture fixture = new();
        AppLaunchResult launch = await fixture.LaunchWindowAppAsync();
        WindowId windowId = fixture.CreateWindowFor(launch);
        AlwaysConfirmGuard guard = new();
        using IDisposable registration = fixture.CloseGuards.Register(windowId, guard);
        await fixture.PlatformPreference.InitializeAsync();
        await fixture.PlatformPreference.SetExplicitAsync(WellKnownAppPlatforms.Mobile);
        PlatformShellSwitchCoordinator coordinator = fixture.CreateCoordinator();

        bool switched = await coordinator.RequestAutoAsync();

        Assert.True(switched);
        Assert.Equal(UiPlatformSelectionSource.Auto, fixture.PlatformPreference.Current.SelectionSource);
        Assert.Empty(fixture.WindowRuntime.Windows);
    }

    private sealed class AlwaysConfirmGuard : IWindowCloseGuard
    {
        public ValueTask<bool> ConfirmCloseAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);
    }

    private sealed class RejectingGuard : IWindowCloseGuard
    {
        public ValueTask<bool> ConfirmCloseAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class Fixture
    {
        private readonly DateTimeOffset _now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
        private readonly LocalLoginName _aliceLoginName;
        private int _entryId = 1;
        private int _transactionId = 100;

        internal Fixture()
        {
            FixedTimeProvider timeProvider = new(_now);
            InMemoryFileSystemRepository repository = new(
                () => FileSystemEntryId.FromGuid(new Guid(_entryId++, 0, 0, new byte[8])),
                () => new Guid(_transactionId++, 0, 0, new byte[8]),
                timeProvider);
            FileSystemMountRouter router = new(repository);
            Grants = new CapabilityGrantRepository(() => _now);
            InMemoryTopicMessageBus topicBus = new(Grants);
            FileSystemService fileSystem = new(
                router, new FileSystemPathResolver(router), new FileSystemAuthorizer(), topicBus,
                () => new Guid(_transactionId++, 0, 0, new byte[8]));
            FileSystemSeeder seeder = new(fileSystem, timeProvider);

            InMemoryLocalUserRepository users = new(() => _now);
            InMemoryLocalGroupRepository groups = new();
            EventBus = new InMemoryEventBus();
            BoundedAuditLog auditLog = new(maxEntries: 100);

            LocalGroup group = groups.CreateGroup(LocalLoginName.Parse("users"));
            _aliceLoginName = LocalLoginName.Parse("alice");
            users.CreateUser(
                _aliceLoginName, "Alice", AppAuthority.User, group.Id,
                credential: LocalPasswordHasher.Create("hunter2", iterations: 100));

            Session = new LocalSessionService(
                users, seeder, EventBus, auditLog,
                InstallationId.FromGuid(Guid.NewGuid()), DeviceId.FromGuid(Guid.NewGuid()), () => _now);

            ManualSimulationClock clock = new(_now, TimeSpan.FromSeconds(1));
            Manager = new InMemoryProcessManager(clock, Session, EventBus);
            InMemoryNotificationQueue notifications = new(maxEntriesPerUser: 20);
            BoundedDiagnosticSink diagnostics = new(maxEntries: 100);
            Settings = new InMemorySettingsDocumentService([UiPlatformPreferenceSettingsDocuments.CreateDefinition()]);
            AppExecutionContextFactory contextFactory = new(
                Grants, fileSystem, Settings, EventBus, topicBus, notifications, diagnostics, clock, Manager);

            AppManifest manifest = CreateWindowManifest();
            AppCatalogBuildResult catalogResult = AppCatalog.Build([manifest]);
            AppCatalog catalog = catalogResult.Catalog!;
            Dictionary<string, AppDescriptor> descriptors = new(StringComparer.Ordinal)
            {
                // Placeholder entry point type, never instantiated by the orchestrator for a window app.
                [manifest.Id] = new AppDescriptor(manifest, typeof(object), typeof(object).Assembly)
            };
            AppEnablementRegistry enablement = new(catalog);
            Orchestrator = new AppLifecycleOrchestrator(
                catalog, descriptors, enablement, Manager, Grants, contextFactory, Settings, EventBus);

            WindowRuntime = new WindowRuntime(new WindowBounds(0, 0, 1280, 720));
            CloseGuards = new WindowCloseGuardRegistry();
            PlatformPreference = new UiPlatformPreferenceService(Settings, new FixedProbe());
        }

        internal LocalSessionService Session { get; }
        internal InMemoryEventBus EventBus { get; }
        internal InMemoryProcessManager Manager { get; }
        internal CapabilityGrantRepository Grants { get; }
        internal InMemorySettingsDocumentService Settings { get; }
        internal AppLifecycleOrchestrator Orchestrator { get; }
        internal WindowRuntime WindowRuntime { get; }
        internal WindowCloseGuardRegistry CloseGuards { get; }
        internal UiPlatformPreferenceService PlatformPreference { get; }

        internal PlatformShellSwitchCoordinator CreateCoordinator() =>
            new(WindowRuntime, Orchestrator, CloseGuards, PlatformPreference);

        internal async Task<AppLaunchResult> LaunchWindowAppAsync()
        {
            AuthenticatedPrincipal principal = await Session.LoginAsync(_aliceLoginName, "hunter2");
            return await Orchestrator.LaunchAsync(new AppLaunchRequest("org.hackeros.test-window", principal, []));
        }

        internal WindowId CreateWindowFor(AppLaunchResult launch)
        {
            WindowId windowId = WindowId.FromGuid(Guid.NewGuid());
            WindowRuntimeState state = new(
                windowId,
                "org.hackeros.test-window",
                WindowOwnerId.FromGuid(launch.Process!.AppInstanceId.Value),
                "Test Window",
                null,
                new WindowBounds(20, 20, 640, 480),
                null,
                0,
                WindowVisualState.Normal,
                new WindowConstraints(true, 320, 240));
            WindowRuntime.Apply(new CreateWindowCommand(state));
            return windowId;
        }

        private static AppManifest CreateWindowManifest() => new()
        {
            Id = "org.hackeros.test-window",
            Name = "Test Window",
            Version = "1.0.0",
            PublisherId = "pub.hackeros",
            Description = "Platform switch coordinator test app.",
            Kind = AppKind.Window,
            EntryPoint = new AppEntryPointManifest("Test.dll", "Test.Window"),
            SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
            Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
            Resources = AppResourceProfileManifest.None
        };

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class FixedProbe : IPlatformEnvironmentProbe
        {
            public PlatformEnvironmentSnapshot Current { get; } = new(
                new PlatformEnvironmentSignals(1280, 800, false, true, 0, false),
                WellKnownAppPlatforms.Desktop,
                "test");

            public event Action? Changed { add { } remove { } }

            public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
