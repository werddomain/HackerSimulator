using HackerOs.AppSdk.Blazor.Components;
using HackerOs.AppSdk.Components;
using HackerOs.Simulation.Abstractions.FileSystem;
using Microsoft.AspNetCore.Components;

namespace HackerOs.AppSdk.Blazor.Files;

/// <summary>
/// The OS file control (contract <c>hackeros.file-view</c>, ADR 0148): a folder as a table, icons or
/// a tree, with selection, rename in place, drag and drop, the clipboard, the trash, the transfers
/// with their name conflicts, and the file menus of the OS. The File Explorer provides it at run time
/// and shows it itself, so a fix of the control reaches every app without a rebuild; the app only
/// holds this contract.
/// </summary>
/// <remarks>
/// The control runs with the <b>host</b> app's rights (ADR 0148 §4): it lists, copies and deletes what
/// the host may, and its menu commands run in the host's context. Its controller,
/// <see cref="IHackFileView"/>, comes through <c>ControllerChanged</c>.
/// </remarks>
/// <example>
/// <code>
/// &lt;HackFileView InitialDirectory="@home" Mode="HackFileViewMode.Icons"
///               Opening="OnOpening" ControllerChanged="view =&gt; _view = view" /&gt;
/// </code>
/// </example>
public sealed class HackFileView : HackContractComponent<IHackFileView>
{
    /// <summary>Gets or sets the absolute path of the folder shown first.</summary>
    [Parameter, EditorRequired]
    public string InitialDirectory { get; set; } = "/";

    /// <summary>Gets or sets the layout. Two-way bindable.</summary>
    [Parameter]
    public HackFileViewMode Mode { get; set; } = HackFileViewMode.Details;

    /// <summary>Gets or sets the callback told when the user changes the layout.</summary>
    [Parameter]
    public EventCallback<HackFileViewMode> ModeChanged { get; set; }

    /// <summary>Gets or sets what the control does by itself; <see cref="HackFileViewFeatures.All"/> by default.</summary>
    [Parameter]
    public HackFileViewFeatures Features { get; set; } = HackFileViewFeatures.All;

    /// <summary>Gets or sets whether several items can be selected at once.</summary>
    [Parameter]
    public bool AllowMultiSelect { get; set; } = true;

    /// <summary>
    /// Gets or sets which items are shown (a search box); call <see cref="IHackFileView.RefreshFilter"/>
    /// when what it returns changes.
    /// </summary>
    [Parameter]
    public Func<HackFileViewItem, bool>? Filter { get; set; }

    /// <summary>
    /// Gets or sets what runs before an item opens; setting <see cref="HackFileViewOpeningEventArgs.Handled"/>
    /// keeps the control from opening it (the default app for a file, navigation for a folder).
    /// </summary>
    [Parameter]
    public EventCallback<HackFileViewOpeningEventArgs> Opening { get; set; }

    /// <summary>Gets or sets the callback told the new folder after each navigation.</summary>
    [Parameter]
    public EventCallback<string> Navigated { get; set; }

    /// <summary>Gets or sets the callback told the new selection.</summary>
    [Parameter]
    public EventCallback<IReadOnlyList<HackFileViewItem>> SelectionChanged { get; set; }

    /// <summary>Gets or sets the callback told after a rename in place.</summary>
    [Parameter]
    public EventCallback<HackFileViewRenamedEventArgs> Renamed { get; set; }

    /// <summary>Gets or sets the callback told after items went to the trash or were deleted.</summary>
    [Parameter]
    public EventCallback<HackFileViewDeletedEventArgs> Deleted { get; set; }

    /// <summary>Gets or sets what runs before a file menu is drawn: hide, disable, add entries, or cancel.</summary>
    [Parameter]
    public EventCallback<HackFileMenuOpeningEventArgs> ContextMenuOpening { get; set; }

    /// <summary>
    /// Gets or sets the callback receiving the control's errors, worded for the user (a folder that
    /// cannot be read, a copy that failed). Without one, the control posts them as notifications.
    /// </summary>
    [Parameter]
    public EventCallback<string> ErrorOccurred { get; set; }

    /// <summary>
    /// Gets or sets the grant of a folder the user picked in the OS folder dialog
    /// (<c>FileDialogResult.Resource.Handle</c>): the control lists and renames inside it with that
    /// grant, as <c>IAppFileSystemGateway.WithSelectedHandle</c> does, beyond the host's own capabilities.
    /// <see langword="null"/> uses the host's capabilities alone.
    /// </summary>
    [Parameter]
    public FileSystemSelectedResourceHandle? SelectedResource { get; set; }

    /// <inheritdoc />
    protected override string Contract => HackComponentContracts.FileView;

    /// <inheritdoc />
    protected override void AddProviderParameters(IDictionary<string, object?> parameters)
    {
        parameters[nameof(InitialDirectory)] = InitialDirectory;
        parameters[nameof(Mode)] = Mode;
        parameters[nameof(ModeChanged)] = ModeChanged;
        parameters[nameof(Features)] = Features;
        parameters[nameof(AllowMultiSelect)] = AllowMultiSelect;
        parameters[nameof(Filter)] = Filter;
        parameters[nameof(Opening)] = Opening;
        parameters[nameof(Navigated)] = Navigated;
        parameters[nameof(SelectionChanged)] = SelectionChanged;
        parameters[nameof(Renamed)] = Renamed;
        parameters[nameof(Deleted)] = Deleted;
        parameters[nameof(ContextMenuOpening)] = ContextMenuOpening;
        parameters[nameof(ErrorOccurred)] = ErrorOccurred;
        parameters[nameof(SelectedResource)] = SelectedResource;
    }
}
