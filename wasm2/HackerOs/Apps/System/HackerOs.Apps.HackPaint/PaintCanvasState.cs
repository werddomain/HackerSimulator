namespace HackerOs.Apps.HackPaint;

/// <summary>
/// Headlessly testable state engine for Hack Paint.
/// Manages canvas dimensions, brush settings, zoom scale, grid toggle,
/// crop selection bounds, rotation, and an undo/redo stack of pixel buffers.
/// </summary>
public sealed class PaintCanvasState
{
    private const int MaxHistory = 20;

    private sealed record CanvasSnapshot(int Width, int Height, byte[] Buffer);

    public int Width { get; private set; } = 800;
    public int Height { get; private set; } = 600;
    public string ActiveColor { get; set; } = "#33ff33";
    public int BrushSize { get; set; } = 5;
    public double Scale { get; private set; } = 1.0;
    public bool ShowGrid { get; private set; }
    public bool PanMode { get; private set; }
    public bool CropMode { get; private set; }

    // Undo / Redo history stack of canvas snapshots
    private readonly List<CanvasSnapshot> _history = [];
    private int _historyIndex = -1;

    public bool CanUndo => _historyIndex > 0;
    public bool CanRedo => _historyIndex < _history.Count - 1;

    public PaintCanvasState()
    {
        // Push initial blank white canvas
        PushHistory(800, 600, CreateBlankBuffer(800, 600, 255, 255, 255, 255));
    }

    public void NewDocument(int width, int height, byte r = 255, byte g = 255, byte b = 255, byte a = 255)
    {
        int w = Math.Clamp(width, 16, 4096);
        int h = Math.Clamp(height, 16, 4096);
        Scale = 1.0;
        PushHistory(w, h, CreateBlankBuffer(w, h, r, g, b, a));
    }

    public void PushHistory(byte[] pixelRgba) => PushHistory(Width, Height, pixelRgba);

    public void PushHistory(int width, int height, byte[] pixelRgba)
    {
        // Truncate redo history
        if (_historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);

        _history.Add(new CanvasSnapshot(width, height, pixelRgba));
        if (_history.Count > MaxHistory)
            _history.RemoveAt(0);

        _historyIndex = _history.Count - 1;
        Width = width;
        Height = height;
    }

    public byte[]? GetCurrentBuffer() =>
        _historyIndex >= 0 && _historyIndex < _history.Count ? _history[_historyIndex].Buffer : null;

    public void Undo()
    {
        if (CanUndo)
        {
            _historyIndex--;
            ApplyCurrentSnapshot();
        }
    }

    public void Redo()
    {
        if (CanRedo)
        {
            _historyIndex++;
            ApplyCurrentSnapshot();
        }
    }

    private void ApplyCurrentSnapshot()
    {
        if (_historyIndex >= 0 && _historyIndex < _history.Count)
        {
            Width = _history[_historyIndex].Width;
            Height = _history[_historyIndex].Height;
        }
    }

    public void ZoomIn() => Scale = Math.Min(5.0, Math.Round(Scale * 1.25, 2));
    public void ZoomOut() => Scale = Math.Max(0.2, Math.Round(Scale * 0.8, 2));
    public void ToggleGrid() => ShowGrid = !ShowGrid;
    public void TogglePanMode() => PanMode = !PanMode;
    public void ToggleCropMode() => CropMode = !CropMode;

    public void Rotate90()
    {
        var current = GetCurrentBuffer();
        if (current is null) return;

        int oldW = Width;
        int oldH = Height;
        int newW = oldH;
        int newH = oldW;

        byte[] rotated = new byte[newW * newH * 4];

        for (int y = 0; y < oldH; y++)
        {
            for (int x = 0; x < oldW; x++)
            {
                int srcIdx = (y * oldW + x) * 4;
                int dstX = oldH - 1 - y;
                int dstY = x;
                int dstIdx = (dstY * newW + dstX) * 4;

                rotated[dstIdx]     = current[srcIdx];
                rotated[dstIdx + 1] = current[srcIdx + 1];
                rotated[dstIdx + 2] = current[srcIdx + 2];
                rotated[dstIdx + 3] = current[srcIdx + 3];
            }
        }

        PushHistory(newW, newH, rotated);
    }

    public static byte[] CreateBlankBuffer(int w, int h, byte r, byte g, byte b, byte a)
    {
        byte[] buf = new byte[w * h * 4];
        for (int i = 0; i < buf.Length; i += 4)
        {
            buf[i]     = r;
            buf[i + 1] = g;
            buf[i + 2] = b;
            buf[i + 3] = a;
        }
        return buf;
    }
}
