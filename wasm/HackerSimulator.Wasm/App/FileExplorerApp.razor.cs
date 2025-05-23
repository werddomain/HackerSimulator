using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using HackerSimulator.Wasm.Dialogs;
using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Shared;

namespace HackerSimulator.Wasm.Apps
{
    [AppIcon("fa:folder")]
    public partial class FileExplorerApp : Windows.WindowBase
    {
        [Inject] private FileSystemService FS { get; set; } = default!;
        [Inject] private FileTypeService FileTypes { get; set; } = default!;

        private string _path = "/home/user";
        private List<FileSystemService.FileSystemEntry> _entries = new();
        private HashSet<string> Selected { get; } = new();
        private List<string> _history = new();
        private int _historyIndex = -1;
        private (bool cut, List<string> paths)? _clipboard;
        private readonly Dictionary<string, ShortcutData> _shortcuts = new();

        private bool ShowHidden { get; set; }
        private FileListViewMode ViewMode { get; set; } = FileListViewMode.List;

        private bool IsBackDisabled => _historyIndex <= 0;
        private bool IsForwardDisabled => _historyIndex >= _history.Count - 1;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "File Explorer";
            _history.Add(_path);
            _historyIndex = 0;
            _ = Load();
        }

        private async Task Load()
        {
            var entries = await FS.ReadDirectory(_path);
            _entries = entries
                .Where(e => ShowHidden || !e.Name.StartsWith('.'))
                .OrderBy(e => e.Type)
                .ThenBy(e => e.Name)
                .ToList();

            _shortcuts.Clear();
            foreach (var e in _entries)
            {
                var p = EntryPath(e);
                if (!e.IsDirectory && p.EndsWith(".hlnk", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var json = await FS.ReadFile(p);
                        var sc = JsonSerializer.Deserialize<ShortcutData>(json);
                        if (sc != null) _shortcuts[p] = sc;
                    }
                    catch { }
                }
            }

            Selected.Clear();
            StateHasChanged();
        }

        private async Task Navigate(string path)
        {
            _path = FS.ResolvePath(path, _path);
            if (_historyIndex < _history.Count - 1)
                _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            if (_history.Count == 0 || _history[^1] != _path)
            {
                _history.Add(_path);
                _historyIndex = _history.Count - 1;
            }
            await Load();
        }

        private Task Up()
        {
            if (_path == "/") return Task.CompletedTask;
            var idx = _path.LastIndexOf('/');
            var parent = idx <= 0 ? "/" : _path[..idx];
            return Navigate(parent);
        }

        private Task Back()
        {
            if (IsBackDisabled) return Task.CompletedTask;
            _historyIndex--;
            return Navigate(_history[_historyIndex]);
        }

        private Task Forward()
        {
            if (IsForwardDisabled) return Task.CompletedTask;
            _historyIndex++;
            return Navigate(_history[_historyIndex]);
        }

        private Task Refresh() => Load();

        private string EntryPath(FileSystemService.FileSystemEntry e)
            => (_path == "/" ? string.Empty : _path) + "/" + e.Name;

        private string GetIcon(FileSystemService.FileSystemEntry e, string path)
        {
            path = EntryPath(e);
            if (e.IsDirectory) return "📁";
            if (_shortcuts.TryGetValue(path, out var sc) && !string.IsNullOrEmpty(sc.Icon))
                return sc.Icon!;
            if (path.EndsWith(".hlnk", StringComparison.OrdinalIgnoreCase)) return "🔗";
            return FileTypes.GetIcon(path);
        }

        private void Select(FileSystemService.FileSystemEntry entry)
        {
            var path = EntryPath(entry);
            if (Selected.Contains(path))
                Selected.Remove(path);
            else
            {
                Selected.Clear();
                Selected.Add(path);
            }
        }

        private async Task Open(FileSystemService.FileSystemEntry entry)
        {
            var path = EntryPath(entry);
            if (entry.IsDirectory)
            {
                await Navigate(path);
            }
            else if (path.EndsWith(".hlnk", StringComparison.OrdinalIgnoreCase))
            {
                if (!_shortcuts.TryGetValue(path, out var sc))
                {
                    try
                    {
                        var json = await FS.ReadFile(path);
                        sc = JsonSerializer.Deserialize<ShortcutData>(json);
                    }
                    catch { }
                }
                if (sc != null && !string.IsNullOrWhiteSpace(sc.Command))
                {
                    await Shell.Run(sc.Command, sc.Args ?? Array.Empty<string>(), this);
                }
            }
            else
            {
                await Shell.OpenFile(path);
            }
        }

