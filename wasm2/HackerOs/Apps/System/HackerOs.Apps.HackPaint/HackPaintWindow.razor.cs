using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Blazor;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace HackerOs.Apps.HackPaint;

/// <summary>
/// Hack Paint's window: an RGBA canvas editor that opens and saves real image files of the OS
/// (ADR 0103 §1). Files are read and written in C# through the app's gateway (<see cref="PaintFileStore"/>);
/// the collocated JS module only decodes and encodes pixels with the browser's codecs.
/// </summary>
public partial class HackPaintWindow : WindowAppBase, IAsyncDisposable, IWindowCloseGuard
{
    private const string AppName = "Hack Paint";
    private static readonly FileDialogFilter ImageFilter = new(
        "Images", [.. ImageFileFormat.OpenExtensions], ["image/png", "image/jpeg", "image/bmp", "image/gif", "image/webp"]);

    private readonly PaintCanvasState _state = new();
    private ElementReference _root;
    private ElementReference _canvas;
    private IJSObjectReference? _module;
    private bool _drawing;
    private int _lastX;
    private int _lastY;
    private (int X, int Y)? _cropStart;
    private bool _panning;
    private bool _launchFileHandled;

    // Current document file. The gateway carries the dialog handle when the file lies outside the
    // app's home-folder capability, so later saves to the same path stay authorized.
    private VirtualPath? _path;
    private IAppFileSystemGateway? _fileSystem;
    private string? _error;
    private string? _busyMessage;
    private int _jpegQuality = 92;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    /// <summary>Gets the window title: <c>name.png — Hack Paint</c>, prefixed with <c>*</c> while unsaved.</summary>
    internal string WindowTitle => PaintTitle.Format(_path?.Value, _state.IsDirty);

    private bool IsBusy => _busyMessage is not null;

    /// <summary>Gets or sets the JPEG quality, in percent, used when saving a <c>.jpg</c> file.</summary>
    private int JpegQuality
    {
        get => _jpegQuality;
        set => _jpegQuality = Math.Clamp(value, 10, 100);
    }

