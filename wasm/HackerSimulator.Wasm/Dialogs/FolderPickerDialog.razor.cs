using Microsoft.AspNetCore.Components;
using HackerSimulator.Wasm.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerSimulator.Wasm.Dialogs
{
    public partial class FolderPickerDialog : Dialog<string?>
    {
        [Inject] private FileSystemService FS { get; set; } = default!;

        protected string _path = "/";
        protected IEnumerable<FileSystemService.FileSystemEntry> _entries = new List<FileSystemService.FileSystemEntry>();

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            await Load();
        }

        protected async Task Load()
        {
            _entries = await FS.ReadDirectory(_path);
        }

        protected async Task Select(FileSystemService.FileSystemEntry entry)
        {
            if (entry.IsDirectory)
            {
                _path = FS.ResolvePath((_path == "/" ? string.Empty : _path) + "/" + entry.Name);
                await Load();
            }
        }

        protected void Ok() => CloseDialog(_path);
        protected void Cancel() => CloseDialog(null);
    }
}
