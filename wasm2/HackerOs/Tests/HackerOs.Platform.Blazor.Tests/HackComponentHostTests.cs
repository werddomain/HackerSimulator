using System.Reflection;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Blazor;
using HackerOs.AppSdk.Blazor.Components;
using HackerOs.Platform.Blazor.Components;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Discovery;
using HackerOs.Platform.Core.Lifecycle;

namespace HackerOs.Platform.Blazor.Tests;

/// <summary>The OS registry of embeddable components (ADR 0063).</summary>
public sealed class HackComponentHostTests
{
    private const string Contract = "hackeros.tests-view";
    private static readonly string AssemblyName = typeof(TestEmbed).Assembly.GetName().Name + ".dll";

    [Fact]
    public async Task Resolves_the_registered_component_after_loading_its_app()
    {
        AppManifest provider = CreateManifest("org.hackeros.provider", typeof(TestEmbed));
        var loads = new List<string>();
        HackComponentHost host = CreateHost([provider], loads: loads);

        HackComponentResolution resolution = await host.ResolveAsync(Contract);

        Assert.True(resolution.IsResolved);
        Assert.Equal(typeof(TestEmbed), resolution.ComponentType);
        Assert.Equal("org.hackeros.provider", resolution.ProviderAppId);
        Assert.Equal(["org.hackeros.provider"], loads);
    }

    [Fact]
    public async Task Reports_when_no_installed_app_provides_the_contract()
    {
        AppManifest provider = CreateManifest("org.hackeros.provider", typeof(TestEmbed));
        HackComponentHost host = CreateHost([provider], disabled: ["org.hackeros.provider"]);

        HackComponentResolution resolution = await host.ResolveAsync(Contract);

        Assert.False(resolution.IsResolved);
        Assert.Contains("No installed app", resolution.Error);
    }

    [Fact]
    public async Task Rejects_a_type_that_is_not_an_embeddable_component()
    {
        AppManifest provider = CreateManifest("org.hackeros.provider", typeof(NotEmbeddable));
        HackComponentHost host = CreateHost([provider]);

        HackComponentResolution resolution = await host.ResolveAsync(Contract);

        Assert.False(resolution.IsResolved);
        Assert.Equal("org.hackeros.provider", resolution.ProviderAppId);
        Assert.Contains("not an embeddable component", resolution.Error);
    }

    [Fact]
    public async Task System_apps_are_tried_before_other_providers()
    {
        AppManifest thirdParty = CreateManifest("org.example.alpha", typeof(TestEmbed));
        AppManifest system = CreateManifest("org.hackeros.zeta", typeof(TestEmbed));
        HackComponentHost host = CreateHost([thirdParty, system], systemAppIds: ["org.hackeros.zeta"]);

        HackComponentResolution resolution = await host.ResolveAsync(Contract);

        Assert.Equal("org.hackeros.zeta", resolution.ProviderAppId);
    }

    [Fact]
    public async Task Falls_back_to_the_next_provider_when_one_does_not_load()
    {
        AppManifest broken = CreateManifest("org.hackeros.alpha", typeof(TestEmbed));
        AppManifest working = CreateManifest("org.hackeros.beta", typeof(TestEmbed));
        HackComponentHost host = CreateHost([broken, working], unavailable: ["org.hackeros.alpha"]);

        HackComponentResolution resolution = await host.ResolveAsync(Contract);

        Assert.Equal("org.hackeros.beta", resolution.ProviderAppId);
    }

    [Fact]
    public async Task A_platform_component_is_resolved_without_loading_any_app()
    {
        var loads = new List<string>();
        HackComponentHost host = CreateHost(
            [],
            loads: loads,
            platform: [new HackPlatformComponent(AppComponentContracts.ContextMenu, typeof(TestEmbed))]);

        HackComponentResolution resolution = await host.ResolveAsync(AppComponentContracts.ContextMenu);

        Assert.True(resolution.IsResolved);
        Assert.Equal(typeof(TestEmbed), resolution.ComponentType);
        Assert.Equal(HackComponentResolution.PlatformProviderId, resolution.ProviderAppId);
        Assert.Empty(loads);
    }

