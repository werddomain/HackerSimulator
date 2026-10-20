using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Components;

namespace HackerOs.Apps.FileExplorer;

/// <summary>
/// Manifest of "File Explorer" (ADR 0047). The build generates <c>manifest.json</c> from this
/// class; it is never instantiated at runtime.
/// </summary>
[AppManifest]
public sealed class FileExplorerApp : AppDefinition
{
    /// <inheritdoc />
    public override string Id => "org.hackeros.file-explorer";

    /// <inheritdoc />
    public override string Name => "File Explorer";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Window;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(FileExplorerWindow);

    /// <inheritdoc />
    public override string PublisherId => "pub.hackeros";

    /// <inheritdoc />
    public override string Description => "Graphical virtual file manager and directory browser";

    /// <inheritdoc />
    public override IReadOnlyList<string> Capabilities =>
        [AppCapabilities.AppsLaunch, AppCapabilities.FileSystemUserHomeRead, AppCapabilities.FileSystemUserHomeWrite, AppCapabilities.FileSystemSystemRead, AppCapabilities.FileSystemSystemWrite, AppCapabilities.DialogFileOpen, AppCapabilities.DialogFileSave, AppCapabilities.DialogFolderSelect, AppCapabilities.ClipboardRead, AppCapabilities.ClipboardWrite, AppCapabilities.NotificationsPost, AppCapabilities.FileAssociationsRead];

    /// <inheritdoc />
    public override IReadOnlyList<FileHandler> FileHandlers => [new("inode/directory", [], ["open"])];

    /// <summary>
    /// The OS file control and its dialogs (ADR 0148, ADR 0155): every app shows the explorer's
    /// <c>HackFileView</c> and opens its "Open with", properties and conflict dialogs; the explorer
    /// shows them itself.
    /// </summary>
    public override IReadOnlyList<AppComponent> Components =>
    [
        new(HackComponentContracts.FileView, typeof(ExplorerFileView)),
        new(HackComponentContracts.FileDialogs, typeof(ExplorerFileDialogs))
    ];

    /// <inheritdoc />
    public override AppResourceProfileManifest Resources => new(
        BaselineCpuWeight: 0.05,
        BurstCpuWeight: 0.2,
        BaselineMemoryWeight: 0.05,
        BurstMemoryWeight: 0.2,
        BaselineStorageIoWeight: 0.05,
        BurstStorageIoWeight: 0.3,
        BaselineNetworkIoWeight: 0.0,
        BurstNetworkIoWeight: 0.0);
}
