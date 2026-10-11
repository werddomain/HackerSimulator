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

    private static HackComponentHost CreateHost(
        IReadOnlyList<AppManifest> manifests,
        IReadOnlyList<string>? disabled = null,
        IReadOnlyList<string>? systemAppIds = null,
        IReadOnlyList<string>? unavailable = null,
        List<string>? loads = null)
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
        });
    }

    private static AppManifest CreateManifest(string id, Type componentType) => new()
    {
        Id = id,
        Name = id,
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Provides a test component.",
        Kind = AppKind.Window,
        EntryPoint = new AppEntryPointManifest(AssemblyName, typeof(TestWindow).FullName!),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        Components = [new AppComponentManifest(Contract, AssemblyName, componentType.FullName!)]
    };

    public sealed class TestWindow : WindowAppBase;

    public sealed class TestEmbed : HackEmbeddableComponent;

    public sealed class NotEmbeddable;
}