    /// <inheritdoc />
    protected override async Task OnAppAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/HackerOs.Apps.HackPaint/HackPaintWindow.razor.js");
            await _module.InvokeVoidAsync("guardShortcuts", _root);
        }

        if (!_launchFileHandled)
        {
            // "Open with", a double-click in the File Explorer or `hack-paint <file>` passes the path
            // as the first argument (ADR 0050); decoding needs the module, hence after the first render.
            _launchFileHandled = true;
            if (LaunchFile is { } launchFile)
            {
                await LoadAsync(launchFile, AppContext.FileSystem);
                StateHasChanged();
                return;
            }
        }

        await RenderCanvasAsync();
    }

    /// <summary>Asks before closing a window whose image is not saved (ADR 0006).</summary>
    /// <param name="cancellationToken">Cancels the close request.</param>
    public async ValueTask<bool> ConfirmCloseAsync(CancellationToken cancellationToken = default)
    {
        if (!_state.IsDirty)
        {
            return true;
        }

        MessageBoxDialogResult answer = await MessageBox(
            "Unsaved image",
            $"Close {PaintTitle.FileName(_path?.Value)} and discard the changes?",
            MessageBoxType.YesNo,
            cancellationToken);
        return answer.Result == MessageBoxResult.Yes;
    }

    // ──────────────────────────── File commands ────────────────────────────

    private async Task NewDocumentAsync()
    {
        if (!await ConfirmDiscardAsync())
        {
            return;
        }

        _state.NewDocument(800, 600);
        _path = null;
        _fileSystem = null;
        _error = null;
        await RenderCanvasAsync();
    }

    private async Task OpenAsync()
    {
        if (!await ConfirmDiscardAsync())
        {
            return;
        }

        OpenFileDialogResult result = await OpenFileAsync(new OpenFileDialogRequest
        {
            Title = "Open Image",
            InitialDirectory = VirtualPath.Parse(CurrentDirectory()),
            Filters = [ImageFilter, new FileDialogFilter("All files", [], [])],
            RequestedAccess = SelectedFileAccess.ReadWrite
        });
        if (result.Status == FileDialogStatus.Selected && result.Resources.Count > 0)
        {
            SelectedFileResource selected = result.Resources[0];
            await LoadAsync(selected.Path, AppContext.FileSystem.WithSelectedHandle(selected.Handle));
        }
    }

    private async Task SaveAsync()
    {
        if (_path is { } path && _fileSystem is not null)
        {
            await WriteAsync(path, _fileSystem);
        }
        else
        {
            await SaveAsAsync();
        }
    }

    private async Task SaveAsAsync()
    {
        SaveFileDialogResult result = await SaveFileAsync(new SaveFileDialogRequest
        {
            Title = "Save Image",
            InitialDirectory = VirtualPath.Parse(CurrentDirectory()),
            SuggestedFileName = _path is { } path ? PaintTitle.FileName(path.Value) : "untitled.png",
            DefaultExtension = ".png",
            Filters =
            [
                new FileDialogFilter("PNG image", [".png"], ["image/png"]),
                new FileDialogFilter("JPEG image", [".jpg", ".jpeg"], ["image/jpeg"])
            ],
            ConfirmOverwrite = true
        });
        if (result.Status == FileDialogStatus.Selected && result.Resource is not null)
        {
            await WriteAsync(result.Resource.Path, AppContext.FileSystem.WithSelectedHandle(result.Resource.Handle));
        }
    }

    /// <summary>Reads, checks and decodes an image, then makes it the document.</summary>
    private async Task LoadAsync(VirtualPath path, IAppFileSystemGateway fileSystem)
    {
        _error = null;
        _busyMessage = $"Opening {PaintTitle.FileName(path.Value)}…";
        StateHasChanged();
        try
        {
            PaintFileReadResult read = await PaintFileStore.ReadAsync(fileSystem, path, AppContext.CancellationToken);
            if (read.Error is not null || read.Bytes is null || read.Header is null)
            {
                _error = $"Cannot open {path.Value}: {read.Error}";
                return;
            }

            DecodedImage decoded = await _module!.InvokeAsync<DecodedImage>(
                "decodeImage", AppContext.CancellationToken, read.Bytes, read.Header.MediaType, ImageFileFormat.MaxDimension);
            if (decoded.Error is not null || decoded.Pixels is null)
            {
                _error = $"Cannot open {path.Value}: {decoded.Error}";
                return;
            }

            _state.ReplaceDocument(decoded.Width, decoded.Height, decoded.Pixels);
            _path = path;
            _fileSystem = fileSystem;
            await RenderCanvasAsync();
        }
        catch (Exception exception) when (exception is JSException or ArgumentException)
        {
            _error = $"Cannot open {path.Value}: {exception.Message}";
        }
        finally
        {
            _busyMessage = null;
        }
    }

    /// <summary>Encodes the image for the target's extension and writes it.</summary>
    private async Task WriteAsync(VirtualPath path, IAppFileSystemGateway fileSystem)
    {
        _error = null;
        ImageFileKind kind = ImageFileFormat.SaveKindFor(path.Value);
        string mediaType = ImageFileFormat.MediaTypeOf(kind);
        _busyMessage = $"Saving {PaintTitle.FileName(path.Value)}…";
        StateHasChanged();
        try
        {
            byte[] encoded = await _module!.InvokeAsync<byte[]>(
                "encodeImage", AppContext.CancellationToken, _state.Width, _state.Height, _state.GetCurrentBuffer(), mediaType, JpegQuality / 100.0);
            string? failure = await PaintFileStore.WriteAsync(fileSystem, path, encoded, mediaType, AppContext.CancellationToken);
            if (failure is not null)
            {
                _error = $"Cannot save {path.Value}: {failure}";
                return;
            }

            _path = path;
            _fileSystem = fileSystem;
            _state.MarkSaved();
        }
        catch (JSException exception)
        {
            _error = $"Cannot save {path.Value}: {exception.Message}";
        }
        finally
        {
            _busyMessage = null;
        }
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        if (!_state.IsDirty)
        {
            return true;
        }

        MessageBoxDialogResult answer = await MessageBox(
            "Unsaved image",
            $"Discard the changes to {PaintTitle.FileName(_path?.Value)}?",
            MessageBoxType.YesNo);
        return answer.Result == MessageBoxResult.Yes;
    }

    private string CurrentDirectory() =>
        _path is { } path && path.Value.LastIndexOf('/') is > 0 and var slash
            ? path.Value[..slash]
            : HackUser.Describe(AppContext).HomeDirectory;

    private void DismissError() => _error = null;

    private async Task HandleKeyDownAsync(KeyboardEventArgs args)
    {
        if (!(args.CtrlKey || args.MetaKey) || IsBusy)
        {
            return;
        }

        switch (args.Key.ToLowerInvariant())
        {
            case "s" when args.ShiftKey: await SaveAsAsync(); break;
            case "s": await SaveAsync(); break;
            case "o": await OpenAsync(); break;
            case "n": await NewDocumentAsync(); break;
            case "z": await UndoAsync(); break;
            case "y": await RedoAsync(); break;
        }
    }

    // ──────────────────────────── Drawing ────────────────────────────

    private async Task StartDrawAsync(PointerEventArgs args)
    {
        if (_module is null)
        {
            return;
        }

        _lastX = (int)args.OffsetX;
        _lastY = (int)args.OffsetY;
        await _module.InvokeVoidAsync("capturePointer", _canvas, args.PointerId);
        if (_state.PanMode)
        {
            _panning = true;
            return;
        }

        if (_state.CropMode)
        {
            _cropStart = (_lastX, _lastY);
            return;
        }

        _drawing = true;
        _state.DrawLine(_lastX, _lastY, _lastX, _lastY);
        await RenderCanvasAsync();
    }

    private async Task DrawAsync(PointerEventArgs args)
    {
        int x = (int)args.OffsetX;
        int y = (int)args.OffsetY;
        if (_panning)
        {
            _state.PanBy(x - _lastX, y - _lastY);
            (_lastX, _lastY) = (x, y);
            await ApplyViewportAsync();
            return;
        }

        if (!_drawing)
        {
            return;
        }

        _state.DrawLine(_lastX, _lastY, x, y);
        (_lastX, _lastY) = (x, y);
        await RenderCanvasAsync();
    }

    private async Task StopDrawAsync(PointerEventArgs args)
    {
        _drawing = false;
        _panning = false;
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("releasePointer", _canvas, args.PointerId);
        }

        if (_cropStart is not { } start)
        {
            return;
        }

        _cropStart = null;
        int endX = (int)args.OffsetX;
        int endY = (int)args.OffsetY;
        _state.Crop(Math.Min(start.X, endX), Math.Min(start.Y, endY), Math.Abs(endX - start.X) + 1, Math.Abs(endY - start.Y) + 1);
        await RenderCanvasAsync();
    }

    private async Task UndoAsync() { _state.Undo(); await RenderCanvasAsync(); }

    private async Task RedoAsync() { _state.Redo(); await RenderCanvasAsync(); }

    private async Task RotateAsync() { _state.Rotate90(); await RenderCanvasAsync(); }

    private async Task ToggleGridAsync() { _state.ToggleGrid(); await InvokeAsync(StateHasChanged); }

    private void ToggleCrop() => _state.ToggleCropMode();

    private void TogglePan() => _state.TogglePanMode();

    private async Task ZoomIn() { _state.ZoomIn(); await ApplyViewportAsync(); }

    private async Task ZoomOut() { _state.ZoomOut(); await ApplyViewportAsync(); }

    private async ValueTask RenderCanvasAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("renderRgba", _canvas, _state.Width, _state.Height, _state.GetCurrentBuffer());
        }
    }

    private async ValueTask ApplyViewportAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("setViewport", _canvas, _state.PanX, _state.PanY, _state.Scale);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is gone.
            }
        }
    }

    /// <summary>Pixels decoded by the module, or why decoding failed.</summary>
    private sealed record DecodedImage(int Width, int Height, byte[]? Pixels, string? Error);
}

/// <summary>Formats Hack Paint's window title from the document path and its saved state.</summary>
internal static class PaintTitle
{
    /// <summary>Gets the file name of a path, or <c>Untitled</c>.</summary>
    /// <param name="path">Document path or <see langword="null"/>.</param>
    public static string FileName(string? path) =>
        string.IsNullOrEmpty(path) ? "Untitled" : path[(path.LastIndexOf('/') + 1)..];

    /// <summary>Builds <c>[*]name — Hack Paint</c>.</summary>
    /// <param name="path">Document path or <see langword="null"/>.</param>
    /// <param name="dirty">Whether the image has unsaved changes.</param>
    public static string Format(string? path, bool dirty) => $"{(dirty ? "*" : string.Empty)}{FileName(path)} — Hack Paint";
}
