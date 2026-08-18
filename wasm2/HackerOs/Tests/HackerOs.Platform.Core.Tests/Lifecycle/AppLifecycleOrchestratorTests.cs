using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.Diagnostics;
using HackerOs.Platform.Core.Discovery;
using HackerOs.Platform.Core.Events;
using HackerOs.Platform.Core.Execution;
using HackerOs.Platform.Core.FileSystem;
using HackerOs.Platform.Core.Intents;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Platform.Core.Notifications;
using HackerOs.Platform.Core.Policy;
using HackerOs.Platform.Core.Processes;
using HackerOs.Platform.Core.Sessions;
using HackerOs.Platform.Core.Time;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Tests.Lifecycle;

/// <summary>
/// Tests for `P1-APP-004` through `P1-APP-006` and `P1-APP-010`/`P1-APP-011`: launching Terminal
/// and Service apps, singleton Window focusing, ordered service start/stop, and runtime
/// enable/disable with dependency cascades.
/// </summary>
public sealed class AppLifecycleOrchestratorTests
{
    [Fact]
    public async Task Launching_a_terminal_app_executes_it_synchronously_and_returns_its_exit_code_and_output()
    {
        Fixture fixture = new(EchoManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();

        AppLaunchResult result = await fixture.Orchestrator.LaunchAsync(
            new AppLaunchRequest("org.hackeros.echo", principal, ["hello", "world"]));

        Assert.Equal(AppLaunchStatus.Launched, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"hello world{Environment.NewLine}", result.StandardOutput);
        Assert.Equal(ProcessState.Stopped, result.Process!.State);
    }

    [Fact]
    public async Task Launching_a_service_app_runs_it_in_the_background_until_it_is_stopped()
    {
        Fixture fixture = new(WaiterManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();

        AppLaunchResult launch = await fixture.Orchestrator.LaunchAsync(new AppLaunchRequest("org.hackeros.waiter", principal, []));

        Assert.Equal(AppLaunchStatus.Launched, launch.Status);
        Assert.True(fixture.Manager.TryGetActive(launch.Process!.Pid, out ProcessRecord active));
        Assert.Equal(ProcessState.Running, active.State);

        await fixture.Orchestrator.StopAllAsync(ProcessExitReason.Shutdown);

        Assert.False(fixture.Manager.TryGetActive(launch.Process!.Pid, out _));
        Assert.Equal(ServiceStopReason.Shutdown, WaitingServiceApp.LastStopReasonObserved);
    }

    [Fact]
    public async Task A_singleton_window_apps_second_launch_focuses_the_existing_instance()
    {
        Fixture fixture = new(SingletonWindowManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();

        AppLaunchResult first = await fixture.Orchestrator.LaunchAsync(new AppLaunchRequest("org.hackeros.notes", principal, []));
        AppLaunchResult second = await fixture.Orchestrator.LaunchAsync(new AppLaunchRequest("org.hackeros.notes", principal, []));

        Assert.Equal(AppLaunchStatus.Launched, first.Status);
        Assert.Equal(AppLaunchStatus.FocusedExisting, second.Status);
        Assert.Equal(first.Process!.Pid, second.Process!.Pid);
    }

    [Fact]
    public async Task Stopping_one_window_instance_cancels_its_context_and_records_close_reason()
    {
        Fixture fixture = new(SingletonWindowManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();
        AppLaunchResult launch = await fixture.Orchestrator.LaunchAsync(
            new AppLaunchRequest("org.hackeros.notes", principal, []));

        bool stopped = await fixture.Orchestrator.StopAsync(launch.Process!.Pid);

        Assert.True(stopped);
        Assert.True(launch.Context!.CancellationToken.IsCancellationRequested);
        Assert.False(fixture.Manager.TryGetActive(launch.Process.Pid, out _));
        ProcessRecord history = Assert.Single(fixture.Manager.GetHistory(), item => item.Pid == launch.Process.Pid);
        Assert.Equal(ProcessExitReason.CloseRequested, history.ExitReason);
        Assert.False(await fixture.Orchestrator.StopAsync(launch.Process.Pid));
    }

    [Fact]
    public async Task Launching_an_unknown_app_id_returns_not_found()
    {
        Fixture fixture = new(EchoManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();

        AppLaunchResult result = await fixture.Orchestrator.LaunchAsync(new AppLaunchRequest("org.hackeros.missing", principal, []));

        Assert.Equal(AppLaunchStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Launching_a_disabled_app_returns_disabled()
    {
        Fixture fixture = new(EchoManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();
        await fixture.Orchestrator.DisableAsync("org.hackeros.echo");

        AppLaunchResult result = await fixture.Orchestrator.LaunchAsync(new AppLaunchRequest("org.hackeros.echo", principal, []));

        Assert.Equal(AppLaunchStatus.Disabled, result.Status);
    }

    [Fact]
    public async Task An_entry_point_that_throws_faults_the_process_and_returns_entry_point_fault()
    {
        Fixture fixture = new(FaultingManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();

        AppLaunchResult result = await fixture.Orchestrator.LaunchAsync(new AppLaunchRequest("org.hackeros.faulty", principal, []));

        Assert.Equal(AppLaunchStatus.EntryPointFault, result.Status);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(ProcessState.Faulted, result.Process!.State);
    }

    [Fact]
    public async Task Disabling_an_app_cascades_to_apps_that_depend_on_it_and_stops_their_processes()
    {
        AppManifest dependency = WaiterManifest();
        AppManifest dependent = WaiterManifest() with
        {
            Id = "org.hackeros.dependent-waiter",
            EntryPoint = new AppEntryPointManifest("HackerOs.Platform.Core.Tests", typeof(WaitingServiceApp).FullName!),
            Dependencies = [new AppDependencyManifest(dependency.Id, "1.0.0", null, Optional: false)]
        };
        Fixture fixture = new(dependency, dependent);
        List<string> disabledEvents = [];
        using IDisposable subscription = fixture.EventBus.Subscribe<AppDisabledEvent>(
            disabled => disabledEvents.Add(disabled.AppId));
        AuthenticatedPrincipal principal = await fixture.LoginAsync();
        await fixture.Orchestrator.LaunchAsync(new AppLaunchRequest(dependency.Id, principal, []));
        await fixture.Orchestrator.LaunchAsync(new AppLaunchRequest(dependent.Id, principal, []));

        AppDisableResult disableResult = await fixture.Orchestrator.DisableAsync(dependency.Id);

        Assert.True(disableResult.Success);
        Assert.Contains(dependency.Id, disableResult.DisabledAppIds);
        Assert.Contains(dependent.Id, disableResult.DisabledAppIds);
        Assert.False(fixture.Orchestrator.Enablement.IsEnabled(dependency.Id));
        Assert.False(fixture.Orchestrator.Enablement.IsEnabled(dependent.Id));
        Assert.Equal(disableResult.DisabledAppIds, disabledEvents);
    }

    [Fact]
    public async Task DisableAsync_persists_through_the_catalog_repository_when_supplied()
    {
        FakeAppCatalogRepository catalogRepository = new();
        Fixture fixture = new(catalogRepository, EchoManifest());
        await fixture.LoginAsync();

        await fixture.Orchestrator.DisableAsync("org.hackeros.echo");

        Assert.Equal(("org.hackeros.echo", false), Assert.Single(catalogRepository.Calls));
    }

    [Fact]
    public async Task EnableAsync_persists_through_the_catalog_repository_when_supplied()
    {
        FakeAppCatalogRepository catalogRepository = new();
        Fixture fixture = new(catalogRepository, EchoManifest());
        await fixture.LoginAsync();
        await fixture.Orchestrator.DisableAsync("org.hackeros.echo");
        catalogRepository.Calls.Clear();

        await fixture.Orchestrator.EnableAsync("org.hackeros.echo");

        Assert.Equal(("org.hackeros.echo", true), Assert.Single(catalogRepository.Calls));
    }

    [Fact]
    public async Task DisableAsync_and_EnableAsync_work_unchanged_when_no_catalog_repository_is_supplied()
    {
        Fixture fixture = new(EchoManifest());
        await fixture.LoginAsync();

        AppDisableResult disableResult = await fixture.Orchestrator.DisableAsync("org.hackeros.echo");
        AppEnableResult enableResult = await fixture.Orchestrator.EnableAsync("org.hackeros.echo");

        Assert.True(disableResult.Success);
        Assert.True(enableResult.Success);
    }

    [Fact]
    public async Task Launching_a_command_whose_constructor_needs_an_injected_service_resolves_it_from_di()
    {
        FakeServiceProvider services = new(new FakeInjectedService("injected-value"));

        AppManifest manifest = InjectingManifest();
        Fixture fixture = new(services, manifest);
        AuthenticatedPrincipal principal = await fixture.LoginAsync();

        AppLaunchResult result = await fixture.Orchestrator.LaunchAsync(
            new AppLaunchRequest(manifest.Id, principal, []));

        Assert.Equal(AppLaunchStatus.Launched, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"injected-value{Environment.NewLine}", result.StandardOutput);
    }

    [Fact]
    public async Task Launching_a_command_with_extra_constructor_dependencies_without_a_service_provider_fails_as_entry_point_fault()
    {
        // Characterizes the bug this pass fixes: without a service provider, construction throws
        // (MissingMethodException) inside LaunchAsync's existing catch-all, which reports it as
        // EntryPointFault rather than crashing — this is the "curl doesn't crash the app, it just
        // silently produces a confusing reflection error" shape the bug actually had in production.
        AppManifest manifest = InjectingManifest();
        Fixture fixture = new(manifest);
        AuthenticatedPrincipal principal = await fixture.LoginAsync();

        AppLaunchResult result = await fixture.Orchestrator.LaunchAsync(
            new AppLaunchRequest(manifest.Id, principal, []));

        Assert.Equal(AppLaunchStatus.EntryPointFault, result.Status);
        Assert.Contains("constructor", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private interface IFakeInjectedService
    {
        string Value { get; }
    }

    private sealed class FakeInjectedService(string value) : IFakeInjectedService
    {
        public string Value { get; } = value;
    }

    private sealed class FakeServiceProvider(IFakeInjectedService injected) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IFakeInjectedService) ? injected : null;
    }

    private sealed class InjectingTerminalApp(AppManifest manifest, IFakeInjectedService injected) : TerminalAppBase(manifest)
    {
        public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
        {
            await context.StandardOutput.WriteLineAsync(injected.Value.AsMemory(), cancellationToken);
            return 0;
        }
    }

    private static AppManifest InjectingManifest() => new()
    {
        Id = "org.hackeros.injecting",
        Name = "Injecting",
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "Requires an injected service beyond the manifest.",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Platform.Core.Tests", typeof(InjectingTerminalApp).FullName!),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("injecting", [], "injecting")
    };

    [Fact]
    public async Task Enabling_an_app_is_blocked_with_an_explanatory_error_when_a_dependency_is_disabled()
    {
        AppManifest dependency = EchoManifest();
        AppManifest dependent = EchoManifest() with
        {
            Id = "org.hackeros.dependent-echo",
            Dependencies = [new AppDependencyManifest(dependency.Id, "1.0.0", null, Optional: false)]
        };
        Fixture fixture = new(dependency, dependent);
        await fixture.Orchestrator.DisableAsync(dependency.Id);
        await fixture.Orchestrator.DisableAsync(dependent.Id);

        AppEnableResult result = await fixture.Orchestrator.EnableAsync(dependent.Id);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains(dependency.Id, StringComparison.Ordinal));
    }

    private static AppManifest EchoManifest() => new()
    {
        Id = "org.hackeros.echo",
        Name = "Echo",
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "Echoes its arguments.",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Platform.Core.Tests", typeof(EchoTerminalApp).FullName!),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("echo", [], "echo [text]")
    };

    private static AppManifest FaultingManifest() => new()
    {
        Id = "org.hackeros.faulty",
        Name = "Faulty",
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "Always throws.",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Platform.Core.Tests", typeof(FaultingTerminalApp).FullName!),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("faulty", [], "faulty")
    };

    private static AppManifest WaiterManifest() => new()
    {
        Id = "org.hackeros.waiter",
        Name = "Waiter",
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "Waits for cancellation.",
        Kind = AppKind.Service,
        EntryPoint = new AppEntryPointManifest("HackerOs.Platform.Core.Tests", typeof(WaitingServiceApp).FullName!),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None
    };

    private static AppManifest SingletonWindowManifest() => new()
    {
        Id = "org.hackeros.notes",
        Name = "Notes",
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "A singleton notes window.",
        Kind = AppKind.Window,
        EntryPoint = new AppEntryPointManifest("HackerOs.Platform.Core.Tests", "org.hackeros.notes.EntryPoint"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        SingleInstancePerUser = true
    };

    private sealed class EchoTerminalApp(AppManifest manifest) : TerminalAppBase(manifest)
    {
        public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
        {
            await context.StandardOutput.WriteLineAsync(string.Join(' ', context.Arguments).AsMemory(), cancellationToken);
            return 0;
        }
    }

    private sealed class FaultingTerminalApp(AppManifest manifest) : TerminalAppBase(manifest)
    {
        public override ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated entry-point fault.");
    }

    private sealed class WaitingServiceApp(AppManifest manifest) : ServiceAppBase(manifest)
    {
        internal static ServiceStopReason? LastStopReasonObserved { get; private set; }

        protected override Task RunCoreAsync(IAppExecutionContext context, CancellationToken sessionCancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, sessionCancellationToken);

        protected override ValueTask OnStoppingAsync(ServiceStopReason reason, CancellationToken cancellationToken)
        {
            LastStopReasonObserved = reason;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Fixture
    {
        private int _entryId = 1;
        private int _transactionId = 100;
        private readonly DateTimeOffset _now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        private readonly LocalLoginName _aliceLoginName;

        internal Fixture(params AppManifest[] manifests)
            : this(catalogRepository: null, services: null, manifests)
        {
        }

        internal Fixture(IPersistentAppCatalogRepository? catalogRepository, params AppManifest[] manifests)
            : this(catalogRepository, services: null, manifests)
        {
        }

        internal Fixture(IServiceProvider services, params AppManifest[] manifests)
            : this(catalogRepository: null, services, manifests)
        {
        }

        internal Fixture(
            IPersistentAppCatalogRepository? catalogRepository, IServiceProvider? services, params AppManifest[] manifests)
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
            Settings = new InMemorySettingsDocumentService([FileAssociationSettingsDocuments.CreateDefinition()]);
            AppExecutionContextFactory contextFactory = new(
                Grants, fileSystem, Settings, EventBus, topicBus, notifications, diagnostics, clock, Manager);

            AppCatalogBuildResult catalogResult = AppCatalog.Build(manifests);
            Assert.True(catalogResult.IsSuccess, string.Join(", ", catalogResult.Errors.Select(e => e.Message)));
            AppCatalog catalog = catalogResult.Catalog!;

            Dictionary<string, System.Reflection.Assembly> hostAssemblies = new(StringComparer.Ordinal)
            {
                ["HackerOs.Platform.Core.Tests"] = typeof(Fixture).Assembly
            };
            AppDiscoveryResult discovery = AppEntryPointDiscovery.Discover(catalog, hostAssemblies);
            Assert.True(
                discovery.IsSuccess || manifests.Any(m => m.Kind == AppKind.Window),
                string.Join(", ", discovery.Errors.Select(e => e.Message)));

            // Window apps in these tests use a placeholder entry point (never instantiated by the
            // orchestrator), so descriptors are supplied directly rather than through discovery.
            Dictionary<string, AppDescriptor> descriptors = new(StringComparer.Ordinal);
            foreach (AppManifest manifest in manifests)
            {
                descriptors[manifest.Id] = manifest.Kind == AppKind.Window
                    ? new AppDescriptor(manifest, typeof(object), typeof(object).Assembly)
                    : discovery.Descriptors![manifest.Id];
            }

            AppEnablementRegistry enablement = new(catalog);
            Orchestrator = new AppLifecycleOrchestrator(
                catalog, descriptors, enablement, Manager, Grants, contextFactory, Settings, EventBus,
                descriptorLoader: null, catalogRepository, services);
        }

        internal LocalSessionService Session { get; }
        internal InMemoryEventBus EventBus { get; }
        internal InMemoryProcessManager Manager { get; }
        internal CapabilityGrantRepository Grants { get; }
        internal InMemorySettingsDocumentService Settings { get; }
        internal AppLifecycleOrchestrator Orchestrator { get; }

        internal Task<AuthenticatedPrincipal> LoginAsync() => Session.LoginAsync(_aliceLoginName, "hunter2");

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }

    private sealed class FakeAppCatalogRepository : IPersistentAppCatalogRepository
    {
        public List<(string AppId, bool Enabled)> Calls { get; } = [];

        public ValueTask<IReadOnlyList<PersistedAppCatalogEntry>> ReconcileAsync(
            IEnumerable<AppManifest> selectedManifests, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by these tests.");

        public ValueTask<bool> SetEnabledAsync(string appId, bool enabled, CancellationToken cancellationToken = default)
        {
            Calls.Add((appId, enabled));
            return ValueTask.FromResult(true);
        }

        public ValueTask<IReadOnlyList<PersistedAppCatalogEntry>> ReadAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by these tests.");
    }
}
