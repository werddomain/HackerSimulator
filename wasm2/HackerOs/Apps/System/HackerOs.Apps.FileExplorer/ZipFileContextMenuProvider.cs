using HackerOs.App.Abstractions;
using HackerOs.AppSdk.FileView;
using HackerOs.AppSdk.FileView.ContextMenu;
using HackerOs.AppSdk.FileView.Icons;
using HackerOs.AppSdk.Icons;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Apps.FileExplorer;

/// <summary>
/// <c>INT-003</c> — the integration plan's worked example for <see cref="IFileViewContextMenuProvider"/>:
/// a <see cref="FileViewContextMenuScope.FileType"/> provider matching <c>.zip</c> files that inserts
/// <c>"UnZip Here…"</c> immediately after the default <c>"open"</c> item, reusing the exact same
/// extraction logic (<see cref="FileExplorerZipService"/>) the toolbar's own Extract button uses.
/// </summary>
public sealed class ZipFileContextMenuProvider : IFileViewContextMenuProvider
{
    private readonly IAppFileSystemGateway _fileSystem;
    private readonly Func<FileView?> _fileView;
    private readonly Action<string> _reportError;

    /// <param name="fileSystem">The host app's own scoped filesystem gateway.</param>
    /// <param name="fileView">
    /// Resolves the owning <see cref="FileView"/> lazily — the host constructs this provider before the
    /// component's own <c>@ref</c> is captured, so a direct instance can't be passed at construction time.
    /// </param>
    /// <param name="reportError">Invoked with a human-readable message when extraction fails.</param>
    public ZipFileContextMenuProvider(IAppFileSystemGateway fileSystem, Func<FileView?> fileView, Action<string> reportError)
    {
        _fileSystem = fileSystem;
        _fileView = fileView;
        _reportError = reportError;
    }

    /// <inheritdoc />
    public FileViewContextMenuScope Scope => FileViewContextMenuScope.FileType;

    /// <inheritdoc />
    public bool Matches(FileViewItem item) =>
        !item.IsDirectory && item.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Customize(FileViewContextMenuContext context, FileViewMenuItemCollection items)
    {
        if (context.Item is not { } item)
        {
            return;
        }

        items.InsertAfter("open", new FileViewMenuItem(
            "unzip-here",
            "UnZip Here…",
            ShellIconDescriptor.Vector(IconLibrary.Lucide, "package-open"),
            () => ExtractAsync(item)));
    }

    private async Task ExtractAsync(FileViewItem item)
    {
        FileView? fileView = _fileView();
        if (fileView is null)
        {
            return;
        }

        string folderName = item.FileName[..^".zip".Length];
        string currentDirectory = fileView.CurrentDirectory.Value;
        VirtualPath destinationDirectory = VirtualPath.Parse(
            currentDirectory == "/" ? $"/{folderName}" : $"{currentDirectory}/{folderName}");

        string? error = await FileExplorerZipService.ExtractAsync(_fileSystem, item.FullPath, destinationDirectory, CancellationToken.None);
        if (error is not null)
        {
            _reportError(error);
            return;
        }

        await fileView.RefreshAsync();
    }
}
