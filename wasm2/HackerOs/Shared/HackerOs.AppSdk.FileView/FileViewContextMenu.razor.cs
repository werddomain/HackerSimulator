using HackerOs.AppSdk.FileView.ContextMenu;
using HackerOs.AppSdk.FileView.Events;
using HackerOs.AppSdk.FileView.Icons;
using HackerOs.AppSdk.Icons;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace HackerOs.AppSdk.FileView;

/// <summary>
/// Builds and opens one context menu: default well-known items for the given scope, then every matching
/// <see cref="IFileViewContextMenuProvider"/> in <see cref="FileView.ContextMenuProviders"/> order, then
/// <see cref="FileView.ContextMenuOpening"/> as the final host-level adjustment point. See
/// <c>docs/Global-FileView-And-MessagingSystem/FileViewControl.md#context-menu-customization</c>.
/// </summary>
public partial class FileViewContextMenu
{
    private MudMenu? _menu;
    private FileViewMenuItemCollection? _currentItems;

    /// <summary>The owning <see cref="FileView"/> whose state this menu acts on.</summary>
    [Parameter, EditorRequired]
    public FileView Owner { get; set; } = null!;

    internal async Task OpenAsync(MouseEventArgs args, FileViewContextMenuScope scope, FileViewItem? item)
    {
        FileViewMenuItemCollection items = new();
        AddDefaultItems(items, scope, item);

        FileViewContextMenuContext context = new(scope, item);
        foreach (IFileViewContextMenuProvider provider in Owner.ContextMenuProviders)
        {
            if (Applies(provider, scope, item))
            {
                provider.Customize(context, items);
            }
        }

        Owner.RaiseContextMenuOpening(new FileViewContextMenuOpeningEventArgs(scope, item, items));

        _currentItems = items;
        StateHasChanged();
        if (_menu is not null)
        {
            await _menu.OpenMenuAsync(args);
        }
    }

    private static bool Applies(IFileViewContextMenuProvider provider, FileViewContextMenuScope scope, FileViewItem? item) =>
        provider.Scope switch
        {
            FileViewContextMenuScope.Background => scope == FileViewContextMenuScope.Background,
            FileViewContextMenuScope.Directory => scope == FileViewContextMenuScope.Directory,
            FileViewContextMenuScope.File => scope == FileViewContextMenuScope.File,
            FileViewContextMenuScope.FileType =>
                scope is FileViewContextMenuScope.File or FileViewContextMenuScope.Directory
                && item is not null
                && provider.Matches(item),
            _ => false
        };

    private void AddDefaultItems(FileViewMenuItemCollection items, FileViewContextMenuScope scope, FileViewItem? item)
    {
        switch (scope)
        {
            case FileViewContextMenuScope.Background:
                items.Add(new FileViewMenuItem(
                    "new-folder", "New Folder", ShellIconDescriptor.Vector(IconLibrary.Lucide, "folder-plus"),
                    Owner.CreateFolderInteractiveAsync));
                items.Add(new FileViewMenuItem(
                    "refresh", "Refresh", ShellIconDescriptor.Vector(IconLibrary.Lucide, "refresh-cw"),
                    () => Owner.RefreshAsync()));
                break;

            case FileViewContextMenuScope.Directory:
            case FileViewContextMenuScope.File:
                if (item is not null)
                {
                    items.Add(new FileViewMenuItem(
                        "open", "Open", ShellIconDescriptor.Vector(IconLibrary.Lucide, "folder-open"),
                        () => Owner.ActivateItemAsync(item)));
                    items.Add(new FileViewMenuItem(
                        "rename", "Rename", ShellIconDescriptor.Vector(IconLibrary.Lucide, "pencil"),
                        () =>
                        {
                            item.Select();
                            item.Rename();
                            return Task.CompletedTask;
                        }));
                    items.Add(new FileViewMenuItem(
                        "delete", "Delete", ShellIconDescriptor.Vector(IconLibrary.Lucide, "trash-2"),
                        () =>
                        {
                            item.Select();
                            return Owner.DeleteSelectedAsync();
                        }));
                }

                break;
        }
    }
}
