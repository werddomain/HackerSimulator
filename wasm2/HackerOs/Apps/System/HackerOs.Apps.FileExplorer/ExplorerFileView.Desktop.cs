using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Apps;
using HackerOs.AppSdk.Blazor;
using HackerOs.AppSdk.Blazor.ContextMenus;
using HackerOs.AppSdk.Blazor.Files;
using HackerOs.AppSdk.FileView;
using HackerOs.AppSdk.FileView.Events;
using HackerOs.AppSdk.FileView.Icons;
using HackerOs.AppSdk.Files;
using HackerOs.AppSdk.Icons;
using HackerOs.AppSdk.IO;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Events;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Apps.FileExplorer;

/// <summary>
/// The desktop mode of the control and its <c>.desktop</c> shortcuts (ADR 0152, ADR 0160 §2, §5-6): the
/// Home and Trash icons with their menus and drops, the positions handed to the host, and the shortcuts
/// shown with their app's icon in every mode.
/// </summary>
public sealed partial class ExplorerFileView
{
    /// <summary>The explorer's own id: opening the Trash icon starts it on <c>trash:///</c>.</summary>
    private const string FileExplorerAppId = "org.hackeros.file-explorer";

    private readonly string _systemMenuName = "hackeros.file-view.desktop." + Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, ShortcutState> _shortcuts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _pendingShortcuts = new(StringComparer.Ordinal);
    private FileViewDesktopBinding? _desktop;
    private IDisposable? _systemMenu;
    private IReadOnlyDictionary<string, HackAppInfo>? _apps;
    private bool _loadingShortcuts;
    private bool _trashWatched;
    private CancellationTokenSource? _trashWatchCts;
    private ITopicChannelSubscription<FileSystemChangeEvent>? _trashWatch;

    /// <summary>Gets or sets the <c>[view]</c> options of the desktop mode.</summary>
    [Parameter]
    public HackDesktopOptions? DesktopOptions { get; set; }

    /// <summary>Gets or sets the saved cell of each desktop icon.</summary>
    [Parameter]
    public IReadOnlyDictionary<string, HackDesktopCell>? DesktopPositions { get; set; }

    /// <summary>Gets or sets the callback receiving the positions to save.</summary>
    [Parameter]
    public EventCallback<IReadOnlyDictionary<string, HackDesktopCell>> DesktopPositionsChanged { get; set; }

    private bool IsDesktop => Mode == HackFileViewMode.Desktop;

    /// <summary>Wires the shortcut decoration and, on a desktop, the binding of the grid (on each parameter change).</summary>
    private void ConfigureDesktop()
    {
        _binding.Decorate = DecorateShortcut;
        if (!IsDesktop)
        {
            _binding.Desktop = null;
            return;
        }

        if (_desktop is null)
        {
            _desktop = new FileViewDesktopBinding
            {
                SavePositions = positions => DesktopPositionsChanged.InvokeAsync(positions),
                AttachSystemMenu = key => Menus.Attach(_systemMenuName, key),
                OpenSystemItem = OpenSystemItemAsync,
                DropOnSystemItem = DropOnSystemItemAsync
            };
            _systemMenu = Menus.Define(_systemMenuName, static _ => { }, OnSystemMenuOpeningAsync);
        }

        _desktop.Options = DesktopOptions ?? HackDesktopOptions.Default;
        _desktop.Positions = DesktopPositions ?? new Dictionary<string, HackDesktopCell>();
        _desktop.HomeLabel = L["Desktop.Home"];
        _desktop.TrashLabel = L["Desktop.Trash"];
        _desktop.GridLabel = L["Desktop.Grid"];
        _binding.Desktop = _desktop;
    }

