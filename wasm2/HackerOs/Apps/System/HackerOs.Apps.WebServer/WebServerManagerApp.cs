using HackerOs.App.Abstractions;
using HackerOs.Apps.WebServer.Manager;

namespace HackerOs.Apps.WebServer;

/// <summary>
/// Manifest of "Web Server Manager", the IIS-like window that drives <c>hackwebd</c> through its
/// <c>.site</c> files and the service controller (ADR 0077, section 3). The build generates its
/// manifest from this class; it is never instantiated at runtime.
/// </summary>
[AppManifest]
public sealed class WebServerManagerApp : AppDefinition
{
    /// <summary>The app id, which the File Explorer launches with a folder to publish.</summary>
    public const string AppId = "org.hackeros.web-server-manager";

    /// <inheritdoc />
    public override string Id => AppId;

    /// <inheritdoc />
    public override string Name => "Web Server Manager";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Window;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(WebServerManagerWindow);

    /// <inheritdoc />
    public override string PublisherId => "pub.hackeros";

    /// <inheritdoc />
    public override string Description => "Publish folders as web sites and manage the hackwebd web server";

    /// <inheritdoc />
    public override string Category => "system";

    /// <inheritdoc />
    /// <remarks>
    /// <c>filesystem.system.write</c> writes <c>/etc/hackweb/sites</c> (the files are
    /// <c>system:system</c>, so each write still needs an administrator's approval);
    /// <c>filesystem.system.read</c> reads the system sites and <c>/var/log/hackweb</c>;
    /// <c>apps.launch</c> opens a site in the browser; <c>process.list</c> shows which sockets
    /// <c>hackwebd</c> holds.
    /// </remarks>
    public override IReadOnlyList<string> Capabilities =>
    [
        AppCapabilities.FileSystemSystemRead,
        AppCapabilities.FileSystemSystemWrite,
        AppCapabilities.FileSystemUserHomeRead,
        AppCapabilities.FileSystemUserHomeWrite,
        AppCapabilities.DialogFolderSelect,
        AppCapabilities.AppsLaunch,
        AppCapabilities.ProcessList,
        AppCapabilities.NotificationsPost,
    ];

    /// <inheritdoc />
    public override bool SingleInstancePerUser => true;

    /// <inheritdoc />
    public override AppResourceProfileManifest Resources => new(
        BaselineCpuWeight: 0.02,
        BurstCpuWeight: 0.1,
        BaselineMemoryWeight: 0.04,
        BurstMemoryWeight: 0.1,
        BaselineStorageIoWeight: 0.0,
        BurstStorageIoWeight: 0.05,
        BaselineNetworkIoWeight: 0.0,
        BurstNetworkIoWeight: 0.0);
}
