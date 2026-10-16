using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Infrastructure.Browser.Catalog;
using HackerOs.Infrastructure.Browser.FileSystem;
using HackerOs.Infrastructure.Browser.Interop;
using HackerOs.Infrastructure.Browser.Policy;
using HackerOs.Infrastructure.Browser.ServerConnection;
using HackerOs.Infrastructure.Browser.Sessions;
using HackerOs.Infrastructure.Browser.Settings;
using HackerOs.Infrastructure.Browser.Sync;
using HackerOs.Platform.Core.ServerConnection;
using HackerOs.Ecosystem.Administration;
using HackerOs.Platform.Core.Administration;
using HackerOs.Platform.Core.Power;
using HackerOs.Platform.Core.Apps;
using HackerOs.Platform.Core;
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
using HackerOs.Platform.Core.Processes.Accounting;
using HackerOs.Platform.Core.Sessions;
using HackerOs.Platform.Core.Settings;
using HackerOs.Platform.Core.Shell;
using HackerOs.Platform.Core.Time;
using HackerOs.Platform.Blazor.Components;
using HackerOs.Platform.Blazor.Dialogs;
using HackerOs.Platform.Blazor.Security;
using HackerOs.Platform.Blazor.Shell;
using HackerOs.Platform.Blazor.Windows;
using HackerOs.Simulation.Abstractions;
using HackerOs.Windowing.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HackerOs.Windowing.Abstractions;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Metrics;
using HackerOs.Simulation.Abstractions.Network;
using HackerOs.Simulation.Abstractions.Settings;
using HackerOs.Simulation.Abstractions.Notifications;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.ServerConnection;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Simulation.Abstractions.Sync;
using HackerOs.Simulation.Abstractions.Time;
using HackerOs.AppSdk.Blazor;
using HackerOs.AppSdk.Blazor.Components;
using HackerOs.AppSdk.FileView.Icons;
using HackerOs.AppSdk.Icons;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace HackerOs.Ecosystem;

