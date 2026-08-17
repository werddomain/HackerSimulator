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

        Assert.Equal(36, catalog.Manifests.Count);
        Assert.Contains("org.hackeros.browser", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.diagnostic", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.hack-paint", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.terminal", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.commands.curl", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.commands.ping", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.commands.nmap", catalog.Manifests.Keys);
        Assert.DoesNotContain("org.hackeros.commands.cd", catalog.Manifests.Keys);
        Assert.DoesNotContain("org.hackeros.commands.pwd", catalog.Manifests.Keys);
        Assert.DoesNotContain("org.hackeros.commands.clear", catalog.Manifests.Keys);
        Assert.DoesNotContain("org.hackeros.commands.help", catalog.Manifests.Keys);

        // Window/service apps (the launcher-visible desktop suite) stay Visible; terminal
        // commands are deliberately Hidden — they're invoked by name from the terminal, not
        // double-clicked from a launcher, and shouldn't clutter it.
        Assert.All(catalog.Manifests.Values, manifest => Assert.Equal(
            manifest.Kind == AppKind.Terminal ? AppLaunchVisibility.Hidden : AppLaunchVisibility.Visible,
            manifest.Presentation.LaunchVisibility));
        Assert.Equal(36, BuildKnownLazyAssemblies.Names.Count);
    }
}
