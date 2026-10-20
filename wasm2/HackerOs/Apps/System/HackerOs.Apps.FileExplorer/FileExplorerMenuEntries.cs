namespace HackerOs.Apps.FileExplorer;

/// <summary>
/// The File Explorer's own entries in the file menus of its window (ADR 0155 §7): they used to be
/// providers of the old FileView menu, and become entries registered by their apps in chantier 4
/// (ADR 0151).
/// </summary>
internal static class FileExplorerMenuEntries
{
    /// <summary>"UnZip Here…" on one <c>.zip</c> file: extracts it next to itself, like the toolbar's Extract.</summary>
    internal const string UnzipHere = "unzip-here";

    /// <summary>
    /// "Publish as a web site…" on one folder (ADR 0077 §3): opens Web Server Manager with the folder as
    /// its first argument, which fills in a new hosted static site rooted there.
    /// </summary>
    internal const string PublishSite = "publish-site";

    /// <summary>The app id of Web Server Manager.</summary>
    internal const string WebServerManagerAppId = "org.hackeros.web-server-manager";
}
