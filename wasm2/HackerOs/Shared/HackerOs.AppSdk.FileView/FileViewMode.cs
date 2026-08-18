namespace HackerOs.AppSdk.FileView;

/// <summary>
/// Selects which renderer <see cref="FileView"/> uses to display its current directory's contents. All
/// three modes share the same <see cref="FileViewItem"/> data and selection state — switching modes
/// never loses selection. See <c>docs/Global-FileView-And-MessagingSystem/FileViewControl.md</c>.
/// </summary>
public enum FileViewMode
{
    /// <summary>Lazily expandable hierarchical directory tree.</summary>
    Tree,

    /// <summary>Sortable table with per-column headers (Name, Kind, Size, Modified by default).</summary>
    Details,

    /// <summary>Wrapping grid of icon tiles.</summary>
    Icons
}
