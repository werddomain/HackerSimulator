using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using HackerSimulator.Wasm.Dialogs;
using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Shared;

namespace HackerSimulator.Wasm.Apps
{
    [AppIcon("fa:folder")]
    [OpenFileType("zip")]
    public partial class FileExplorerApp : Windows.WindowBase
    {
        [Inject] private FileSystemService FS { get; set; } = default!;
        [Inject] private FileTypeService FileTypes { get; set; } = default!;
        [Inject] private FileOpsService FileOps { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        [Inject] private ApplicationService Apps { get; set; } = default!;
        private string _path = "/home/user";
        private List<FileSystemService.FileSystemEntry> _entries = new();
        private HashSet<string> Selected { get; } = new();
        private List<string> _history = new();
        private int _historyIndex = -1;
        private (bool cut, List<string> paths)? _clipboard;
        private readonly Dictionary<string, ShortcutData> _shortcuts = new();
        private InputFile? _fileInput;
        private readonly string _fileInputId = "fileInput" + Guid.NewGuid().ToString("N");

        // zip viewing state
        private bool _zipMode;
        private string _zipFile = string.Empty;
        private string _zipPath = string.Empty;
        private string _pathBeforeZip = string.Empty;

        private List<ApplicationService.AppInfo> _openWith = new();
        private bool ShowHidden { get; set; }
        private FileListViewMode ViewMode { get; set; } = FileListViewMode.List;

        private bool IsBackDisabled => _zipMode || _historyIndex <= 0;
        private bool IsForwardDisabled => _zipMode || _historyIndex >= _history.Count - 1;

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
            if (_zipMode)
            {
                await LoadZip();
                return;
            }

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

        private async Task LoadZip()
        {
            _entries.Clear();
            var bytes = await FS.ReadBinaryFile(_zipFile);
            using var ms = new System.IO.MemoryStream(bytes);
            using var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
            var prefix = string.IsNullOrEmpty(_zipPath) ? string.Empty : _zipPath.TrimEnd('/') + "/";
            var names = new HashSet<string>();
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith(prefix)) continue;
                var rel = entry.FullName.Substring(prefix.Length);
                if (string.IsNullOrEmpty(rel)) continue;
                var parts = rel.Split('/', 2);
                var name = parts[0];
                if (!names.Add(name)) continue;
                bool dir = parts.Length > 1 || entry.FullName.EndsWith("/");
                _entries.Add(new FileSystemService.FileSystemEntry { Name = name, Type = dir ? "directory" : "file" });
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

        private async Task EnterZip(string path)
        {
            _zipMode = true;
            _zipFile = path;
            _zipPath = string.Empty;
            _pathBeforeZip = _path;
            await Load();
        }

        private Task Up()
        {
            if (_zipMode)
            {
                if (string.IsNullOrEmpty(_zipPath))
                {
                    _zipMode = false;
                    _path = _pathBeforeZip;
                    return Load();
                }
                var idxZip = _zipPath.LastIndexOf('/');
                _zipPath = idxZip <= 0 ? string.Empty : _zipPath[..idxZip];
                return Load();
            }

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
        {
            if (_zipMode)
                return (_zipPath == string.Empty ? string.Empty : _zipPath + "/") + e.Name;
            return (_path == "/" ? string.Empty : _path) + "/" + e.Name;
        }

        private string GetIcon(FileSystemService.FileSystemEntry e, string path)
        {

            //path = EntryPath(e);

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
            if (_zipMode)
            {
                if (entry.IsDirectory)
                {
                    _zipPath = string.IsNullOrEmpty(_zipPath) ? entry.Name : $"{_zipPath}/{entry.Name}";
                    await Load();
                }
                return;
            }

            var path = EntryPath(entry);
            if (entry.IsDirectory)
            {
                await Navigate(path);
            }
            else if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                await EnterZip(path);
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

        private async Task ZipDirectory(FileSystemService.FileSystemEntry entry)
        {
            var path = EntryPath(entry);
            var bytes = await FS.ZipEntry(path);
            var dest = path + ".zip";
            await FS.WriteBinaryFile(dest, bytes);
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
            if (_zipMode)
                return;
            _menuEntry = entry;
            _openWith = entry == null ? new List<ApplicationService.AppInfo>() : Apps.GetAppsForFile(EntryPath(entry)).ToList();
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
                case "zip-folder" when _menuEntry != null && _menuEntry.IsDirectory:
                    await ZipDirectory(_menuEntry);
                    break;
            }
            _showMenu = false;
        }


        private async Task TriggerUpload()
        {
            await JS.InvokeVoidAsync("eval", $"document.getElementById('{_fileInputId}').click()");
        }

        private async Task HandleFileSelected(InputFileChangeEventArgs e)
        {
            foreach (var file in e.GetMultipleFiles())
            {
                using var stream = file.OpenReadStream(long.MaxValue);
                using var ms = new System.IO.MemoryStream();
                await stream.CopyToAsync(ms);
                var bytes = ms.ToArray();
                var path = (_path == "/" ? string.Empty : _path) + "/" + file.Name;
                await FS.WriteBinaryFile(path, bytes);
            }
            await Load();
        }

        private async Task DownloadSelection()
        {
            if (Selected.Count == 0) return;
            if (Selected.Count == 1)
            {
                var path = Selected.First();
                var stat = await FS.Stat(path);
                if (stat.IsDirectory)
                {
                    var bytes = await FS.ZipEntry(path);
                    var name = path.Split('/').Last() + ".zip";
                    await FileOps.SaveFile(name, bytes);
                }
                else
                {
                    var bytes = await FS.ReadFileBytes(path);
                    var name = path.Split('/').Last();
                    await FileOps.SaveFile(name, bytes);
                }
            }
            else
            {
                var bytes = await FS.ZipEntries(Selected);
                await FileOps.SaveFile("download.zip", bytes);
            }
        }


        private async Task OpenWith(string command)
        {
            if (_menuEntry == null) return;
            var path = EntryPath(_menuEntry);
            await Shell.Run(command, new[] { path }, this);
            _showMenu = false;
        }

        private record ShortcutData(string Command, string[]? Args, string? Icon);
    }
}
