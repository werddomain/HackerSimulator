using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Apps
{
    public partial class FileExplorerApp : Windows.WindowBase
    {
        [Inject] private FileSystemService FS { get; set; } = default!;

        private string _path = "/";
        private List<FileSystemService.FileSystemEntry> _entries = new();

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "File Explorer";
            
            _ = Load();
        }

        private async Task Load()
        {
            _entries = new List<FileSystemService.FileSystemEntry>(await FS.ReadDirectory(_path));
        }

        private async Task Up()
        {
            if (_path == "/") return;
            var idx = _path.LastIndexOf('/');
            _path = idx <= 0 ? "/" : _path[..idx];
            await Load();
        }

        private async Task Open(FileSystemService.FileSystemEntry entry)
        {
            if (entry.IsDirectory)
            {
                _path = (_path == "/" ? string.Empty : _path) + "/" + entry.Name;
                await Load();
            }
            else
            {
                await Shell.Run("texteditorapp", new[] { (_path == "/" ? string.Empty : _path) + "/" + entry.Name });
            }
        }
    }
}