        private async Task NewFile()
        {
            var dialog = new PromptDialog { Message = "File name?" };
            var name = await dialog.ShowDialog(this);
            if (string.IsNullOrWhiteSpace(name)) return;
            await FS.WriteFile((_path == "/" ? string.Empty : _path) + "/" + name, string.Empty);
            await Load();
        }

        private async Task NewFolder()
        {
            var dialog = new PromptDialog { Message = "Folder name?" };
            var name = await dialog.ShowDialog(this);
            if (string.IsNullOrWhiteSpace(name)) return;
            await FS.CreateDirectory((_path == "/" ? string.Empty : _path) + "/" + name);
            await Load();
        }

        private async Task DeleteSelection()
        {
            if (Selected.Count == 0) return;
            var dialog = new MessageBoxDialog { Message = "Delete selected?", ShowCancel = true };
            var ok = await dialog.ShowDialog(this);
            if (ok != true) return;
            foreach (var p in Selected)
                await FS.Remove(p);
            await Load();
        }

        private async Task Rename(FileSystemService.FileSystemEntry entry)
        {
            var oldPath = EntryPath(entry);
            var dialog = new PromptDialog { Message = "New name", DefaultText = entry.Name };
            var newName = await dialog.ShowDialog(this);
            if (string.IsNullOrWhiteSpace(newName) || newName == entry.Name) return;
            var newPath = (_path == "/" ? string.Empty : _path) + "/" + newName;
            await FS.Move(oldPath, newPath);
            await Load();
        }

        private void Copy() { if (Selected.Count > 0) _clipboard = (false, Selected.ToList()); }
        private void Cut() { if (Selected.Count > 0) _clipboard = (true, Selected.ToList()); }

        private async Task Paste()
        {
            if (_clipboard == null) return;
            foreach (var p in _clipboard.Value.paths)
            {
                var name = p.Split('/').Last();
                var dest = (_path == "/" ? string.Empty : _path) + "/" + name;
                if (_clipboard.Value.cut)
                    await FS.Move(p, dest);
                else
                    await FS.Copy(p, dest);
            }
            if (_clipboard.Value.cut) _clipboard = null;
            await Load();
        }

        private async Task ToggleHidden()
        {
            ShowHidden = !ShowHidden;
            await Load();
        }

        private Task SetView(FileListViewMode mode)
        {
            ViewMode = mode;
            return Task.CompletedTask;
        }

        private async Task OnPathKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Enter")
                await Navigate(_path);
        }

        private bool _showMenu;
        private double _menuX;
        private double _menuY;
        private FileSystemService.FileSystemEntry? _menuEntry;

        private void ShowContextMenu(MouseEventArgs e, FileSystemService.FileSystemEntry? entry)
        {
            _menuEntry = entry;
            _menuX = e.ClientX;
            _menuY = e.ClientY;
            _showMenu = true;
        }

        private void ShowBackgroundMenu(MouseEventArgs e) => ShowContextMenu(e, null);
        private void HideMenu() => _showMenu = false;

        private Task OnListContextMenu((MouseEventArgs e, FileSystemService.FileSystemEntry? entry) data)
        {
            ShowContextMenu(data.e, data.entry);
            return Task.CompletedTask;
        }

        private async Task ContextAction(string action)
        {
            switch (action)
            {
                case "open" when _menuEntry != null:
                    await Open(_menuEntry);
                    break;
                case "rename" when _menuEntry != null:
                    await Rename(_menuEntry);
                    break;
                case "delete":
                    await DeleteSelection();
                    break;
                case "new-file":
                    await NewFile();
                    break;
                case "new-folder":
                    await NewFolder();
                    break;
                case "copy":
                    Copy();
                    break;
                case "cut":
                    Cut();
                    break;
                case "paste":
                    await Paste();
                    break;
            }
            _showMenu = false;
        }

        private record ShortcutData(string Command, string[]? Args, string? Icon);
    }
}
