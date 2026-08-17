using HackerOs.App.Abstractions;
using HackerOs.Platform.Core;
using Xunit;

namespace HackerOs.Apps.Terminal.Tests;

public sealed class TerminalCommandResolverTests
{
    [Fact]
    public void GetTerminalCommands_ReturnsRegisteredManifests_OrderedByName_NotHardcoded()
    {
        AppCatalog catalog = BuildCatalog(
            Manifest("org.example.zeta", "zeta", "does zeta things", "zeta [--flag]"),
            Manifest("org.example.alpha", "alpha", "does alpha things", "alpha <arg>"));
        TerminalCommandResolver resolver = new(catalog);

        IReadOnlyList<AppManifest> commands = resolver.GetTerminalCommands();

        Assert.Equal(["alpha", "zeta"], commands.Select(m => m.Terminal!.Name));
        Assert.Equal("does alpha things", commands[0].Description);
        Assert.Equal("alpha <arg>", commands[0].Terminal!.Usage);
    }

    [Fact]
    public void GetTerminalCommands_ExcludesNonTerminalApps()
    {
        AppManifest windowApp = Manifest("org.example.window", "window-app", "a window app", "");
        windowApp = windowApp with { Kind = AppKind.Window, Terminal = null };
        AppCatalog catalog = BuildCatalog(
            Manifest("org.example.cmd", "cmd", "a real command", "cmd"),
            windowApp);
        TerminalCommandResolver resolver = new(catalog);

        IReadOnlyList<AppManifest> commands = resolver.GetTerminalCommands();

        Assert.Single(commands);
        Assert.Equal("cmd", commands[0].Terminal!.Name);
    }

    private static AppCatalog BuildCatalog(params AppManifest[] manifests)
    {
        AppCatalogBuildResult result = AppCatalog.Build(manifests);
        Assert.True(result.IsSuccess, string.Join(", ", result.Errors.Select(e => e.Message)));
        return result.Catalog!;
    }

    private static AppManifest Manifest(string id, string name, string description, string usage) => new()
    {
        SchemaVersion = 1,
        Id = id,
        Name = name,
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = description,
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest($"{name}.dll", $"{name}.Command"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest(name, [], usage),
        SingleInstancePerUser = false
    };
}