    [Fact]
    public void A_platform_component_must_be_embeddable()
    {
        Assert.Throws<ArgumentException>(() => new HackPlatformComponent(AppComponentContracts.ContextMenu, typeof(NotEmbeddable)));
    }

    [Fact]
    public async Task A_reserved_contract_is_never_given_to_an_app_that_is_not_a_system_app()
    {
        AppManifest impostor = CreateManifest("org.example.impostor", typeof(TestEmbed), AppComponentContracts.FileView);
        var errors = new List<string>();
        HackComponentHost host = CreateHost([impostor], errors: errors);

        HackComponentResolution resolution = await host.ResolveAsync(AppComponentContracts.FileView);

        Assert.False(resolution.IsResolved);
        Assert.Empty(host.GetProviders(AppComponentContracts.FileView));
        // ADR 0148 §5: the OS's own control missing is a fault, written to the system log.
        string error = Assert.Single(errors);
        Assert.Contains(AppComponentContracts.FileView, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_reserved_contract_is_given_to_a_system_app()
    {
        AppManifest explorer = CreateManifest("org.hackeros.explorer", typeof(TestEmbed), AppComponentContracts.FileView);
        var errors = new List<string>();
        HackComponentHost host = CreateHost([explorer], systemAppIds: ["org.hackeros.explorer"], errors: errors);

        HackComponentResolution resolution = await host.ResolveAsync(AppComponentContracts.FileView);

        Assert.Equal("org.hackeros.explorer", resolution.ProviderAppId);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task An_open_contract_without_provider_is_not_written_to_the_system_log()
    {
        var errors = new List<string>();
        HackComponentHost host = CreateHost([], errors: errors);

        HackComponentResolution resolution = await host.ResolveAsync(Contract);

        Assert.False(resolution.IsResolved);
        Assert.Empty(errors);
    }

    private static HackComponentHost CreateHost(
        IReadOnlyList<AppManifest> manifests,
        IReadOnlyList<string>? disabled = null,
        IReadOnlyList<string>? systemAppIds = null,
        IReadOnlyList<string>? unavailable = null,
        List<string>? loads = null,
        IReadOnlyList<HackPlatformComponent>? platform = null,
        List<string>? errors = null)
    {
        AppCatalog catalog = Assert.IsType<AppCatalog>(AppCatalog.Build(manifests, systemAppIds ?? []).Catalog);
        var enablement = new AppEnablementRegistry(catalog);
        enablement.MarkDisabled(disabled ?? []);
        return new HackComponentHost(catalog, enablement, (appId, _) =>
        {
            loads?.Add(appId);
            if (unavailable?.Contains(appId) == true)
            {
                return Task.FromResult<(AppDescriptor?, string?)>((null, "asset missing"));
            }

            AppManifest manifest = catalog.Manifests[appId];
            Assembly assembly = typeof(TestEmbed).Assembly;
            return Task.FromResult<(AppDescriptor?, string?)>((new AppDescriptor(manifest, typeof(TestWindow), assembly), null));
        }, platform, errors is null ? null : errors.Add);
    }

    private static AppManifest CreateManifest(string id, Type componentType, string contract = Contract) => new()
    {
        Id = id,
        Name = id,
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Provides a test component.",
        Kind = AppKind.Window,
        EntryPoint = new AppEntryPointManifest(AssemblyName, typeof(TestWindow).FullName!),
        SdkCompatibility = new AppSdkCompatibilityManifest(HackerOsSdk.Version),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        Components = [new AppComponentManifest(contract, AssemblyName, componentType.FullName!)]
    };

    public sealed class TestWindow : WindowAppBase;

    public sealed class TestEmbed : HackEmbeddableComponent;

    public sealed class NotEmbeddable;
}