    /// <summary>On a desktop, reads whether the trash holds something and follows it.</summary>
    private async Task StartDesktopAsync()
    {
        if (!IsDesktop || _trashWatched)
        {
            return;
        }

        _trashWatched = true;
        using IDisposable scope = HackExecutionContext.Enter(HostContext);
        await RefreshTrashStateAsync();
        try
        {
            string files = HackTrash.TrashDirectory + "/files";
            _trashWatch = await HostContext.Watch.WatchAsync(VirtualPath.Parse(files), FileSystemWatchScope.ImmediateChildren, HostContext.CancellationToken);
            _trashWatchCts = new CancellationTokenSource();
            _ = WatchTrashAsync(_trashWatch, _trashWatchCts.Token);
        }
        catch (Exception exception) when (exception is AppGatewayAccessDeniedException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            // No watch (the trash does not exist yet, or a host without one): the icon follows this
            // control's own deletions only.
        }
    }

    private async Task WatchTrashAsync(ITopicChannelSubscription<FileSystemChangeEvent> subscription, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (TopicMessage<FileSystemChangeEvent> _ in subscription.Reader.ReadAllAsync(cancellationToken))
            {
                await InvokeAsync(async () =>
                {
                    using IDisposable scope = HackExecutionContext.Enter(HostContext);
                    await RefreshTrashStateAsync();
                });
            }
        }
        catch (OperationCanceledException)
        {
            // The control is gone.
        }
    }

    private async Task StopDesktopAsync()
    {
        _systemMenu?.Dispose();
        _trashWatchCts?.Cancel();
        _trashWatchCts?.Dispose();
        _trashWatchCts = null;
        if (_trashWatch is not null)
        {
            await _trashWatch.DisposeAsync();
            _trashWatch = null;
        }
    }

    /// <summary>Draws the Trash icon full or empty (ADR 0160 §5).</summary>
    private async Task RefreshTrashStateAsync()
    {
        if (_desktop is null)
        {
            return;
        }

        bool full;
        try
        {
            full = (await HackTrash.GetItemsAsync(HostContext.CancellationToken)).Count > 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or AppGatewayAccessDeniedException or NotSupportedException)
        {
            return;
        }

        if (full != _desktop.IsTrashFull)
        {
            _desktop.IsTrashFull = full;
            StateHasChanged();
        }
    }

    private string HomeDirectory => HackerOs.AppSdk.HackUser.Describe(HostContext).HomeDirectory;

    // ---- Home and Trash ------------------------------------------------------------------------------

    /// <summary>Opens Home in a new explorer window, the Trash in the explorer's trash view.</summary>
    private async Task OpenSystemItemAsync(string key)
    {
        try
        {
            if (key == HackDesktopItems.Home)
            {
                await HostContext.Intents.OpenFileAsync(VirtualPath.Parse(HomeDirectory), "inode/directory", HostContext.CancellationToken);
            }
            else if (key == HackDesktopItems.Trash)
            {
                await HostContext.Intents.LaunchAsync(FileExplorerAppId, [FileExplorerWindow.TrashArgument], HostContext.CancellationToken);
            }
        }
        catch (Exception exception) when (exception is AppGatewayAccessDeniedException or NotSupportedException)
        {
            await ReportErrorAsync(L["Error.LaunchDenied"]);
        }
    }

    /// <summary>A drop on Home moves (or copies) into the home folder; a drop on the Trash trashes the entries.</summary>
    private async Task DropOnSystemItemAsync(string key, FileViewDropRequestedEventArgs drop)
    {
        if (key == HackDesktopItems.Home)
        {
            await TransferDropAsync(drop, VirtualPath.Parse(HomeDirectory));
            return;
        }

        if (key != HackDesktopItems.Trash || drop.Entries.Count == 0 || !Has(HackFileViewFeatures.Delete))
        {
            return;
        }

        List<string> failures = [];
        async Task TrashAllAsync()
        {
            foreach (FileViewDraggedEntry entry in drop.Entries)
            {
                try
                {
                    await HackTrash.MoveToTrashAsync(entry.Path.Value, HostContext.CancellationToken);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    failures.Add(L["Error.TrashMove", entry.Path.Value, exception.Message]);
                }
            }
        }

        await TrackOperation(TrashAllAsync());
        _view?.ClearSelect();
        await RefreshAfterChangeAsync();
        await RefreshTrashStateAsync();
        if (failures.Count > 0)
        {
            await ReportErrorAsync(string.Join("; ", failures));
        }
    }

    /// <summary>The menu of the Home or Trash icon (ADR 0160 §5): no file entries, no app entries.</summary>
    private async Task OnSystemMenuOpeningAsync(HackMenuOpeningEventArgs args)
    {
        if (args.Item is not string key || !HackDesktopItems.IsSystemItem(key))
        {
            args.Cancel = true;
            return;
        }

        if (key == HackDesktopItems.Trash)
        {
            await RefreshTrashStateAsync();
        }

        args.Menu.Groups.Clear();
        HackMenuGroupDefinition main = args.Menu.AddGroup();
        SystemEntry(main, HackFileMenuItems.Open, L["Menu.Open"], "folder-open", () => OpenSystemItemAsync(key));
        if (key == HackDesktopItems.Trash)
        {
            HackMenuItemDefinition empty = SystemEntry(main, HackFileMenuItems.EmptyTrash, L["Menu.EmptyTrash"], "trash-2", EmptyTrashAsync);
            empty.IsDisabled = _desktop is not { IsTrashFull: true };
        }

        if (Has(HackFileViewFeatures.Properties))
        {
            SystemEntry(args.Menu.AddGroup(), HackFileMenuItems.Properties, L["Menu.Properties"], "info",
                () => ShowPropertiesAsync(key == HackDesktopItems.Home ? HomeDirectory : HackTrash.TrashDirectory));
        }
    }

    private HackMenuItemDefinition SystemEntry(HackMenuGroupDefinition group, string id, string title, string icon, Func<Task> run)
    {
        HackMenuItemDefinition entry = group.Add(id, title);
        entry.Icon = HackIcons.Vector(IconLibrary.Lucide, icon);
        entry.OnClick = EventCallback.Factory.Create<object?>(this, _ => run());
        return entry;
    }

    /// <summary>"Empty Trash", after a confirmation: everything in it is deleted for good.</summary>
    private async Task EmptyTrashAsync()
    {
        if (Dialogs is not { } dialogs)
        {
            await ReportErrorAsync(L["Error.DialogsUnavailable"]);
            return;
        }

        MessageBoxDialogResult answer = await dialogs.MessageBoxAsync(
            HostContext,
            new MessageBoxDialogRequest { Title = L["Dialog.EmptyTrash.Title"], Content = L["Dialog.EmptyTrash.Content"], DialogType = MessageBoxType.YesNo },
            HostContext.CancellationToken);
        if (answer.Result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await TrackOperation(HackTrash.EmptyAsync(HostContext.CancellationToken));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await ReportErrorAsync(L["Error.EmptyTrash", exception.Message]);
        }

        await RefreshTrashStateAsync();
    }

    // ---- Shortcuts (.desktop) ------------------------------------------------------------------------

    /// <summary>
    /// Shows a <c>.desktop</c> shortcut with its name and its app's icon, greyed when the app is not
    /// installed (ADR 0152 §5). The file is read once per revision, after the listing: the item is drawn
    /// as a plain file until then.
    /// </summary>
    private void DecorateShortcut(FileViewItem item)
    {
        if (item.IsDirectory || !HackDesktopEntry.IsDesktopFile(item.FileName))
        {
            return;
        }

        string path = item.FullPath.Value;
        if (_shortcuts.TryGetValue(path, out ShortcutState? state) && state.Revision == item.Metadata.Revision)
        {
            if (state.Entry is { } entry)
            {
                HackAppInfo? app = _apps?.GetValueOrDefault(entry.AppId);
                item.DisplayName = entry.Name;
                item.IconOverride = ShellIconDescriptor.Vector(IconLibrary.Lucide, HackAppIcons.LucideName(entry.AppId, app?.Category));
                item.IsDimmed = app is null;
            }

            return;
        }

        long size = item.Metadata is FileMetadata file ? file.Length : 0;
        if (size > HackDesktopEntry.MaxFileSize)
        {
            _shortcuts[path] = new ShortcutState(item.Metadata.Revision, null);
            return;
        }

        _pendingShortcuts[path] = item.Metadata.Revision;
        if (!_loadingShortcuts)
        {
            _loadingShortcuts = true;
            _ = InvokeAsync(async () =>
            {
                // Runs outside any event of the host: its rights are made ambient for the reads.
                using IDisposable scope = HackExecutionContext.Enter(HostContext);
                await LoadShortcutsAsync();
            });
        }
    }

    /// <summary>Reads the shortcuts listed but not read yet, and the catalog, then redraws the items.</summary>
    private async Task LoadShortcutsAsync()
    {
        try
        {
            // After the listing that asked for them has finished its render.
            await Task.Yield();
            _apps = await ReadInstalledAppsAsync();
            while (_pendingShortcuts.Count > 0)
            {
                (string path, long revision) = _pendingShortcuts.First();
                _pendingShortcuts.Remove(path);
                _shortcuts[path] = new ShortcutState(revision, await ReadShortcutAsync(path));
            }
        }
        finally
        {
            _loadingShortcuts = false;
        }

        _view?.RefreshFilter();
    }

    private async Task<IReadOnlyDictionary<string, HackAppInfo>> ReadInstalledAppsAsync()
    {
        try
        {
            IReadOnlyList<HackAppInfo> apps = await HostContext.Catalog.GetInstalledAsync(HostContext.CancellationToken);
            return apps.GroupBy(static app => app.Id, StringComparer.Ordinal).ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is AppGatewayAccessDeniedException or NotSupportedException)
        {
            return new Dictionary<string, HackAppInfo>(StringComparer.Ordinal);
        }
    }

    private async Task<HackDesktopEntry?> ReadShortcutAsync(string path)
    {
        try
        {
            string content = await HackFile.ReadAllTextAsync(path, HostContext.CancellationToken);
            return HackDesktopEntry.TryParse(content, out HackDesktopEntry? entry) ? entry : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Opening a shortcut launches its app with its arguments (ADR 0152 §5); <see langword="false"/> when
    /// the file is not a shortcut, which then opens as any file.
    /// </summary>
    private async Task<bool> TryLaunchShortcutAsync(FileViewItem item)
    {
        if (item.IsDirectory || !HackDesktopEntry.IsDesktopFile(item.FileName))
        {
            return false;
        }

        HackDesktopEntry? entry = _shortcuts.TryGetValue(item.FullPath.Value, out ShortcutState? state) && state.Revision == item.Metadata.Revision
            ? state.Entry
            : await ReadShortcutAsync(item.FullPath.Value);
        if (entry is null)
        {
            return false;
        }

        _apps ??= await ReadInstalledAppsAsync();
        if (!_apps.ContainsKey(entry.AppId))
        {
            // The catalog may have changed since it was read: ask once more before refusing.
            _apps = await ReadInstalledAppsAsync();
        }

        if (!_apps.ContainsKey(entry.AppId))
        {
            await ReportErrorAsync(L["Error.AppMissing", entry.AppId]);
            return true;
        }

        try
        {
            await HostContext.Intents.LaunchAsync(entry.AppId, entry.Arguments, HostContext.CancellationToken);
        }
        catch (Exception exception) when (exception is AppGatewayAccessDeniedException or NotSupportedException)
        {
            await ReportErrorAsync(L["Error.LaunchDenied"]);
        }

        return true;
    }

    /// <summary>An app dropped from the Start menu: a shortcut to it in <paramref name="destination"/> (ADR 0160 §6).</summary>
    private async Task CreateShortcutsAsync(IReadOnlyList<string> appIds, VirtualPath destination)
    {
        if (appIds.Count == 0)
        {
            return;
        }

        _apps = await ReadInstalledAppsAsync();
        foreach (string appId in appIds)
        {
            string name = _apps.TryGetValue(appId, out HackAppInfo? app) ? app.Name : appId;
            try
            {
                await HackDesktopEntry.CreateShortcutAsync(destination.Value, appId, name, cancellationToken: HostContext.CancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                await ReportErrorAsync(L["Error.Shortcut", name, exception.Message]);
            }
        }
    }

    /// <summary>A shortcut read at one revision of its file; <see cref="Entry"/> is <see langword="null"/> for a file that is not one.</summary>
    private sealed record ShortcutState(long Revision, HackDesktopEntry? Entry);
}
