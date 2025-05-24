using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Apps
{
    [OpenFileType("txt", "md")]
    [AppIcon("fa:file-lines")]
    public partial class NotepadApp : Windows.WindowBase
    {
        [Inject] private FileSystemService FS { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        private IJSObjectReference? _module;
        private ElementReference _editorRef;
        private string _path = string.Empty;
        private string _content = string.Empty;
        private bool _wrap = true;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Notepad";
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (firstRender)
                _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/notepad.js");
        }

        private Task NewFile()
        {
            _path = string.Empty;
            _content = string.Empty;
            return Task.CompletedTask;
        }

        private async Task OpenFile()
        {
            var dialog = new Dialogs.FilePickerDialog();
            var result = await dialog.ShowDialog(this);
            if (!string.IsNullOrWhiteSpace(result))
            {
                _path = result;
                _content = await FS.ReadFile(result);
            }
        }

        private async Task Save()
        {
            if (string.IsNullOrWhiteSpace(_path))
            {
                await SaveAs();
                return;
            }
            await FS.WriteFile(_path, _content);
        }

        private async Task SaveAs()
        {
            var dialog = new Dialogs.FilePickerDialog();
            var result = await dialog.ShowDialog(this);
            if (!string.IsNullOrWhiteSpace(result))
            {
                _path = result;
                await FS.WriteFile(_path, _content);
            }
        }

        private async Task Exec(string cmd)
        {
            if (_module != null)
                await _module.InvokeVoidAsync("exec", _editorRef, cmd);
        }

        private Task Undo() => Exec("undo");
        private Task Cut() => Exec("cut");
        private Task Copy() => Exec("copy");
        private Task Paste() => Exec("paste");
        private Task SelectAll() => Exec("selectAll");

        private void ToggleWrap() => _wrap = !_wrap;
    }
}
