using HackerOs.AppSdk.FileView;
using HackerOs.AppSdk.FileView.ContextMenu;
using HackerOs.AppSdk.FileView.Icons;
using HackerOs.AppSdk.Icons;

namespace HackerOs.Apps.FileExplorer;

/// <summary>
/// "Publish as a web site…" on a folder (ADR 0077, section 3): opens Web Server Manager with the
/// folder as its first argument, which fills in a new hosted static site rooted there (a user site on
/// <c>http://localhost:&lt;free port&gt;/</c>), like IIS Manager's "Add Website" with a physical path.
/// </summary>
public sealed class PublishSiteContextMenuProvider : IFileViewContextMenuProvider
{
    /// <summary>The app id of Web Server Manager.</summary>
    public const string WebServerManagerAppId = "org.hackeros.web-server-manager";

    private readonly Func<string, string, Task> _launch;

    /// <param name="launch">Starts an app with one argument (the explorer's <c>LaunchWithAsync</c>).</param>
    public PublishSiteContextMenuProvider(Func<string, string, Task> launch)
    {
        _launch = launch ?? throw new ArgumentNullException(nameof(launch));
    }

    /// <inheritdoc />
    public FileViewContextMenuScope Scope => FileViewContextMenuScope.Directory;

    /// <inheritdoc />
    public bool Matches(FileViewItem item) => item.IsDirectory;

    /// <inheritdoc />
    public void Customize(FileViewContextMenuContext context, FileViewMenuItemCollection items)
    {
        if (context.Item is not { IsDirectory: true } item)
        {
            return;
        }

        items.InsertAfter("open", new FileViewMenuItem(
            "publish-site",
            "Publish as a web site…",
            ShellIconDescriptor.Vector(IconLibrary.Lucide, "globe"),
            () => _launch(WebServerManagerAppId, item.FullPath.Value)));
    }
}
