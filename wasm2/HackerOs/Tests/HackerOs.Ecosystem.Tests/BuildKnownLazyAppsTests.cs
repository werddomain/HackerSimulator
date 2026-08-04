using HackerOs.App.Abstractions;
using HackerOs.Platform.Core;

namespace HackerOs.Ecosystem.Tests;

/// <summary>Verifies the runnable host exposes every first-party system application.</summary>
public sealed class BuildKnownLazyAppsTests
{
    [Fact]
    public void Catalog_contains_all_first_party_system_apps()
    {
        AppCatalog catalog = BuildKnownLazyApps.Catalog;

        Assert.Equal(10, catalog.Manifests.Count);
        Assert.Contains("org.hackeros.browser", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.hack-paint", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.terminal", catalog.Manifests.Keys);
        Assert.All(catalog.Manifests.Values, manifest =>
            Assert.Equal(AppLaunchVisibility.Visible, manifest.Presentation.LaunchVisibility));
        Assert.Equal(10, BuildKnownLazyAssemblies.Names.Count);
    }
}
