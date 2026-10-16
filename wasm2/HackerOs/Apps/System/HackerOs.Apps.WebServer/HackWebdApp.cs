using HackerOs.App.Abstractions;
using HackerOs.Apps.WebServer.Daemon;

namespace HackerOs.Apps.WebServer;

/// <summary>
/// Manifest of <c>hackwebd</c>, the web server of the hosted static sites (ADR 0077, section 2). The
/// build generates its manifest from this class; it is never instantiated at runtime.
/// </summary>
/// <remarks>
/// ADR 0080, section 1: it runs on the main thread, not in a dedicated worker as ADR 0077 planned,
/// because it follows its configuration directories with <c>HackFileSystemWatcher</c> and a worker
/// does not offer file-system watches yet (the change feed is an event of the OS's heap).
/// </remarks>
[AppManifest(Execution = AppExecutionHost.MainThread)]
public sealed class HackWebdApp : AppDefinition
{
    /// <summary>The service name (<c>systemctl status hackwebd</c>).</summary>
    public const string AppId = "org.hackeros.hackwebd";

    /// <inheritdoc />
    public override string Id => AppId;

    /// <inheritdoc />
    public override string Name => "hackwebd";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Service;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(HackWebdService);

    /// <inheritdoc />
    public override string PublisherId => "pub.hackeros";

    /// <inheritdoc />
    public override string Description => "HackerOS web server: serves the static sites declared in /etc/hackweb/sites and ~/.config/hackweb/sites";

    /// <inheritdoc />
    public override string Category => "system";

    /// <inheritdoc />
    public override AppLaunchVisibility LaunchVisibility => AppLaunchVisibility.Hidden;

    /// <inheritdoc />
    /// <remarks>
    /// <c>network.listen.privileged</c> (dangerous, granted to a system app) binds port 80 for an
    /// administrator's site; <c>filesystem.system.read</c> reads <c>/etc/hackweb</c> and site roots
    /// outside the home; the home capabilities read user site roots and create
    /// <c>~/.config/hackweb/sites</c> on first start.
    /// </remarks>
    public override IReadOnlyList<string> Capabilities =>
    [
        AppCapabilities.NetworkListen,
        AppCapabilities.NetworkListenPrivileged,
        AppCapabilities.FileSystemSystemRead,
        AppCapabilities.FileSystemUserHomeRead,
        AppCapabilities.FileSystemUserHomeWrite,
    ];

    /// <inheritdoc />
    public override bool AutoStart => true;

    /// <inheritdoc />
    public override bool SingleInstancePerUser => true;

    /// <inheritdoc />
    public override AppResourceProfileManifest Resources => new(
        BaselineCpuWeight: 0.01,
        BurstCpuWeight: 0.1,
        BaselineMemoryWeight: 0.02,
        BurstMemoryWeight: 0.08,
        BaselineStorageIoWeight: 0.01,
        BurstStorageIoWeight: 0.2,
        BaselineNetworkIoWeight: 0.01,
        BurstNetworkIoWeight: 0.2);
}
