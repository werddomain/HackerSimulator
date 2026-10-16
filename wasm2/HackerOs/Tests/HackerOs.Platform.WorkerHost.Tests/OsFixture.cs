using System.Reflection;
using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.AppSdk.IO;
using HackerOs.Platform.Core.Diagnostics;
using HackerOs.Platform.Core.Discovery;
using HackerOs.Platform.Core.Elevation;
using HackerOs.Platform.Core.Events;
using HackerOs.Platform.Core.Execution;
using HackerOs.Platform.Core.FileSystem;
using HackerOs.Platform.Core.FileSystem.Virtual;
using HackerOs.Platform.Core.Intents;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Platform.Core.Metrics;
using HackerOs.Platform.Core.Network;
using HackerOs.Platform.Core.Notifications;
using HackerOs.Platform.Core.Policy;
using HackerOs.Platform.Core.Processes;
using HackerOs.Platform.Core.Sessions;
using HackerOs.Platform.Core.Settings;
using HackerOs.Platform.Core.Shell;
using HackerOs.Platform.Core.Time;
using HackerOs.Platform.WorkerHost.Host;
using HackerOs.Platform.WorkerHost.Loopback;
using HackerOs.Platform.Core;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Network;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.WorkerHost.Tests;

/// <summary>
/// A whole headless OS for the worker tests: the real filesystem with <c>/proc</c> and <c>/dev</c>,
/// sessions, process manager, simulated network, catalog, shell interpreter, every built-in command
/// of <c>HackerOs.Commands</c> and a few test commands. With <c>workers</c>, commands and services run
/// in loopback Web Workers through the real protocol (ADR 0070); without, in-process as before.
/// </summary>
internal sealed class OsFixture : IAsyncDisposable
{
    public const string HostAppId = "org.hackeros.terminal";

    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] HostCapabilities =
    [
        AppCapabilities.FileSystemUserHomeRead, AppCapabilities.FileSystemUserHomeWrite,
        AppCapabilities.FileSystemSystemRead, AppCapabilities.FileSystemSystemWrite,
        AppCapabilities.AppsLaunch, AppCapabilities.ShellHost, AppCapabilities.ProcessList
    ];

    private readonly LocalLoginName _alice = LocalLoginName.Parse("alice");
    private readonly CapabilityGrantRepository _grants;
    private readonly LocalSessionService _sessions;
    private int _entryId = 1;
    private int _transactionId = 1000;

    private OsFixture(bool workers, WorkerPoolOptions? poolOptions, int hardwareConcurrency, TimeSpan? workerStartupDelay)
    {
        FixedTimeProvider timeProvider = new(Now);
        InMemoryFileSystemRepository repository = new(
            () => FileSystemEntryId.FromGuid(new Guid(_entryId++, 0, 0, new byte[8])),
            () => new Guid(_transactionId++, 0, 0, new byte[8]),
            timeProvider);
        Clock = new ManualSimulationClock(Now, TimeSpan.FromSeconds(1));
        DeterministicResourceSimulator resources = new(Clock, new SeededSimulationRandom(42), VirtualHardwareProfile.Default);
        ProcFileSystemProvider proc = new(
            () => Manager!, resources, VirtualHardwareProfile.Default, Clock,
            environments: () => Manager?.Environments,
            transactionIdFactory: () => new Guid(_transactionId++, 0, 0, new byte[8]),
            metrics: new ProcMetricsSources(Workers: () => Pool));
        FileSystemMountRouter router = new(repository,
        [
            new FileSystemMount(ProcFileSystemProvider.DefaultMountPath, proc),
            new FileSystemMount(DevFileSystemProvider.DefaultMountPath, new DevFileSystemProvider(Clock))
        ]);
        _grants = new CapabilityGrantRepository(() => Now);
        InMemoryTopicMessageBus topicBus = new(_grants);
        FileSystemService fileSystem = new(
            router, new FileSystemPathResolver(router), new FileSystemAuthorizer(), topicBus,
            () => new Guid(_transactionId++, 0, 0, new byte[8]));
        FileSystem = fileSystem;
        FileSystemSeeder seeder = new(fileSystem, timeProvider);

        InMemoryLocalUserRepository users = new(() => Now);
        InMemoryLocalGroupRepository groups = new();
        EventBus = new InMemoryEventBus();
        LocalGroup group = groups.CreateGroup(LocalLoginName.Parse("users"));
        users.CreateUser(_alice, "Alice", AppAuthority.User, group.Id, credential: LocalPasswordHasher.Create("hunter2", iterations: 100));
        _sessions = new LocalSessionService(
            users, seeder, EventBus, new BoundedAuditLog(maxEntries: 100),
            InstallationId.FromGuid(Guid.NewGuid()), DeviceId.FromGuid(Guid.NewGuid()), () => Now);

        Manager = new InMemoryProcessManager(Clock, _sessions, EventBus);
        Log = new LogFileWriter(Clock, new BoundedDiagnosticSink(maxEntries: 1000), EventBus);

        AppManifest[] commands = [.. BuiltInCommands.Select(AppDefinitionMapper.ToManifest), .. TestCommands.Manifests];
        AppManifest host = HostManifest();
        Catalog = AppCatalog.Build([host, .. commands]).Catalog!;
        AppDiscoveryResult discovery = AppEntryPointDiscovery.Discover(
            AppCatalog.Build(commands).Catalog!,
            new Dictionary<string, Assembly>(StringComparer.Ordinal)
            {
                ["HackerOs.Commands.dll"] = typeof(HackerOs.Commands.Cd.CdCommand).Assembly,
                [TestCommands.AssemblyFile] = typeof(OsFixture).Assembly
            });
        Assert.True(discovery.IsSuccess, string.Join("; ", discovery.Errors.Select(error => error.ToString())));
        Dictionary<string, AppDescriptor> descriptors = new(discovery.Descriptors!, StringComparer.Ordinal)
        {
            [HostAppId] = new AppDescriptor(host, typeof(object), typeof(object).Assembly)
        };

        AppEnablementRegistry enablement = new(Catalog);
        AppExecutionContextFactory contextFactory = new(
            _grants, fileSystem, EventBus, topicBus, new InMemoryNotificationQueue(maxEntriesPerUser: 20),
            new BoundedDiagnosticSink(maxEntries: 100), Clock, Manager,
            intentDispatcherProvider: () => Dispatcher!,
            logWriter: Log,
            network: new InMemorySimulatedNetworkService(SmokeTestNetworkSeed.Hosts, SmokeTestWebsites()),
            appCatalog: new AppCatalogGatewayServices(Catalog, enablement),
            shellHostProvider: () => ShellHost!,
            metrics: new SystemMetricsServices(Drives: new AppDriveGateway(() => router, VirtualHardwareProfile.Default)));

        if (workers)
        {
            Runtime = new LoopbackWebWorkerRuntime(hardwareConcurrency, workerStartupDelay);
            Pool = new WorkerPool(
                Runtime,
                poolOptions ?? FastOptions,
                Clock,
                () => Manager,
                (message, pid) => Log.Write(LogFileTarget.Syslog, HackerOs.Simulation.Abstractions.Diagnostics.DiagnosticSeverity.Warning, "kernel", message, pid),
                Startup);
        }

        Orchestrator = new AppLifecycleOrchestrator(
            Catalog, descriptors, enablement, Manager, _grants, contextFactory, EventBus,
            elevationTickets: new InMemoryElevationTicketStore(),
            remoteHost: Pool is null ? null : new WorkerRemoteProcessHost(Pool));
        Interpreter = new ShellInterpreter(Orchestrator, Catalog, enablement);
        ShellHost = new ShellHostService(Interpreter, Manager, new RefusingElevationService());
        Dispatcher = new AppIntentDispatcher(
            Orchestrator, Catalog, enablement,
            new FileAssociationResolver(Catalog, enablement, new OsSettingsService(fileSystem, _sessions, Clock)),
            _grants, fileSystem);
    }

    /// <summary>Timings shortened for tests; same rules as the ADR defaults.</summary>
    public static WorkerPoolOptions FastOptions { get; } = new()
    {
        HeartbeatPeriod = TimeSpan.FromMilliseconds(20),
        UnresponsiveThreshold = TimeSpan.FromMilliseconds(400),
        BusyThreshold = TimeSpan.FromMilliseconds(150),
        KillAcknowledgeTimeout = TimeSpan.FromMilliseconds(300),
        OverflowIdleTimeout = TimeSpan.FromMilliseconds(400),
        IdleWaitTimeout = TimeSpan.FromMilliseconds(10),
        VirtualCores = 1,
        MeterCpu = false
    };

    /// <summary>Every <c>[AppManifest]</c> class of <c>HackerOs.Commands</c>: all the built-in commands.</summary>
    public static IReadOnlyList<AppDefinition> BuiltInCommands { get; } =
    [
        .. typeof(HackerOs.Commands.Cd.CdApp).Assembly.GetTypes()
            .Where(static type => type.GetCustomAttribute<AppManifestAttribute>() is not null)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .Select(static type => (AppDefinition)Activator.CreateInstance(type)!)
    ];

    public ManualSimulationClock Clock { get; }

    public InMemoryProcessManager Manager { get; }

    public InMemoryEventBus EventBus { get; }

    public IFileSystemService FileSystem { get; }

    public LogFileWriter Log { get; }

    public AppCatalog Catalog { get; }

    public AppLifecycleOrchestrator Orchestrator { get; }

    public ShellInterpreter Interpreter { get; }

    public ShellHostService ShellHost { get; }

    public AppIntentDispatcher Dispatcher { get; }

    public LoopbackWebWorkerRuntime? Runtime { get; }

    public WorkerPool? Pool { get; }

    public SystemStartupSignal Startup { get; } = new();

    public AuthenticatedPrincipal Principal { get; private set; } = default!;

    public IAppExecutionContext Host { get; private set; } = default!;

    public ShellSession Session { get; private set; } = default!;

    /// <summary>Builds the OS, logs Alice in and opens a terminal with its shell.</summary>
    public static async Task<OsFixture> CreateAsync(
        bool workers,
        WorkerPoolOptions? poolOptions = null,
        int hardwareConcurrency = 4,
        TimeSpan? workerStartupDelay = null)
    {
        OsFixture fixture = new(workers, poolOptions, hardwareConcurrency, workerStartupDelay);
        fixture.Principal = await fixture._sessions.LoginAsync(fixture._alice, "hunter2");
        foreach (string capability in HostCapabilities)
        {
            fixture.Grant(HostAppId, capability);
        }

        // Every command gets what it declares, as the system-app seeder grants it (ADR 0048).
        foreach (AppManifest command in fixture.Catalog.Manifests.Values.Where(static manifest => manifest.Kind != AppKind.Window))
        {
            foreach (string capability in command.Capabilities)
            {
                fixture.Grant(command.Id, capability);
            }
        }

        AppLaunchResult launched = await fixture.Orchestrator.LaunchAsync(new AppLaunchRequest(HostAppId, fixture.Principal, []));
        Assert.True(launched.IsSuccess, launched.ErrorMessage);
        fixture.Host = launched.Context!;
        fixture.Session = new ShellSession(fixture.Host, fixture.Principal);
        return fixture;
    }

    public void Grant(string appId, string capability) =>
        _grants.Grant(appId, Principal.UserId.ToString(), capability, CapabilityGrantSource.SystemPolicy, AppAuthority.Administrator);

    /// <summary>Runs one command line in the terminal's shell, capturing its output.</summary>
    public async Task<ShellRun> RunAsync(string line, string? input = null, CancellationToken cancellationToken = default)
    {
        StringWriter stdout = new() { NewLine = "\n" };
        StringWriter stderr = new() { NewLine = "\n" };
        ShellExecutionResult result = await Interpreter.ExecuteAsync(
            Session,
            line,
            new ShellExecutionOptions
            {
                StandardInput = input is null ? null : new StringReader(input),
                StandardOutput = stdout,
                StandardError = stderr
            },
            cancellationToken);
        return new ShellRun(result.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <summary>Runs a line while advancing simulated time, for <c>sleep</c> and other clock waits.</summary>
    public async Task<ShellRun> RunWithClockAsync(string line, int maxTicks = 50)
    {
        Task<ShellRun> run = RunAsync(line);
        for (int tick = 0; tick < maxTicks && !run.IsCompleted; tick++)
        {
            await Task.WhenAny(run, Task.Delay(30));
            if (!run.IsCompleted)
            {
                Clock.Advance();
            }
        }

        return await run.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public async Task<string> ReadFileAsync(string path)
    {
        using IDisposable scope = HackExecutionContext.Enter(Host);
        return await HackFile.ReadAllTextAsync(path);
    }

    public async Task WriteFileAsync(string path, string content)
    {
        using IDisposable scope = HackExecutionContext.Enter(Host);
        string? directory = HackPath.GetDirectoryName(path);
        if (directory is not null)
        {
            await HackDirectory.CreateDirectoryAsync(directory);
        }

        await HackFile.WriteAllTextAsync(path, content);
    }

    /// <summary>Lists every file and directory under <paramref name="root"/> with its content, for comparing two runs.</summary>
    public async Task<string> SnapshotTreeAsync(string root)
    {
        using IDisposable scope = HackExecutionContext.Enter(Host);
        List<string> lines = [];
        await WalkAsync(root);
        return string.Join('\n', lines);

        async Task WalkAsync(string directory)
        {
            foreach (string entry in (await HackDirectory.GetFileSystemEntriesAsync(directory)).Order(StringComparer.Ordinal))
            {
                if (await HackDirectory.ExistsAsync(entry))
                {
                    lines.Add($"{entry}/");
                    await WalkAsync(entry);
                }
                else
                {
                    UnixFileMode mode = await HackFile.GetUnixFileModeAsync(entry);
                    byte[] content = await HackFile.ReadAllBytesAsync(entry);
                    lines.Add($"{entry} {Convert.ToString((int)mode, 8)} {Convert.ToBase64String(content)}");
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        TestCommands.ReleaseSpinners();
        if (Pool is not null)
        {
            await Pool.DisposeAsync();
        }

        await Log.DisposeAsync();
    }

    private static ISimulatedWebsiteRegistry SmokeTestWebsites()
    {
        InMemorySimulatedWebsiteRegistry registry = new();
        foreach (ISimulatedWebsiteController controller in SmokeTestNetworkSeed.Websites)
        {
            registry.Register(controller);
        }

        return registry;
    }

    private static AppManifest HostManifest() => new()
    {
        Id = HostAppId,
        Name = "Terminal",
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "Hosts the shell.",
        Kind = AppKind.Window,
        EntryPoint = new AppEntryPointManifest("HackerOs.Platform.WorkerHost.Tests", "org.hackeros.terminal.EntryPoint"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        Capabilities = HostCapabilities
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>No administrator here: sudo always fails, identically in both modes.</summary>
    private sealed class RefusingElevationService : IElevationService
    {
        public Task<ElevationRequestResult> RequestSudoAsync(string adminLoginName, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ElevationRequestResult(false, null, null, "elevation.denied"));

        public Task<ElevationRequestResult> RequestCapabilityApprovalAsync(
            string adminLoginName, string password, string capability, VirtualPath? path, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ElevationRequestResult(false, null, null, "elevation.denied"));
    }
}

/// <summary>The outcome of one command line.</summary>
internal sealed record ShellRun(int ExitCode, string Stdout, string Stderr);
