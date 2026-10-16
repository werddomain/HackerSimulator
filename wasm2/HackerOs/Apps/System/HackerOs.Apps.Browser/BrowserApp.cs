using HackerOs.App.Abstractions;

namespace HackerOs.Apps.Browser;

/// <summary>
/// Manifest of "Browser" (ADR 0047). The build generates <c>manifest.json</c> from this
/// class; it is never instantiated at runtime.
/// </summary>
[AppManifest]
public sealed class BrowserApp : AppDefinition
{
    /// <inheritdoc />
    public override string Id => "org.hackeros.browser";

    /// <inheritdoc />
    public override string Name => "Browser";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Window;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(BrowserWindow);

    /// <inheritdoc />
    public override string PublisherId => "pub.hackeros";

    /// <inheritdoc />
    public override string Description =>
        "Simulated web browser for navigating the HackerOS virtual network";

    /// <inheritdoc />
    public override string Category => "internet";

    /// <inheritdoc />
    /// <remarks>
    /// Files (ADR 0075 §4 and §5): <c>file://</c> pages are read in the user's home with the user's
    /// rights; downloads go to <c>~/Downloads</c> and <c>localStorage</c> to
    /// <c>~/.local/share/hackeros/browser/storage</c>. The browser is not a system app, so it does not
    /// ask for <c>filesystem.system.read</c> (a dangerous capability needing an administrator):
    /// a <c>file://</c> URL outside the home answers <c>ERR_ACCESS_DENIED</c>.
    /// </remarks>
    public override IReadOnlyList<string> Capabilities =>
    [
        AppCapabilities.NetworkSimulatedRead,
        AppCapabilities.NetworkSimulatedWrite,
        AppCapabilities.FileSystemUserHomeRead,
        AppCapabilities.FileSystemUserHomeWrite,
    ];

    /// <inheritdoc />
    /// <remarks>Opening an <c>.html</c> file shows it as <c>file://</c> (ADR 0075 §4).</remarks>
    public override IReadOnlyList<FileHandler> FileHandlers =>
        [new("text/html", [".html", ".htm"], ["open"])];

    /// <inheritdoc />
    public override AppResourceProfileManifest Resources => new(
        BaselineCpuWeight: 0.05,
        BurstCpuWeight: 0.25,
        BaselineMemoryWeight: 0.08,
        BurstMemoryWeight: 0.3,
        BaselineStorageIoWeight: 0.0,
        BurstStorageIoWeight: 0.05,
        BaselineNetworkIoWeight: 0.05,
        BurstNetworkIoWeight: 0.2);
}
