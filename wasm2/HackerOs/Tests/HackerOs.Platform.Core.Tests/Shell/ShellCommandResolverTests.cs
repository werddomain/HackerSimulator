using HackerOs.App.Abstractions;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Platform.Core.Shell;
using Xunit;

namespace HackerOs.Platform.Core.Tests.Shell;

public sealed class ShellCommandResolverTests
{
    [Fact]
    public void GetTerminalCommands_ReturnsRegisteredManifests_OrderedByName_NotHardcoded()
    {
        AppCatalog catalog = BuildCatalog(
            Manifest("org.example.zeta", "zeta", "does zeta things", "zeta [--flag]"),
            Manifest("org.example.alpha", "alpha", "does alpha things", "alpha <arg>"));
        ShellCommandResolver resolver = new(catalog);

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
        ShellCommandResolver resolver = new(catalog);

        IReadOnlyList<AppManifest> commands = resolver.GetTerminalCommands();

        Assert.Single(commands);
        Assert.Equal("cmd", commands[0].Terminal!.Name);
    }

    [Fact]
    public void Uninstalled_commands_are_neither_resolved_nor_listed()
    {
        // ADR 0048: an uninstalled command answers "command not found", like a removed binary.
        AppCatalog catalog = BuildCatalog(
            Manifest("org.example.cmd", "cmd", "a real command", "cmd"),
            Manifest("org.example.gone", "gone", "an uninstalled command", "gone"));
        AppEnablementRegistry enablement = new(catalog);
        enablement.MarkDisabled(["org.example.gone"]);
        ShellCommandResolver resolver = new(catalog, enablement);

        Assert.Null(resolver.Resolve("gone", []));
        Assert.NotNull(resolver.Resolve("cmd", []));
        Assert.Equal(["cmd"], resolver.GetTerminalCommands().Select(m => m.Terminal!.Name));
    }

    private static AppCatalog BuildCatalog(params AppManifest[] manifests)
    {
        AppCatalogBuildResult result = AppCatalog.Build(manifests);
        Assert.True(result.IsSuccess, string.Join(", ", result.Errors.Select(e => e.Message)));
        return result.Catalog!;
    }

    [Fact]
    public void Resolve_finds_terminal_apps_by_name_or_alias()
    {
        AppCatalog catalog = BuildCatalog(Manifest("org.hackeros.echo", "echo", "Echoes", "echo [text]", aliases: ["print"]));
        ShellCommandResolver resolver = new(catalog);

        Assert.Equal("org.hackeros.echo", resolver.Resolve("echo", ["hello"])?.AppId);
        Assert.Equal("org.hackeros.echo", resolver.Resolve("print", ["hello"])?.AppId);
        Assert.Null(resolver.Resolve("nonexistent", []));
    }

    private static AppManifest Manifest(string id, string name, string description, string usage, IReadOnlyList<string>? aliases = null) => new()
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
        Terminal = new TerminalCommandManifest(name, aliases ?? [], usage),
        SingleInstancePerUser = false
    };
}
