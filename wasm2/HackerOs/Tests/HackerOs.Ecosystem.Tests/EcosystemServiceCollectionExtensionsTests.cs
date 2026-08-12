using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk.Blazor;
using HackerOs.Ecosystem;
using HackerOs.Infrastructure.Browser.FileSystem;
using HackerOs.Infrastructure.Browser.Policy;
using HackerOs.Platform.Blazor.Windows;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Execution;
using HackerOs.Platform.Core.Intents;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Notifications;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Simulation.Abstractions.Settings;
using HackerOs.Simulation.Abstractions.Time;
using HackerOs.Windowing.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace HackerOs.Ecosystem.Tests;

public sealed class EcosystemServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddHackerOsEcosystem_resolves_complete_synchronous_graph()
    {
        ServiceCollection services = new();
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddSingleton<IJSRuntime, NonInvokedJsRuntime>();
        services.AddHackerOsEcosystem();

        await using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.IsType<IndexedDbFileSystemProvider>(provider.GetRequiredService<IFileSystemProvider>());
        Assert.Same(
            provider.GetRequiredService<IndexedDbCapabilityGrantRepository>(),
            provider.GetRequiredService<IPersistentCapabilityGrantRepository>());
        Assert.NotNull(provider.GetRequiredService<ISettingsDocumentService>());
        Assert.NotNull(provider.GetRequiredService<IFileSystemService>());
        Assert.NotNull(provider.GetRequiredService<ISessionService>());
        Assert.NotNull(provider.GetRequiredService<IProcessManager>());
        Assert.NotNull(provider.GetRequiredService<IResourceSimulator>());
        Assert.NotNull(provider.GetRequiredService<AppCatalog>());
        Assert.NotNull(provider.GetRequiredService<AppLifecycleOrchestrator>());
        Assert.NotNull(provider.GetRequiredService<AppIntentDispatcher>());
        Assert.NotNull(provider.GetRequiredService<WindowRuntime>());
        Assert.NotNull(provider.GetRequiredService<INotificationQueue>());
        Assert.NotNull(provider.GetRequiredService<ISimulationClock>());
        Assert.NotNull(provider.GetRequiredService<IDiagnosticSink>());
        Assert.NotNull(provider.GetRequiredService<IAuditLog>());
        Assert.NotNull(provider.GetRequiredService<IEventBus>());

        SessionId sessionId = SessionId.FromGuid(Guid.NewGuid());
        IFileDialogService dialogs = provider.GetRequiredService<FileDialogServiceFactory>().Create(sessionId);
        Assert.Equal(sessionId, Assert.IsType<HackerOs.Platform.Blazor.Dialogs.FileDialogCoordinator>(dialogs).SessionId);
    }

    private sealed class NonInvokedJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("DI graph validation must not invoke browser storage.");

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) =>
            throw new InvalidOperationException("DI graph validation must not invoke browser storage.");
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/");
        }
    }
}