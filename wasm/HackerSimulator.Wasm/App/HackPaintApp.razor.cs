using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Apps
{
    [AppIcon("fa:paint-brush")]
    public partial class HackPaintApp : Windows.WindowBase, IAsyncDisposable
    {
        [Inject] private IJSRuntime JS { get; set; } = default!;

        private IJSObjectReference? _module;
        private ElementReference containerRef;
        private double _width = 800;
        private double _height = 600;
        private double _scale = 1;
        private bool _showGrid;
        private string _exportFormat = "png";
        private double _quality = 0.92;
        private string _color = "#000000";
        private double _size = 5;
        private bool _drawing;
        private readonly List<string> _history = new();
        private int _historyIndex = -1;
        private string CanvasId { get; } = "canvas" + Guid.NewGuid().ToString("N");
        private string ContainerId { get; } = "container" + Guid.NewGuid().ToString("N");

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "HackPaint";
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (firstRender)
            {
                _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/hackpaint.js");
                await _module.InvokeVoidAsync("init", CanvasId);
                await PushHistory();
            }
        }

        private async Task StartDraw(MouseEventArgs e)
        {
            if (_module == null) return;
            _drawing = true;
            await _module.InvokeVoidAsync("startDraw", CanvasId, e.OffsetX / _scale, e.OffsetY / _scale, _color, _size);
        }

        private async Task Draw(MouseEventArgs e)
        {
            if (_drawing && _module != null)
                await _module.InvokeVoidAsync("draw", CanvasId, e.OffsetX / _scale, e.OffsetY / _scale);
        }

        private async Task EndDraw()
        {
            if (_drawing && _module != null)
            {
                _drawing = false;
                await _module.InvokeVoidAsync("endDraw", CanvasId);
                await PushHistory();
            }
        }

        private async Task HandleMouseDown(MouseEventArgs e) => await StartDraw(e);
        private async Task HandleMouseMove(MouseEventArgs e) => await Draw(e);
        private async Task HandleMouseUp(MouseEventArgs e) => await EndDraw();

        private async Task NewDocument()
        {
            if (_module == null) return;
            var w = await JS.InvokeAsync<string?>("prompt", "Width", "800");
            var h = await JS.InvokeAsync<string?>("prompt", "Height", "600");
            if (int.TryParse(w, out var wi) && int.TryParse(h, out var he))
            {
                var color = await JS.InvokeAsync<string?>("prompt", "Background color (empty for transparent)", "#ffffff");
                await _module.InvokeVoidAsync("clear", CanvasId, wi, he, string.IsNullOrWhiteSpace(color) ? null : color);
                _width = wi;
                _height = he;
                await PushHistory();
            }
        }

        private async Task LoadImage(InputFileChangeEventArgs e)
        {
            if (_module == null) return;
            var file = e.File;
            if (file == null) return;
            using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var dataUrl = $"data:{file.ContentType};base64,{Convert.ToBase64String(ms.ToArray())}";
            await _module.InvokeVoidAsync("loadImage", CanvasId, dataUrl);
            await PushHistory();
        }


        private async Task ExportImage()
        {
            if (_module == null) return;
            var mime = _exportFormat == "jpeg" ? "image/jpeg" : "image/png";
            var url = await _module.InvokeAsync<string>("toDataUrl", CanvasId, mime, _quality);
            await _module.InvokeVoidAsync("download", url, $"image.{_exportFormat}");
        }

        private async Task Rotate90()
        {
            if (_module == null) return;
            await _module.InvokeVoidAsync("rotate90", CanvasId);
            await PushHistory();
        }

        private async Task ToggleGrid()
        {
            if (_module == null) return;
            _showGrid = !_showGrid;
            await _module.InvokeVoidAsync("toggleGrid", ContainerId);
        }

        private async Task ZoomIn()
        {
            if (_module == null) return;
            _scale *= 1.25;
            await _module.InvokeVoidAsync("setScale", CanvasId, _scale);
        }

        private async Task ZoomOut()
        {
            if (_module == null) return;
            _scale *= 0.8;
            await _module.InvokeVoidAsync("setScale", CanvasId, _scale);
        }

        private async Task PushHistory()
        {
            if (_module == null) return;
            var url = await _module.InvokeAsync<string>("toDataUrl", CanvasId, "image/png", 1.0);
            if (_historyIndex < _history.Count - 1)
                _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            _history.Add(url);
            _historyIndex = _history.Count - 1;
        }

        private async Task Undo()
        {
            if (_module == null) return;
            if (_historyIndex > 0)
            {
                _historyIndex--;
                await _module.InvokeVoidAsync("loadImage", CanvasId, _history[_historyIndex]);
            }
        }

        private async Task Redo()
        {
            if (_module == null) return;
            if (_historyIndex < _history.Count - 1)
            {
                _historyIndex++;
                await _module.InvokeVoidAsync("loadImage", CanvasId, _history[_historyIndex]);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_module != null)
                await _module.DisposeAsync();
        }
    }
}
