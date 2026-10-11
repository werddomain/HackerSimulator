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

        Assert.Equal(46, catalog.Manifests.Count);
        Assert.Contains("org.hackeros.browser", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.diagnostic", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.hack-paint", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.terminal", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.commands.curl", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.commands.ping", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.commands.nmap", catalog.Manifests.Keys);
        // ADR 0051: cd/pwd/clear/help ship with the other commands in HackerOs.Commands.
        Assert.Contains("org.hackeros.cmd.cd", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.cmd.pwd", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.commands.clear", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.commands.help", catalog.Manifests.Keys);
        // ADR 0051 section 4: sudo and the environment commands are catalog commands too.
        Assert.Contains("org.hackeros.commands.sudo", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.commands.export", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.commands.unset", catalog.Manifests.Keys);
        Assert.Contains("org.hackeros.commands.env", catalog.Manifests.Keys);
        // ADR 0058 section 4: exit ends the shell that ran it, so the console can close the tab.
        Assert.Contains("org.hackeros.commands.exit", catalog.Manifests.Keys);

        // Window/service apps (the launcher-visible desktop suite) stay Visible; terminal
        // commands are deliberately Hidden — they're invoked by name from the terminal, not
        // double-clicked from a launcher, and shouldn't clutter it.
        Assert.All(catalog.Manifests.Values, manifest => Assert.Equal(
            manifest.Kind == AppKind.Terminal ? AppLaunchVisibility.Hidden : AppLaunchVisibility.Visible,
            manifest.Presentation.LaunchVisibility));
        // One assembly per window app, plus the single HackerOs.Commands assembly (ADR 0051).
        Assert.Equal(13, BuildKnownLazyAssemblies.Names.Count);
        Assert.Contains("HackerOs.Commands.dll", BuildKnownLazyAssemblies.Names);
    }

    [Fact]
    public void Every_app_comes_from_the_aggregated_BuiltInApplications_resource()
    {
        using Stream? resource = typeof(BuildKnownLazyApps).Assembly
            .GetManifestResourceStream(BuiltInApplicationsJsonSerializer.ResourceName);
        Assert.NotNull(resource);
        using StreamReader reader = new(resource);
        IReadOnlyList<BuiltInApplication> builtIn = BuiltInApplicationsJsonSerializer.DeserializeStrict(reader.ReadToEnd());

        // Every app is migrated to [AppManifest] (ADR 0047 §6): it reaches the host through its
        // ProjectReference only. No hand-written manifest resource and no manual lazy-load entry remain.
        Assert.DoesNotContain(
            typeof(BuildKnownLazyApps).Assembly.GetManifestResourceNames(),
            name => name.StartsWith("HackerOs.Ecosystem.Manifests.", StringComparison.Ordinal));
        Assert.Equal(
            builtIn.Select(item => item.Manifest.Id).Order(StringComparer.Ordinal),
            BuildKnownLazyApps.Catalog.Manifests.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            builtIn.Select(item => item.Manifest.EntryPoint!.Assembly).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
            BuildKnownLazyAssemblies.Names.Order(StringComparer.Ordinal));

        BuiltInApplication calculator = Assert.Single(builtIn, item => item.Manifest.Id == "org.hackeros.calculator");
        Assert.Equal("HackerOs.Apps.Calculator.dll", calculator.Manifest.EntryPoint!.Assembly);

        // Calculator's ProjectReference has no IsSystemApp metadata (ADR 0048 §1).
        Assert.False(calculator.IsSystemApp);
        Assert.False(BuildKnownLazyApps.Catalog.IsSystemApp("org.hackeros.calculator"));
    }

    [Fact]
    public void System_apps_follow_the_host_ProjectReference_metadata()
    {
        AppCatalog catalog = BuildKnownLazyApps.Catalog;
        string[] systemWindows =
        [
            // ADR 0056 §7: its terminal panel hosts a shell (shell.host) without an approval prompt.
            "org.hackeros.code-editor",
            "org.hackeros.error-log-viewer",
            "org.hackeros.file-explorer",
            "org.hackeros.settings",
            "org.hackeros.system-monitor",
            "org.hackeros.terminal",
            "org.hackeros.text-editor",
        ];
        string[] uninstallable =
        [
            "org.hackeros.browser",
            "org.hackeros.calculator",
            "org.hackeros.diagnostic",
            "org.hackeros.hack-paint",
            "org.hackeros.icon-viewer",
        ];

        // ADR 0048 §1 table: every command is a system app, like coreutils.
        string[] commands = [.. catalog.Manifests.Values
            .Where(manifest => manifest.Kind == AppKind.Terminal)
            .Select(manifest => manifest.Id)];
        Assert.Equal(34, commands.Length);

        Assert.Equal(
            systemWindows.Concat(commands).Order(StringComparer.Ordinal),
            catalog.SystemAppIds.Order(StringComparer.Ordinal));
        Assert.All(uninstallable, appId => Assert.False(catalog.IsSystemApp(appId)));
        Assert.Equal(catalog.Manifests.Count, catalog.SystemAppIds.Count + uninstallable.Length);
    }

    [Fact]
    public void No_non_system_app_of_the_host_declares_a_dangerous_capability()
    {
        // ADR 0049 "Conséquences": the base experience shows no approval window. Single, deliberate
        // exception (ADR 0056 section 11): Diagnostic stays uninstallable and its first launch asks an
        // administrator for system.storage.inspect, because the raw snapshot bypasses file rights.
        AppCatalog catalog = BuildKnownLazyApps.Catalog;
        const string diagnostic = "org.hackeros.diagnostic";

        Assert.All(
            catalog.Manifests.Values.Where(manifest => !catalog.IsSystemApp(manifest.Id) && manifest.Id != diagnostic),
            manifest => Assert.DoesNotContain(manifest.Capabilities, AppCapabilities.IsDangerous));
        Assert.False(catalog.IsSystemApp(diagnostic));
        Assert.Equal(
            [AppCapabilities.SystemStorageInspect],
            catalog.Manifests[diagnostic].Capabilities.Where(AppCapabilities.IsDangerous));
    }
}
