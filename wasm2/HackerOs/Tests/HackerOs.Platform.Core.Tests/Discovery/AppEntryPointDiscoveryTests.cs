using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.Discovery;

namespace HackerOs.Platform.Core.Tests.Discovery;

/// <summary>
/// Tests for `P1-APP-001`, `P1-APP-002`, and `P1-APP-003`: resolving catalog manifests to
/// concrete, correctly based entry-point types using only an explicit host assembly list.
/// </summary>
public sealed class AppEntryPointDiscoveryTests
{
    [Fact]
    public void Discover_resolves_terminal_and_service_manifests_to_their_concrete_entry_points()
    {
        AppManifest terminal = CreateManifest("org.hackeros.echo", AppKind.Terminal, typeof(EchoTerminalApp).FullName!) with
        {
            Terminal = new TerminalCommandManifest("echo", [], "echo [text]")
        };
        AppManifest service = CreateManifest("org.hackeros.waiter", AppKind.Service, typeof(WaitingServiceApp).FullName!);
        AppCatalog catalog = BuildCatalog(terminal, service);

        AppDiscoveryResult result = AppEntryPointDiscovery.Discover(catalog, HostAssemblies());

        Assert.True(result.IsSuccess);
        Assert.Equal(typeof(EchoTerminalApp), result.Descriptors![terminal.Id].EntryPointType);
        Assert.Equal(typeof(WaitingServiceApp), result.Descriptors![service.Id].EntryPointType);
    }

    [Fact]
    public void Discover_resolves_a_window_manifest_whose_entry_point_derives_from_the_expected_base_type_name()
    {
        // The real HackerOs.AppSdk.Blazor.WindowAppBase type lives in an assembly Platform Core
        // never references. Discovery matches by base-type full name, not object identity, so a
        // type with that exact namespace-qualified name (defined locally here) proves the same
        // resolution path a real window app would take without pulling in a Blazor dependency.
        AppManifest window = CreateManifest("org.hackeros.notes", AppKind.Window, typeof(TestWindowApp).FullName!);
        AppCatalog catalog = BuildCatalog(window);

        AppDiscoveryResult result = AppEntryPointDiscovery.Discover(catalog, HostAssemblies());

        Assert.True(result.IsSuccess);
        Assert.Equal(typeof(TestWindowApp), result.Descriptors![window.Id].EntryPointType);
    }

    [Fact]
    public void Discover_rejects_an_assembly_not_in_the_hosts_explicit_list()
    {
        AppManifest manifest = CreateManifest("org.hackeros.echo", AppKind.Terminal, typeof(EchoTerminalApp).FullName!) with
        {
            Terminal = new TerminalCommandManifest("echo", [], "echo [text]"),
            EntryPoint = new AppEntryPointManifest("Some.Unlisted.Assembly", typeof(EchoTerminalApp).FullName!)
        };
        AppCatalog catalog = BuildCatalog(manifest);

        AppDiscoveryResult result = AppEntryPointDiscovery.Discover(catalog, HostAssemblies());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "discovery.assembly.not-allowed" && e.AppId == manifest.Id);
    }

    [Fact]
    public void Discover_rejects_a_type_that_does_not_exist_in_the_assembly()
    {
        AppManifest manifest = CreateManifest("org.hackeros.echo", AppKind.Terminal, "DoesNotExist") with
        {
            Terminal = new TerminalCommandManifest("echo", [], "echo [text]")
        };
        AppCatalog catalog = BuildCatalog(manifest);

        AppDiscoveryResult result = AppEntryPointDiscovery.Discover(catalog, HostAssemblies());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "discovery.type.not-found" && e.AppId == manifest.Id);
    }

    [Fact]
    public void Discover_rejects_an_abstract_entry_point_type()
    {
        AppManifest manifest = CreateManifest("org.hackeros.echo", AppKind.Terminal, typeof(AbstractTerminalApp).FullName!) with
        {
            Terminal = new TerminalCommandManifest("echo", [], "echo [text]")
        };
        AppCatalog catalog = BuildCatalog(manifest);

        AppDiscoveryResult result = AppEntryPointDiscovery.Discover(catalog, HostAssemblies());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "discovery.type.not-concrete" && e.AppId == manifest.Id);
    }

    [Fact]
    public void Discover_rejects_a_type_that_does_not_derive_from_the_expected_base_for_its_kind()
    {
        AppManifest manifest = CreateManifest("org.hackeros.echo", AppKind.Service, typeof(EchoTerminalApp).FullName!);
        AppCatalog catalog = BuildCatalog(manifest);

        AppDiscoveryResult result = AppEntryPointDiscovery.Discover(catalog, HostAssemblies());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "discovery.type.wrong-base" && e.AppId == manifest.Id);
    }

    private static Dictionary<string, System.Reflection.Assembly> HostAssemblies() => new(StringComparer.Ordinal)
    {
        ["HackerOs.Platform.Core.Tests"] = typeof(AppEntryPointDiscoveryTests).Assembly
    };

    private static AppCatalog BuildCatalog(params AppManifest[] manifests)
    {
        AppCatalogBuildResult result = AppCatalog.Build(manifests);
        Assert.True(result.IsSuccess, string.Join(", ", result.Errors.Select(e => e.Message)));
        return result.Catalog!;
    }

    private static AppManifest CreateManifest(string id, AppKind kind, string entryTypeName) => new()
    {
        Id = id,
        Name = id,
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "Discovery test application.",
        Kind = kind,
        EntryPoint = new AppEntryPointManifest("HackerOs.Platform.Core.Tests", entryTypeName),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None
    };

    private sealed class EchoTerminalApp(AppManifest manifest) : TerminalAppBase(manifest)
    {
        public override ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(0);
    }

    private abstract class AbstractTerminalApp(AppManifest manifest) : TerminalAppBase(manifest);

    private sealed class WaitingServiceApp(AppManifest manifest) : ServiceAppBase(manifest)
    {
        protected override Task RunCoreAsync(IAppExecutionContext context, CancellationToken sessionCancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, sessionCancellationToken);
    }

    private sealed class TestWindowApp : HackerOs.AppSdk.Blazor.WindowAppBase;
}
