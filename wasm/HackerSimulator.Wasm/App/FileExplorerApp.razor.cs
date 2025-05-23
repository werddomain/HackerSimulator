using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Apps
{
    public partial class FileExplorerApp : Windows.WindowBase
    {
        [Inject] private FileSystemService FS { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        private string _path = "/home/user";
        private List<FileSystemService.FileSystemEntry> _entries = new();
        private HashSet<string> Selected { get; } = new();
        private List<string> _history = new();
        private int _historyIndex = -1;
        private (bool cut, List<string> paths)? _clipboard;

        private bool ShowHidden { get; set; }
        private ViewModes ViewMode { get; set; } = ViewModes.List;

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
            if (entry.IsDirectory)
                await Navigate(EntryPath(entry));
            else
                await Shell.Run("texteditorapp", new[] { EntryPath(entry) });
        }

        private async Task NewFile()
        {
            var name = await JS.InvokeAsync<string?>("prompt", "File name?");
            if (string.IsNullOrWhiteSpace(name)) return;
            await FS.WriteFile((_path == "/" ? string.Empty : _path) + "/" + name, string.Empty);
            await Load();
        }

        private async Task NewFolder()
        {
            var name = await JS.InvokeAsync<string?>("prompt", "Folder name?");
            if (string.IsNullOrWhiteSpace(name)) return;
            await FS.CreateDirectory((_path == "/" ? string.Empty : _path) + "/" + name);
            await Load();
        }

        private async Task DeleteSelection()
        {
            if (Selected.Count == 0) return;
            var ok = await JS.InvokeAsync<bool>("confirm", "Delete selected?");
            if (!ok) return;
            foreach (var p in Selected)
                await FS.Remove(p);
            await Load();
        }

        private async Task Rename(FileSystemService.FileSystemEntry entry)
        {
            var oldPath = EntryPath(entry);
            var newName = await JS.InvokeAsync<string?>("prompt", "New name", entry.Name);
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

        private Task SetView(ViewModes mode)
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

        private enum ViewModes { List, Grid }
    }
}