/// <summary>Registers the process-wide HackerOS ecosystem service graph.</summary>
public static class EcosystemServiceCollectionExtensions
{
    /// <summary>
    /// Adds persistent browser infrastructure and the process-wide simulation services.
    /// Asynchronous storage reconciliation and session startup remain boot responsibilities.
    /// </summary>
    public static IServiceCollection AddHackerOsEcosystem(
        this IServiceCollection services,
        AppCatalog? catalog = null,
        Func<IServiceProvider, IReadOnlyDictionary<string, AppDescriptor>>? descriptorProvider = null,
        Func<IServiceProvider, IAppDescriptorLoader?>? descriptorLoaderProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLogging();
        services.AddMudServices();

        services.AddSingleton(TimeProvider.System);
        // One ManualSimulationClock, driven in live hosts by SimulationClockDriver (ADR 0012);
        // the boot sequence starts the driver.
        services.AddSingleton(_ => new ManualSimulationClock(
            DateTimeOffset.UtcNow,
            TimeSpan.FromMilliseconds(100)));
        services.AddSingleton<ISimulationClock>(provider => provider.GetRequiredService<ManualSimulationClock>());
        services.AddSingleton(provider => new SimulationClockDriver(
            provider.GetRequiredService<ManualSimulationClock>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ISimulationRandom>(_ => new SeededSimulationRandom(1));

        services.AddSingleton<IDiagnosticRedactor>(_ => new SensitiveKeyDiagnosticRedactor());
        services.AddSingleton<IEventBus>(_ => new InMemoryEventBus());
        // In-memory buffer of recent entries for live viewers and the boot error screen. Logs
        // themselves are files under /var/log, written only by LogFileWriter (ADR 0045).
        services.AddSingleton<IDiagnosticSink>(provider => new EventPublishingDiagnosticSink(
            new BoundedDiagnosticSink(1024),
            provider.GetRequiredService<IEventBus>()));
        services.AddSingleton(provider => new LogFileWriter(
            provider.GetRequiredService<ISimulationClock>(),
            provider.GetRequiredService<IDiagnosticSink>(),
            provider.GetRequiredService<IEventBus>()));
        services.AddSingleton<IAuditLog>(provider => new FileAuditLog(
            provider.GetRequiredService<LogFileWriter>(),
            provider.GetRequiredService<IDiagnosticRedactor>()));
        // Every dependency is a singleton, so the host's root ILoggerFactory can build this provider
        // before any Blazor scope exists. ILogger<T> entries land in /var/log/syslog.
        services.AddSingleton<ILoggerProvider>(provider => new HackerOsDiagnosticLoggerProvider(
            provider.GetRequiredService<IDiagnosticSink>(),
            provider.GetRequiredService<LogFileWriter>(),
            provider.GetRequiredService<ISimulationClock>(),
            provider.GetRequiredService<IDiagnosticRedactor>()));
        // Keep this provider scoped to HackerOS's own log categories: the ASP.NET Core host
        // (Kestrel, hosting lifetime, HTTPS redirection, etc.) also resolves every registered
        // ILoggerProvider, and its framework-internal chatter isn't a HackerOS diagnostic event.
        services.AddLogging(builder =>
        {
            builder.AddFilter<HackerOsDiagnosticLoggerProvider>("Microsoft", LogLevel.None);
            builder.AddFilter<HackerOsDiagnosticLoggerProvider>("System", LogLevel.None);
        });

        services.AddScoped<HostExceptionReporter>();
        services.AddSingleton<INotificationQueue>(_ => new InMemoryNotificationQueue(100));

        // IJSRuntime is scoped per Blazor Server circuit (WASM hosts have exactly one implicit
        // scope for the app's lifetime, so this is a no-op behavior change there). Everything in
        // this file that construct-injects IJSRuntime — directly or transitively — must be Scoped,
        // not Singleton: a singleton is constructed once, on first resolution, and Blazor Web Apps
        // construct+inject root/child components (running their [Inject] properties) during the
        // static-SSR pass of the initial HTTP response even when that component's own @rendermode
        // has prerender:false — prerender:false only skips calling its lifecycle methods during
        // that pass, not construction. A singleton first resolved there permanently captures that
        // pass's unattached IJSRuntime (no live circuit yet), and every later real circuit reuses
        // that same dead instance, so its JS interop calls fail forever with "cannot be issued at
        // this time... statically rendered" — regardless of which lifecycle method later calls it.
        // Scoped services get a fresh instance per circuit's own DI scope, which is exactly what's
        // needed here. See docs/Global-FileView-And-MessagingSystem/integrationPlan.md Phase 4 live
        // browser check notes.
        services.AddScoped<IndexedDbLocalUserRepository>();
        services.AddScoped<ILocalUserRepository>(provider =>
            provider.GetRequiredService<IndexedDbLocalUserRepository>());
        services.AddScoped<IndexedDbLocalGroupRepository>();
        services.AddScoped<ILocalGroupRepository>(provider =>
            provider.GetRequiredService<IndexedDbLocalGroupRepository>());

        // Settings are files (ADR 0044). The retired IndexedDB settings store is only read once by
        // the migration below; it is never written again.
        services.AddScoped<ILegacySettingsStore>(provider =>
            new IndexedDbLegacySettingsStore(provider.GetRequiredService<Microsoft.JSInterop.IJSRuntime>()));
        services.AddScoped<IndexedDbCapabilityGrantRepository>();
        services.AddScoped<IPersistentCapabilityGrantRepository>(provider =>
            provider.GetRequiredService<IndexedDbCapabilityGrantRepository>());
        services.AddSingleton<ICapabilityGrantRepository>(_ => new CapabilityGrantRepository());
        services.AddSingleton<ITopicMessageBus>(provider =>
            new InMemoryTopicMessageBus(provider.GetRequiredService<ICapabilityGrantRepository>()));

        // Measurements of ADR 0057, in memory, from zero at each start, never persisted. CPU time is
        // measured when the ADR 0067 accountant is active, simulated otherwise (fed by
        // ResourceSamplingService); disk and network bytes are really counted by
        // the persistence, the filesystem service and each app's network gateway.
        // Optional measured CPU and memory (ADR 0066): AddHackerOsResourceAccounting, called by the
        // host before this method, may already have registered them. Otherwise both measurements are
        // off and every per-process figure stays simulated, exactly as before phase 5.
        services.TryAddSingleton(ResourceAccountingPlan.Disabled);
        services.TryAddSingleton<ICpuTimeAccountant>(NullCpuTimeAccountant.Instance);
        services.TryAddSingleton<IMemoryAccountant>(NullMemoryAccountant.Instance);
        services.AddScoped<IProcessIoAccounting, ProcessIoAccounting>();
        services.AddScoped<IBlockDeviceStatistics>(_ => new BlockDeviceStatistics());
        services.AddScoped<INetworkInterfaceStatistics, NetworkInterfaceStatistics>();
        services.AddScoped<ICpuTimeAccounting>(provider =>
        {
            AppCatalog catalog = provider.GetRequiredService<AppCatalog>();
            // System time: system services, and processes the catalog does not know (OS-owned).
            return new CpuTimeAccounting(
                VirtualHardwareProfile.Default,
                process => !catalog.Manifests.ContainsKey(process.AppId)
                    || (process.Kind == AppKind.Service && catalog.IsSystemApp(process.AppId)),
                provider.GetRequiredService<ICpuTimeAccountant>());
        });

        services.AddScoped<IndexedDbFileSystemProvider>(provider => new IndexedDbFileSystemProvider(
            provider.GetRequiredService<Microsoft.JSInterop.IJSRuntime>(),
            provider.GetRequiredService<IBlockDeviceStatistics>()));
        services.AddScoped<IStorageUsageProvider>(provider => new IndexedDbStorageUsageProvider(
            provider.GetRequiredService<IndexedDbFileSystemProvider>(),
            provider.GetRequiredService<Microsoft.JSInterop.IJSRuntime>()));
        services.AddScoped<IndexedDbFileSystemBootstrapper>();
        services.AddScoped<IFileSystemProvider>(provider =>
            provider.GetRequiredService<IndexedDbFileSystemProvider>());
        // /proc and /dev are virtual, read-only mounts computed on every read and never written to
        // IndexedDB (ADR 0046). The process manager is resolved lazily: it depends on the session
        // service, which depends on FileSystemSeeder, which reaches /proc through this router.
        services.AddScoped<IFileSystemMountRouter>(provider =>
            new FileSystemMountRouter(provider.GetRequiredService<IFileSystemProvider>(),
            [
                new FileSystemMount(ProcFileSystemProvider.DefaultMountPath, new ProcFileSystemProvider(
                    () => provider.GetRequiredService<IProcessManager>(),
                    provider.GetRequiredService<IResourceSimulator>(),
                    VirtualHardwareProfile.Default,
                    provider.GetRequiredService<ISimulationClock>(),
                    process => provider.GetRequiredService<ISessionService>().CurrentPrincipal is { } principal
                        && principal.UserId == process.UserId
                            ? principal.LoginName.Value
                            : null,
                    environments: () => provider.GetRequiredService<IProcessEnvironmentTable>(),
                    metrics: new ProcMetricsSources(
                        provider.GetRequiredService<ICpuTimeAccounting>(),
                        provider.GetRequiredService<IProcessIoAccounting>(),
                        provider.GetRequiredService<IBlockDeviceStatistics>(),
                        provider.GetRequiredService<INetworkInterfaceStatistics>(),
                        () => provider.GetRequiredService<IFileSystemMountRouter>(),
                        provider.GetRequiredService<ICpuTimeAccountant>(),
                        provider.GetRequiredService<IMemoryAccountant>()))),
                new FileSystemMount(DevFileSystemProvider.DefaultMountPath, new DevFileSystemProvider(
                    provider.GetRequiredService<ISimulationClock>()))
            ]));
        // Depends on IFileSystemMountRouter, which is Scoped (see above) — see this file's
        // IJSRuntime scoping note.
        services.AddScoped<IFileSystemPathResolver, FileSystemPathResolver>();
        services.AddSingleton<IFileSystemAuthorizer, FileSystemAuthorizer>();
        services.AddScoped<IFileSystemService, FileSystemService>();
        services.AddTransient<FileSystemSeeder>();
        // Depends on IFileSystemService, which is Scoped (see above) — see this file's IJSRuntime
        // scoping note.
        services.AddScoped<SettingsStoreToFilesMigration>(provider => new SettingsStoreToFilesMigration(
            provider.GetRequiredService<IFileSystemService>(),
            provider.GetRequiredService<ISimulationClock>(),
            provider.GetRequiredService<ILegacySettingsStore>(),
            provider.GetService<ILogger<SettingsStoreToFilesMigration>>()));
        services.AddScoped<IOsSettingsService>(provider => new OsSettingsService(
            provider.GetRequiredService<IFileSystemService>(),
            provider.GetRequiredService<ISessionService>(),
            provider.GetRequiredService<ISimulationClock>(),
            provider.GetService<ITopicMessageBus>(),
            provider.GetService<IEventBus>(),
            provider.GetService<ILogger<OsSettingsService>>()));

        services.AddScoped<WebCryptoPasswordHasher>(provider =>
            new WebCryptoPasswordHasher(provider.GetRequiredService<Microsoft.JSInterop.IJSRuntime>()));
        services.AddSingleton<ILoginProgressTracker, LoginProgressTracker>();

        // Optional server connection (ADR 0028) — opt-in, per-device; a device that never
        // connects sees no behavior change anywhere else in this composition.
        services.AddScoped<IServerConnectionRepository>(provider =>
            new IndexedDbServerConnectionRepository(provider.GetRequiredService<Microsoft.JSInterop.IJSRuntime>()));
        services.AddSingleton<HttpClient>();
        // TryAdd, not Add: the server-hosted host (ADR 0036) registers its own direct-injection
        // implementations before calling this method, so it never gets the HTTP-backed defaults
        // below. Both WASM hosts register nothing here first, so they get the same Http*Client
        // defaults as before this change — zero behavior change for them.
        services.TryAddSingleton<IAccountClient, HttpAccountClient>();
        services.TryAddSingleton<IProxyClient, HttpProxyClient>();
        services.AddScoped<IServerConnectionService>(provider =>
        {
            WebCryptoPasswordHasher hasher = provider.GetRequiredService<WebCryptoPasswordHasher>();
            return new ServerConnectionService(
                provider.GetRequiredService<IServerConnectionRepository>(),
                provider.GetRequiredService<IAccountClient>(),
                asyncPasswordHasher: (password, salt, iterations, length, ct) =>
                    hasher.DeriveKeyAsync(password, salt, iterations, length, ct));
        });

        // Sync plumbing shared by every domain below; each is a no-op when disconnected. Settings
        // have no domain of their own anymore: user settings are home files carried by FileSystem
        // sync (ADR 0044 section 10).
        services.AddScoped<ISyncCursorRepository>(provider =>
            new IndexedDbSyncCursorRepository(provider.GetRequiredService<Microsoft.JSInterop.IJSRuntime>()));
        services.AddScoped<ISyncRecordStateRepository>(provider =>
            new IndexedDbSyncRecordStateRepository(provider.GetRequiredService<Microsoft.JSInterop.IJSRuntime>()));
        services.TryAddSingleton<ISyncClient, HttpSyncClient>();

        // FileSystem domain sync (ADR 0030) — reuses the same connection and sync scaffolding as
        // above; scoped to the active session's own /home/{userId}, so a no-op when
        // disconnected or no session is active.
        services.AddSingleton<IContentTransferClient, HttpContentTransferClient>();
        services.AddScoped<IFileSystemSyncService>(provider => new FileSystemSyncService(
            provider.GetRequiredService<IFileSystemService>(),
            provider.GetRequiredService<ISessionService>(),
            provider.GetRequiredService<IServerConnectionService>(),
            provider.GetRequiredService<ISyncClient>(),
            provider.GetRequiredService<IContentTransferClient>(),
            provider.GetRequiredService<ISyncCursorRepository>(),
            provider.GetRequiredService<ISyncRecordStateRepository>()));

        // Grants domain sync (ADR 0031) — pull-only, applies server-issued grants into the durable
        // grant store. Not wired into live capability enforcement in this pass (see ADR 0031).
        services.AddScoped<IGrantsSyncService>(provider => new GrantsSyncService(
            provider.GetRequiredService<IPersistentCapabilityGrantRepository>(),
            provider.GetRequiredService<IServerConnectionService>(),
            provider.GetRequiredService<ISyncClient>(),
            provider.GetRequiredService<ISyncCursorRepository>()));

        // FileAssociations domain sync (ADR 0033): /etc/hackeros/file-associations.json under its own
        // domain string. The AppCatalog domain is retired by ADR 0048: uninstalls are apps.config
        // files, and the user layer travels with /home through the FileSystem domain.
        services.AddScoped<IFileAssociationsSyncService>(provider => new FileAssociationsSyncService(
            provider.GetRequiredService<IOsSettingsService>(),
            provider.GetRequiredService<IServerConnectionService>(),
            provider.GetRequiredService<ISyncClient>(),
            provider.GetRequiredService<ISyncCursorRepository>(),
            provider.GetRequiredService<ISyncRecordStateRepository>()));

        // Admin re-authentication elevation (ADR 0042) — powers the terminal's `sudo` built-in and
        // the admin-approval prompt on a denied permission. Never touches ISessionService's own
        // state machine; verifies credentials and mints short-lived, single-use tickets only.
        services.AddScoped<IElevationTicketStore, InMemoryElevationTicketStore>();
        services.AddScoped<IAdminElevationVerifier>(provider =>
        {
            WebCryptoPasswordHasher elevationHasher = provider.GetRequiredService<WebCryptoPasswordHasher>();
            return new AdminElevationVerifier(
                provider.GetRequiredService<ILocalUserRepository>(),
                provider.GetRequiredService<IAuditLog>(),
                asyncHasher: (password, salt, iterations, length, ct) =>
                    elevationHasher.DeriveKeyAsync(password, salt, iterations, length, ct));
        });
        services.AddScoped<IElevationService>(provider => new ElevationService(
            provider.GetRequiredService<IAdminElevationVerifier>(),
            provider.GetRequiredService<IElevationTicketStore>()));

        // Dangerous capabilities (ADR 0049): remembered decisions, the launch gate and its
        // secure-desktop overlay (the broker is the host's default approval prompt), and the
        // administration behind Settings > Permissions. Scoped: they read the session user's files.
        services.AddScoped<IAppPermissionStore>(provider => new AppPermissionStore(
            provider.GetRequiredService<IOsSettingsService>(),
            provider.GetService<ILogger<AppPermissionStore>>()));
        services.AddScoped<DangerousCapabilityApprovalBroker>();
        services.AddScoped<IDangerousCapabilityApprovalPrompt>(provider =>
            provider.GetRequiredService<DangerousCapabilityApprovalBroker>());
        services.AddScoped<IDangerousCapabilityGate>(provider => new DangerousCapabilityGate(
            provider.GetRequiredService<AppCatalog>(),
            provider.GetRequiredService<ICapabilityGrantRepository>(),
            provider.GetRequiredService<IAppPermissionStore>(),
            provider.GetRequiredService<IOsSettingsService>(),
            provider.GetRequiredService<IAdminElevationVerifier>(),
            provider.GetRequiredService<IAuditLog>(),
            provider.GetRequiredService<IDangerousCapabilityApprovalPrompt>()));
        // OS authentication agent (ADR 0055 section 5, polkit-like): gateways that need an
        // administrator ask it; its secure-desktop overlay is rendered by the shell.
        services.AddScoped<AdministratorAuthenticationBroker>();
        services.AddScoped<IAdministratorAuthenticationPrompt>(provider =>
            provider.GetRequiredService<AdministratorAuthenticationBroker>());
        services.AddScoped<IAdministratorAuthenticationAgent>(provider => new AdministratorAuthenticationAgent(
            provider.GetRequiredService<IAdminElevationVerifier>(),
            provider.GetRequiredService<IAuditLog>(),
            provider.GetRequiredService<IAdministratorAuthenticationPrompt>()));
        services.AddScoped<AppPermissionAdministration>(provider => new AppPermissionAdministration(
            provider.GetRequiredService<AppCatalog>(),
            provider.GetRequiredService<IAppPermissionStore>(),
            provider.GetRequiredService<IOsSettingsService>(),
            provider.GetRequiredService<IAdminElevationVerifier>(),
            provider.GetRequiredService<IAuditLog>()));
        // Reboot and factory reset (ADR 0056 section 11): the browser reloads the page and deletes
        // the OS database; HackSystem reaches them only after the authentication agent agreed.
        services.AddScoped<ISystemPowerDevice>(provider =>
            new BrowserSystemPowerDevice(provider.GetRequiredService<Microsoft.JSInterop.IJSRuntime>()));
        services.AddScoped<SystemPowerService>(provider => new SystemPowerService(
            provider.GetRequiredService<ISystemPowerDevice>(),
            provider.GetRequiredService<ISessionService>(),
            provider.GetRequiredService<LogFileWriter>(),
            provider.GetRequiredService<IAuditLog>()));
        // Raw storage snapshot behind HackStorageDiagnostics (ADR 0056 section 11).
        services.AddScoped<IStorageInspector>(provider =>
            new IndexedDbStorageInspector(provider.GetRequiredService<Microsoft.JSInterop.IJSRuntime>()));

        services.AddScoped<ISessionService>(provider =>
        {
            WebCryptoPasswordHasher hasher = provider.GetRequiredService<WebCryptoPasswordHasher>();
            return new LocalSessionService(
                provider.GetRequiredService<ILocalUserRepository>(),
                provider.GetRequiredService<FileSystemSeeder>(),
                provider.GetRequiredService<IEventBus>(),
                provider.GetRequiredService<IAuditLog>(),
                InstallationId.FromGuid(Guid.NewGuid()),
                DeviceId.FromGuid(Guid.NewGuid()),
                progressTracker: provider.GetRequiredService<ILoginProgressTracker>(),
                asyncHasher: (password, salt, iterations, length, ct) =>
                    hasher.DeriveKeyAsync(password, salt, iterations, length, ct),
                grantRepository: provider.GetService<ICapabilityGrantRepository>(),
                catalog: provider.GetService<AppCatalog>(),
                profileInitializer: provider.GetRequiredService<SettingsStoreToFilesMigration>());
        });
        // Per-process arguments, working directory and environment (ADR 0050): filled and
        // emptied by the process manager, read by /proc and each app's Environment gateway.
        services.AddScoped<InMemoryProcessEnvironmentTable>();
        services.AddScoped<IProcessEnvironmentTable>(provider =>
            provider.GetRequiredService<InMemoryProcessEnvironmentTable>());
        services.AddScoped<IProcessManager>(provider =>
            new InMemoryProcessManager(
                provider.GetRequiredService<ISimulationClock>(),
                provider.GetRequiredService<ISessionService>(),
                provider.GetRequiredService<IEventBus>(),
                environments: provider.GetRequiredService<InMemoryProcessEnvironmentTable>()));
        // Login environment (/etc/environment) and machine name (/etc/hostname), ADR 0050.
        services.AddScoped<LoginEnvironmentProvider>(provider => new LoginEnvironmentProvider(
            provider.GetRequiredService<IFileSystemService>(),
            provider.GetRequiredService<ISimulationClock>(),
            provider.GetService<ILogger<LoginEnvironmentProvider>>()));
        services.AddSingleton<IResourceSimulator>(provider => new DeterministicResourceSimulator(
            provider.GetRequiredService<ISimulationClock>(),
            provider.GetRequiredService<ISimulationRandom>(),
            VirtualHardwareProfile.Default));
        // The scheduler tick: samples every process once per simulated second (ADR 0057), started
        // by the boot sequence next to the clock driver.
        services.AddScoped<ResourceSamplingService>(provider => new ResourceSamplingService(
            provider.GetRequiredService<ISimulationClock>(),
            provider.GetRequiredService<IResourceSimulator>(),
            () => provider.GetRequiredService<IProcessManager>(),
            provider.GetRequiredService<ICpuTimeAccounting>(),
            provider.GetRequiredService<IProcessIoAccounting>()));

        services.AddScoped<IndexedDbAppCatalogRepository>();
        services.AddScoped<IPersistentAppCatalogRepository>(provider =>
            provider.GetRequiredService<IndexedDbAppCatalogRepository>());
        services.AddScoped<EcosystemBootCoordinator>();

        services.AddSingleton(_ => catalog ?? CreateEmptyCatalog());
        services.AddSingleton<AppEnablementRegistry>(provider =>
            new AppEnablementRegistry(provider.GetRequiredService<AppCatalog>()));
        services.AddSingleton<IAppEnablementRegistry>(provider =>
            provider.GetRequiredService<AppEnablementRegistry>());
        services.AddScoped<AppExecutionContextFactory>(provider => new AppExecutionContextFactory(
            provider.GetRequiredService<ICapabilityGrantRepository>(),
            provider.GetRequiredService<IFileSystemService>(),
            provider.GetRequiredService<IEventBus>(),
            provider.GetRequiredService<ITopicMessageBus>(),
            provider.GetRequiredService<INotificationQueue>(),
            provider.GetRequiredService<IDiagnosticSink>(),
            provider.GetRequiredService<ISimulationClock>(),
            provider.GetRequiredService<IProcessManager>(),
            // Resolved lazily: AppIntentDispatcher depends (transitively, via AppLifecycleOrchestrator)
            // on this very factory, so eagerly injecting it here would be circular.
            intentDispatcherProvider: () => provider.GetRequiredService<AppIntentDispatcher>(),
            logWriter: provider.GetRequiredService<LogFileWriter>(),
            environments: provider.GetRequiredService<IProcessEnvironmentTable>(),
            machineName: () => provider.GetRequiredService<LoginEnvironmentProvider>().HostName,
            // ADR 0056 section 6: each app's Network gateway, with the OS-owned real-network fallback.
            network: provider.GetRequiredService<ISimulatedNetworkService>(),
            serverConnection: provider.GetRequiredService<IServerConnectionService>(),
            proxy: provider.GetRequiredService<IProxyClient>(),
            // HackApps / HackAppManagement (ADR 0056 sections 2 and 3). Lazy for the same reason:
            // the installation service depends on the orchestrator, which depends on this factory.
            appCatalog: new AppCatalogGatewayServices(
                provider.GetRequiredService<AppCatalog>(),
                provider.GetRequiredService<IAppEnablementRegistry>(),
                () => provider.GetRequiredService<FileAssociationResolver>(),
                () => provider.GetRequiredService<IAppInstallationService>(),
                () => provider.GetRequiredService<IAdministratorAuthenticationAgent>()),
            // Lazy for the same reason: ShellInterpreter depends on the orchestrator (ADR 0056 section 7).
            shellHostProvider: () => provider.GetRequiredService<ShellHostService>(),
            // ADR 0057: traffic counters for /proc/net/dev and /proc/<pid>/io, and HackDriveInfo.
            metrics: new SystemMetricsServices(
                provider.GetRequiredService<INetworkInterfaceStatistics>(),
                provider.GetRequiredService<IProcessIoAccounting>(),
                new AppDriveGateway(
                    () => provider.GetRequiredService<IFileSystemMountRouter>(),
                    VirtualHardwareProfile.Default,
                    provider.GetRequiredService<IStorageUsageProvider>())),
            // HackClipboard (ADR 0056 section 12): one OS clipboard shared by every app of the session.
            clipboard: provider.GetRequiredService<HackerOs.Platform.Core.Clipboard.SystemClipboard>(),
            // HackSettings.OpenSystemAsync, HackPermissionAdministration, HackServerConnection,
            // HackSystem and HackStorageDiagnostics (ADR 0056 sections 8 to 11), all lazy.
            administration: new SystemAdministrationGatewayServices(
                () => provider.GetRequiredService<IOsSettingsService>(),
                () => provider.GetRequiredService<AppPermissionAdministration>(),
                new ServerConnectionGatewayServices(
                    () => provider.GetRequiredService<IServerConnectionService>(),
                    () => provider.GetRequiredService<IFileSystemSyncService>(),
                    () => provider.GetRequiredService<IGrantsSyncService>(),
                    () => provider.GetRequiredService<IFileAssociationsSyncService>()),
                () => provider.GetRequiredService<SystemPowerService>(),
                () => provider.GetRequiredService<IStorageInspector>(),
                () => provider.GetRequiredService<IAdministratorAuthenticationAgent>())));
        services.AddScoped<HackerOs.Platform.Core.Clipboard.SystemClipboard>(provider =>
            new HackerOs.Platform.Core.Clipboard.SystemClipboard(provider.GetRequiredService<IEventBus>()));
        // Shared clipboard with the real machine (ADR 0065), started by App.razor once settings exist.
        // Depends on IJSRuntime through its channel — see this file's IJSRuntime scoping note.
        services.AddScoped<HackerOs.Simulation.Abstractions.Clipboard.IHostClipboardChannel, HackerOs.Infrastructure.Browser.Interop.BrowserHostClipboardChannel>();
        services.AddScoped<HackerOs.Platform.Core.Clipboard.HostClipboardBridge>();
        services.AddScoped<FileAssociationResolver>(provider => new FileAssociationResolver(
            provider.GetRequiredService<AppCatalog>(),
            provider.GetRequiredService<IAppEnablementRegistry>(),
            provider.GetRequiredService<IOsSettingsService>()));

        // Simulated network (ADR 0034) — smoke-test seed only, not the full "Game domain" content
        // pack ADR 0023 scoped separately. Enough for curl/ping/nmap's simulated path to work now
        // that the commands themselves are wired into the catalog.
        services.AddSingleton<ISimulatedWebsiteRegistry>(_ =>
        {
            InMemorySimulatedWebsiteRegistry registry = new();
            foreach (ISimulatedWebsiteController controller in SmokeTestNetworkSeed.Websites)
            {
                registry.Register(controller);
            }

            return registry;
        });
        services.AddSingleton<ISimulatedNetworkService>(provider => new InMemorySimulatedNetworkService(
            SmokeTestNetworkSeed.Hosts,
            provider.GetRequiredService<ISimulatedWebsiteRegistry>()));

        services.AddScoped<AppLifecycleOrchestrator>(provider => new AppLifecycleOrchestrator(
            provider.GetRequiredService<AppCatalog>(),
            descriptorProvider?.Invoke(provider) ?? new Dictionary<string, AppDescriptor>(StringComparer.Ordinal),
            provider.GetRequiredService<AppEnablementRegistry>(),
            provider.GetRequiredService<IProcessManager>(),
            provider.GetRequiredService<ICapabilityGrantRepository>(),
            provider.GetRequiredService<AppExecutionContextFactory>(),
            provider.GetRequiredService<IEventBus>(),
            descriptorLoaderProvider?.Invoke(provider),
            provider,
            provider.GetRequiredService<IFileSystemService>(),
            provider.GetRequiredService<IElevationTicketStore>(),
            provider.GetRequiredService<IDangerousCapabilityGate>(),
            provider.GetRequiredService<LoginEnvironmentProvider>()));
        // Uninstall/reinstall through /etc/hackeros/apps.config + ~/.config/hackeros/apps.config
        // (ADR 0048). Started by EcosystemBootCoordinator once storage is ready; feeds the
        // AppEnablementRegistry every launch path consults.
        services.AddScoped<AppEnablementToFilesMigration>(provider => new AppEnablementToFilesMigration(
            provider.GetRequiredService<IFileSystemService>(),
            provider.GetRequiredService<ISimulationClock>(),
            provider.GetRequiredService<AppCatalog>(),
            provider.GetService<ILogger<AppEnablementToFilesMigration>>()));
        services.AddScoped<AppInstallationService>(provider => new AppInstallationService(
            provider.GetRequiredService<AppCatalog>(),
            provider.GetRequiredService<AppEnablementRegistry>(),
            provider.GetRequiredService<AppLifecycleOrchestrator>(),
            provider.GetRequiredService<IFileSystemService>(),
            provider.GetRequiredService<ISessionService>(),
            provider.GetRequiredService<ISimulationClock>(),
            provider.GetRequiredService<ITopicMessageBus>(),
            provider.GetRequiredService<IEventBus>(),
            provider.GetService<ILogger<AppInstallationService>>()));
        services.AddScoped<IAppInstallationService>(provider => provider.GetRequiredService<AppInstallationService>());
        // The headless shell hsh (ADR 0050): the terminal runs every line through it.
        services.AddScoped<ShellInterpreter>(provider => new ShellInterpreter(
            provider.GetRequiredService<AppLifecycleOrchestrator>(),
            provider.GetRequiredService<AppCatalog>(),
            provider.GetRequiredService<IAppEnablementRegistry>()));
        // Shells hosted by terminal emulators through HackShell (ADR 0056 section 7, shell.host).
        services.AddScoped<ShellHostService>(provider => new ShellHostService(
            provider.GetRequiredService<ShellInterpreter>(),
            provider.GetRequiredService<IProcessManager>(),
            provider.GetRequiredService<IElevationService>()));
        services.AddScoped<AppIntentDispatcher>(provider => new AppIntentDispatcher(
            provider.GetRequiredService<AppLifecycleOrchestrator>(),
            provider.GetRequiredService<AppCatalog>(),
            provider.GetRequiredService<IAppEnablementRegistry>(),
            provider.GetRequiredService<FileAssociationResolver>(),
            provider.GetRequiredService<ICapabilityGrantRepository>(),
            provider.GetRequiredService<IFileSystemService>()));

        services.AddSingleton<IFileSystemSelectedResourceHandleRegistry>(provider =>
            new FileSystemSelectedResourceHandleRegistry(
                provider.GetRequiredService<ISimulationClock>(),
                provider.GetRequiredService<ICapabilityGrantRepository>(),
                provider.GetRequiredService<IEventBus>()));
        services.AddSingleton<FileDialogServiceFactory>(provider => new FileDialogServiceFactory(
            provider.GetRequiredService<IFileSystemSelectedResourceHandleRegistry>()));
        services.AddScoped<FileDialogCoordinator>(provider =>
        {
            ISessionService sessionService = provider.GetRequiredService<ISessionService>();
            FileDialogServiceFactory factory = provider.GetRequiredService<FileDialogServiceFactory>();
            SessionId sessionId = sessionService.CurrentPrincipal?.SessionId ?? SessionId.FromGuid(Guid.Empty);
            return (FileDialogCoordinator)factory.Create(sessionId);
        });
        services.AddScoped<IFileDialogService>(provider => provider.GetRequiredService<FileDialogCoordinator>());
        services.AddScoped<DialogCoordinator>(provider => new DialogCoordinator(
            provider.GetRequiredService<IFileDialogService>()));
        services.AddScoped<IDialogService>(provider => provider.GetRequiredService<DialogCoordinator>());
        // Embeddable components registered in manifests (ADR 0063): a window embeds another app's
        // component by contract; the provider's assembly is loaded as a launch would load it.
        services.AddScoped<IHackComponentHost>(provider =>
        {
            AppLifecycleOrchestrator orchestrator = provider.GetRequiredService<AppLifecycleOrchestrator>();
            return new HackComponentHost(
                provider.GetRequiredService<AppCatalog>(),
                provider.GetRequiredService<IAppEnablementRegistry>(),
                orchestrator.EnsureDescriptorAsync);
        });
        services.AddSingleton<IWindowAppFrameworkLifecycle>(NullWindowAppFrameworkLifecycle.Instance);
        // Scoped, not Singleton: on the server host these back live desktop/window state for one
        // circuit. A Singleton here outlives circuit reconnects (e.g. a hard browser reload, which
        // tears down and recreates every Scoped service including AppLifecycleOrchestrator and its
        // running app instances) — the previous circuit's WindowRuntime.Windows entries would
        // survive with an OwnerInstanceId pointing at an app instance the new circuit's orchestrator
        // never launched, so DesktopShell's Orchestrator.TryGetRunningInstance lookup permanently
        // fails and the window is stuck rendering its dead "window-content-placeholder" fallback.
        // No-op behavior change for the WASM host, which has exactly one implicit scope anyway.
        services.AddScoped(_ => new WindowRuntime(new WindowBounds(0, 0, 1280, 720)));
        services.AddScoped<WindowLaunchCoordinator>();
        services.AddScoped<WindowCloseGuardRegistry>();
        services.AddScoped<AppBackHandlerRegistry>();
        // Depends on AppLifecycleOrchestrator, which is Scoped (see above) — see this file's
        // IJSRuntime scoping note.
        services.AddScoped<WindowCloseCoordinator>();
        services.AddScoped<FileDialogWindowAdapter>(provider => new FileDialogWindowAdapter(
            provider.GetRequiredService<FileDialogCoordinator>(),
            provider.GetRequiredService<WindowRuntime>(),
            provider.GetRequiredService<DialogCoordinator>()));
        // Depends on IFileSystemService, which is Scoped (see above) — see this file's IJSRuntime
        // scoping note.
        services.AddScoped<TestDemoFixtureSeeder>();

        services.AddSingleton<IIconCatalog, IconCatalog>();
        services.AddSingleton<IShellIconProvider, DefaultShellIconProvider>();

        // Depends on IJSRuntime through the browser probe — see this file's IJSRuntime scoping note.
        services.AddScoped<IPlatformEnvironmentProbe, BrowserPlatformEnvironmentProbe>();
        // Depends on IPlatformEnvironmentProbe, which is Scoped (see above) — see this file's
        // IJSRuntime scoping note.
        services.AddScoped<UiPlatformPreferenceService>();
        services.AddScoped<HackerOs.Platform.Core.Appearance.ThemePreferenceService>();
        services.AddScoped<WindowGeometryPersistence>();
        services.AddScoped<StartMenuPreferencesService>();
        // Depends on AppLifecycleOrchestrator and UiPlatformPreferenceService, both Scoped (see
        // above) — see this file's IJSRuntime scoping note.
        services.AddScoped<PlatformShellSwitchCoordinator>();

        return services;
    }

    private static AppCatalog CreateEmptyCatalog()
    {
        AppCatalogBuildResult result = AppCatalog.Build([]);
        return result.Catalog ?? throw new InvalidOperationException("The empty app catalog must be valid.");
    }
}
