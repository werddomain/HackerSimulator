using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Infrastructure.Browser.Catalog;
using HackerOs.Infrastructure.Browser.FileSystem;
using HackerOs.Infrastructure.Browser.Diagnostics;
using HackerOs.Infrastructure.Browser.Interop;
using HackerOs.Infrastructure.Browser.Policy;
using HackerOs.Infrastructure.Browser.Sessions;
using HackerOs.Infrastructure.Browser.Settings;
using HackerOs.Platform.Core;
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
using HackerOs.Platform.Blazor.Dialogs;
using HackerOs.Platform.Blazor.Windows;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Notifications;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Simulation.Abstractions.Time;
using HackerOs.AppSdk.Blazor;
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
        services.AddSingleton<ISimulationClock>(_ => new ManualSimulationClock(
            DateTimeOffset.UtcNow,
            TimeSpan.FromMilliseconds(100)));
        services.AddSingleton<ISimulationRandom>(_ => new SeededSimulationRandom(1));

        services.AddSingleton<IDiagnosticRedactor>(_ => new SensitiveKeyDiagnosticRedactor());
        services.AddSingleton<IEventBus>(_ => new InMemoryEventBus());
        services.AddSingleton<IDiagnosticSink>(provider => new EventPublishingDiagnosticSink(
            new BoundedDiagnosticSink(1024),
            provider.GetRequiredService<IEventBus>()));
        services.AddSingleton<IAuditLog>(_ => new BoundedAuditLog(512));
        services.AddSingleton<IndexedDbDiagnosticRepository>(provider => new IndexedDbDiagnosticRepository(
            provider.GetRequiredService<Microsoft.JSInterop.IJSRuntime>(),
            provider.GetRequiredService<IDiagnosticRedactor>(),
            1024));
        services.AddSingleton<IPersistentDiagnosticRepository>(provider =>
            provider.GetRequiredService<IndexedDbDiagnosticRepository>());
        services.AddSingleton<ILoggerProvider>(provider => new HackerOsDiagnosticLoggerProvider(
            provider.GetRequiredService<IDiagnosticSink>(),
            provider.GetRequiredService<IPersistentDiagnosticRepository>(),
            provider.GetRequiredService<ISimulationClock>()));
        // Keep this provider scoped to HackerOS's own log categories: the ASP.NET Core host
        // (Kestrel, hosting lifetime, HTTPS redirection, etc.) also resolves every registered
        // ILoggerProvider, and its framework-internal chatter isn't a HackerOS diagnostic event
        // -- and on the server side, IJSRuntime can't reach a real IndexedDB, so every one of
        // those entries would otherwise fail to persist.
        services.AddLogging(builder =>
        {
            builder.AddFilter<HackerOsDiagnosticLoggerProvider>("Microsoft", LogLevel.None);
            builder.AddFilter<HackerOsDiagnosticLoggerProvider>("System", LogLevel.None);
        });

        services.AddSingleton<HostExceptionReporter>();
        services.AddSingleton<INotificationQueue>(_ => new InMemoryNotificationQueue(100));

        services.AddSingleton<IndexedDbLocalUserRepository>();
        services.AddSingleton<ILocalUserRepository>(provider =>
            provider.GetRequiredService<IndexedDbLocalUserRepository>());
        services.AddSingleton<IndexedDbLocalGroupRepository>();
        services.AddSingleton<ILocalGroupRepository>(provider =>
            provider.GetRequiredService<IndexedDbLocalGroupRepository>());

        services.AddSingleton<IndexedDbSettingsDocumentService>(provider =>
            new IndexedDbSettingsDocumentService(
                provider.GetRequiredService<Microsoft.JSInterop.IJSRuntime>(),
                CreateSystemSettingsDefinitions()));
        services.AddSingleton<ISettingsDocumentService>(provider =>
            provider.GetRequiredService<IndexedDbSettingsDocumentService>());
        services.AddSingleton<IndexedDbCapabilityGrantRepository>();
        services.AddSingleton<IPersistentCapabilityGrantRepository>(provider =>
            provider.GetRequiredService<IndexedDbCapabilityGrantRepository>());
        services.AddSingleton<ICapabilityGrantRepository>(_ => new CapabilityGrantRepository());

        services.AddSingleton<IndexedDbFileSystemProvider>();
        services.AddSingleton<IndexedDbFileSystemBootstrapper>();
        services.AddSingleton<IFileSystemProvider>(provider =>
            provider.GetRequiredService<IndexedDbFileSystemProvider>());
        services.AddSingleton<IFileSystemMountRouter>(provider =>
            new FileSystemMountRouter(provider.GetRequiredService<IFileSystemProvider>()));
        services.AddSingleton<IFileSystemPathResolver, FileSystemPathResolver>();
        services.AddSingleton<IFileSystemAuthorizer, FileSystemAuthorizer>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddTransient<FileSystemSeeder>();

        services.AddSingleton<WebCryptoPasswordHasher>(provider =>
            new WebCryptoPasswordHasher(provider.GetRequiredService<Microsoft.JSInterop.IJSRuntime>()));
        services.AddSingleton<ILoginProgressTracker, LoginProgressTracker>();
        services.AddSingleton<ISessionService>(provider =>
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
                catalog: provider.GetService<AppCatalog>());
        });
        services.AddSingleton<IProcessManager>(provider =>
            new InMemoryProcessManager(
                provider.GetRequiredService<ISimulationClock>(),
                provider.GetRequiredService<ISessionService>(),
                provider.GetRequiredService<IEventBus>()));
        services.AddSingleton<IResourceSimulator>(provider => new DeterministicResourceSimulator(
            provider.GetRequiredService<ISimulationClock>(),
            provider.GetRequiredService<ISimulationRandom>(),
            VirtualHardwareProfile.Default));

        services.AddSingleton<IndexedDbAppCatalogRepository>();
        services.AddSingleton<IPersistentAppCatalogRepository>(provider =>
            provider.GetRequiredService<IndexedDbAppCatalogRepository>());
        services.AddSingleton<EcosystemBootCoordinator>();

        services.AddSingleton(_ => catalog ?? CreateEmptyCatalog());
        services.AddSingleton<AppEnablementRegistry>(provider =>
            new AppEnablementRegistry(provider.GetRequiredService<AppCatalog>()));
        services.AddSingleton<IAppEnablementRegistry>(provider =>
            provider.GetRequiredService<AppEnablementRegistry>());
        services.AddSingleton<AppExecutionContextFactory>(provider => new AppExecutionContextFactory(
            provider.GetRequiredService<ICapabilityGrantRepository>(),
            provider.GetRequiredService<IFileSystemService>(),
            provider.GetRequiredService<ISettingsDocumentService>(),
            provider.GetRequiredService<IEventBus>(),
            provider.GetRequiredService<INotificationQueue>(),
            provider.GetRequiredService<IDiagnosticSink>(),
            provider.GetRequiredService<ISimulationClock>(),
            provider.GetRequiredService<IProcessManager>(),
            // Resolved lazily: AppIntentDispatcher depends (transitively, via AppLifecycleOrchestrator)
            // on this very factory, so eagerly injecting it here would be circular.
            intentDispatcherProvider: () => provider.GetRequiredService<AppIntentDispatcher>()));
        services.AddSingleton<FileAssociationResolver>(provider => new FileAssociationResolver(
            provider.GetRequiredService<AppCatalog>(),
            provider.GetRequiredService<IAppEnablementRegistry>(),
            provider.GetRequiredService<ISettingsDocumentService>()));
        services.AddSingleton<AppLifecycleOrchestrator>(provider => new AppLifecycleOrchestrator(
            provider.GetRequiredService<AppCatalog>(),
            descriptorProvider?.Invoke(provider) ?? new Dictionary<string, AppDescriptor>(StringComparer.Ordinal),
            provider.GetRequiredService<AppEnablementRegistry>(),
            provider.GetRequiredService<IProcessManager>(),
            provider.GetRequiredService<ICapabilityGrantRepository>(),
            provider.GetRequiredService<AppExecutionContextFactory>(),
            provider.GetRequiredService<ISettingsDocumentService>(),
            provider.GetRequiredService<IEventBus>(),
            descriptorLoaderProvider?.Invoke(provider)));
        services.AddSingleton<AppIntentDispatcher>(provider => new AppIntentDispatcher(
            provider.GetRequiredService<AppLifecycleOrchestrator>(),
            provider.GetRequiredService<AppCatalog>(),
            provider.GetRequiredService<IAppEnablementRegistry>(),
            provider.GetRequiredService<FileAssociationResolver>(),
            provider.GetRequiredService<ICapabilityGrantRepository>()));

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
        services.AddSingleton<IWindowAppFrameworkLifecycle>(NullWindowAppFrameworkLifecycle.Instance);
        services.AddSingleton(_ => new WindowRuntime(new WindowBounds(0, 0, 1280, 720)));
        services.AddSingleton<WindowLaunchCoordinator>();
        services.AddSingleton<WindowCloseGuardRegistry>();
        services.AddSingleton<WindowCloseCoordinator>();
        services.AddScoped<FileDialogWindowAdapter>(provider => new FileDialogWindowAdapter(
            provider.GetRequiredService<FileDialogCoordinator>(),
            provider.GetRequiredService<WindowRuntime>(),
            provider.GetRequiredService<DialogCoordinator>()));
        services.AddSingleton<TestDemoFixtureSeeder>();

        services.AddSingleton<IIconCatalog, IconCatalog>();

        return services;
    }

    private static AppCatalog CreateEmptyCatalog()
    {
        AppCatalogBuildResult result = AppCatalog.Build([]);
        return result.Catalog ?? throw new InvalidOperationException("The empty app catalog must be valid.");
    }

    private static SettingsDocumentDefinition[] CreateSystemSettingsDefinitions() =>
    [
        PolicySettingsDocuments.CreateDefinition(),
        FileAssociationSettingsDocuments.CreateDefinition(),
        HackerOs.Platform.Core.Appearance.AppearanceSettingsDocuments.CreateDefinition()
    ];
}
