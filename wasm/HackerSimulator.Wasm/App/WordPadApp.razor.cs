using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Apps
{
    [OpenFileType("rtf", "docx", "txt", "html")]
    [AppIcon("fa:file-word")]
    public partial class WordPadApp : Windows.WindowBase, IAsyncDisposable
    {
        [Inject] private FileSystemService FS { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        private IJSObjectReference? _module;
        private ElementReference _editorRef;
        private string _path = string.Empty;
        private string EditorId { get; } = "editor" + Guid.NewGuid().ToString("N");

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "WordPad";
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (firstRender)
            {
                _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/wordpad.js");
            }
        }

        private async Task ExecCmd(string cmd, string? value = null)
        {
            if (_module != null)
                await _module.InvokeVoidAsync("exec", EditorId, cmd, value);
        }

        private Task Bold() => ExecCmd("bold");
        private Task Italic() => ExecCmd("italic");
        private Task Underline() => ExecCmd("underline");
        private Task Bullet() => ExecCmd("insertUnorderedList");

        private async Task ChangeColor(ChangeEventArgs e)
        {
            if (e.Value is string color)
            {
                await ExecCmd("foreColor", color);
            }
        }

        private async Task Save()
        {
            if (_module == null || string.IsNullOrWhiteSpace(_path)) return;
            var path = FS.ResolvePath(_path);
            var html = await _module.InvokeAsync<string>("getHtml", EditorId);
            await FS.WriteFile(path, html);
        }

        private async Task Open()
        {
            if (_module == null || string.IsNullOrWhiteSpace(_path)) return;
            var path = FS.ResolvePath(_path);
            if (!await FS.Exists(path)) return;
            var html = await FS.ReadFile(path);
            await _module.InvokeVoidAsync("setHtml", EditorId, html);
        }

        private async Task NewDoc()
        {
            if (_module != null)
                await _module.InvokeVoidAsync("setHtml", EditorId, "");
        }

        public async ValueTask DisposeAsync()
        {
            if (_module != null)
                await _module.DisposeAsync();
        }
    }
}
