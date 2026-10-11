using HackerOs.App.Abstractions;

namespace HackerOs.Apps.Terminal;

/// <summary>
/// Manifest of "Terminal" (ADR 0047). The build generates <c>manifest.json</c> from this
/// class; it is never instantiated at runtime.
/// </summary>
[AppManifest]
public sealed class TerminalApp : AppDefinition
{
    /// <inheritdoc />
    public override string Id => "org.hackeros.terminal";

    /// <inheritdoc />
    public override string Name => "Terminal";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Window;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(TerminalWindow);

    /// <inheritdoc />
    public override string PublisherId => "pub.hackeros";

    /// <inheritdoc />
    public override string Description => "Command line interface and interactive shell for HackerOS";

    /// <inheritdoc />
    public override IReadOnlyList<string> Capabilities =>
        [AppCapabilities.AppsLaunch, AppCapabilities.FileSystemUserHomeRead, AppCapabilities.FileSystemUserHomeWrite, AppCapabilities.FileSystemSystemRead, AppCapabilities.ProcessList, AppCapabilities.ProcessManage, AppCapabilities.NotificationsPost, AppCapabilities.ShellHost];

    /// <inheritdoc />
    public override bool SingleInstancePerUser => true;

    /// <inheritdoc />
    public override AppResourceProfileManifest Resources => new(
        BaselineCpuWeight: 0.05,
        BurstCpuWeight: 0.3,
        BaselineMemoryWeight: 0.05,
        BurstMemoryWeight: 0.2,
        BaselineStorageIoWeight: 0.02,
        BurstStorageIoWeight: 0.1,
        BaselineNetworkIoWeight: 0.0,
        BurstNetworkIoWeight: 0.0);
}
