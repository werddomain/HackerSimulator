namespace HackerOs.Apps.HackPaint;

/// <summary>
/// Owns the complete editable Hack Paint document as RGBA pixels. Every visual
/// operation commits a copied pixel snapshot, so undo and redo always restore
/// the same content that the renderer displays.
/// </summary>
public sealed class PaintCanvasState
{
    private const int MaxHistory = 20;
    private sealed record CanvasSnapshot(int Width, int Height, byte[] Buffer);
    private readonly List<CanvasSnapshot> _history = [];
    private int _historyIndex = -1;

    public PaintCanvasState() => NewDocument(800, 600);

    public int Width { get; private set; }
    public int Height { get; private set; }
    public string ActiveColor { get; set; } = "#33ff33";
    public int BrushSize { get; set; } = 5;
    public double Scale { get; private set; } = 1.0;
    public bool ShowGrid { get; private set; }
    public bool PanMode { get; private set; }
    public bool CropMode { get; private set; }
    public int PanX { get; private set; }
    public int PanY { get; private set; }
    public bool CanUndo => _historyIndex > 0;
    public bool CanRedo => _historyIndex >= 0 && _historyIndex < _history.Count - 1;

    /// <summary>Creates a new bounded image and resets history to that image.</summary>
    public void NewDocument(int width, int height, byte r = 255, byte g = 255, byte b = 255, byte a = 255)
    {
        Width = Math.Clamp(width, 16, 4096);
        Height = Math.Clamp(height, 16, 4096);
        Scale = 1.0;
        PanX = 0;
        PanY = 0;
        _history.Clear();
        _historyIndex = -1;
        Commit(Width, Height, CreateBlankBuffer(Width, Height, r, g, b, a));
    }

    /// <summary>Imports an already-decoded RGBA image after validating its size.</summary>
    public void ReplaceDocument(int width, int height, byte[] pixelRgba)
    {
        ValidateBuffer(width, height, pixelRgba);
        _history.Clear();
        _historyIndex = -1;
        Commit(width, height, pixelRgba);
    }

    /// <summary>Returns a defensive copy so callers cannot mutate history.</summary>
    public byte[] GetCurrentBuffer() => _history[_historyIndex].Buffer.ToArray();

    public void PushHistory(byte[] pixelRgba) => Commit(Width, Height, pixelRgba);
    public void PushHistory(int width, int height, byte[] pixelRgba) => Commit(width, height, pixelRgba);

    /// <summary>Draws a round, opaque RGBA brush stroke into the authoritative document.</summary>
    public void DrawLine(int startX, int startY, int endX, int endY, string? color = null, int? size = null)
    {
        var pixels = GetCurrentBuffer();
        var (r, g, b, a) = ParseColor(color ?? ActiveColor);
        var radius = Math.Max(0, (size ?? BrushSize - 1) / 2);
        var dx = Math.Abs(endX - startX);
        var sx = startX < endX ? 1 : -1;
        var dy = -Math.Abs(endY - startY);
        var sy = startY < endY ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            PaintCircle(pixels, startX, startY, radius, r, g, b, a);
            if (startX == endX && startY == endY) break;
            var twiceError = 2 * error;
            if (twiceError >= dy) { error += dy; startX += sx; }
            if (twiceError <= dx) { error += dx; startY += sy; }
        }
        Commit(Width, Height, pixels);
    }

    /// <summary>Crops the current image to a clamped non-empty rectangle.</summary>
    public void Crop(int x, int y, int width, int height)
    {
        var left = Math.Clamp(x, 0, Width - 1);
        var top = Math.Clamp(y, 0, Height - 1);
        var right = Math.Clamp(x + Math.Max(1, width), left + 1, Width);
        var bottom = Math.Clamp(y + Math.Max(1, height), top + 1, Height);
        var cropped = new byte[(right - left) * (bottom - top) * 4];
        var source = _history[_historyIndex].Buffer;
        for (var row = 0; row < bottom - top; row++)
            Buffer.BlockCopy(source, ((top + row) * Width + left) * 4, cropped, row * (right - left) * 4, (right - left) * 4);
        Commit(right - left, bottom - top, cropped);
    }

    public void Undo() { if (CanUndo) Apply(--_historyIndex); }
    public void Redo() { if (CanRedo) Apply(++_historyIndex); }
    public void ZoomIn() => Scale = Math.Min(5, Math.Round(Scale * 1.25, 2));
    public void ZoomOut() => Scale = Math.Max(.2, Math.Round(Scale * .8, 2));
    public void ToggleGrid() => ShowGrid = !ShowGrid;
    public void TogglePanMode() => PanMode = !PanMode;
    public void ToggleCropMode() => CropMode = !CropMode;
    /// <summary>Moves only the rendered viewport; document pixels remain unchanged.</summary>
    public void PanBy(int deltaX, int deltaY) { PanX += deltaX; PanY += deltaY; }

    /// <summary>Rotates the actual pixels clockwise, retaining them in history.</summary>
    public void Rotate90()
    {
        var source = _history[_historyIndex].Buffer;
        var rotated = new byte[Width * Height * 4];
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
            Buffer.BlockCopy(source, (y * Width + x) * 4, rotated, (x * Height + Height - 1 - y) * 4, 4);
        Commit(Height, Width, rotated);
    }

    public static byte[] CreateBlankBuffer(int width, int height, byte r, byte g, byte b, byte a)
    {
        var result = new byte[checked(width * height * 4)];
        for (var index = 0; index < result.Length; index += 4)
            (result[index], result[index + 1], result[index + 2], result[index + 3]) = (r, g, b, a);
        return result;
    }

    private void Commit(int width, int height, byte[] pixels)
    {
        ValidateBuffer(width, height, pixels);
        if (_historyIndex < _history.Count - 1) _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add(new CanvasSnapshot(width, height, pixels.ToArray()));
        if (_history.Count > MaxHistory) _history.RemoveAt(0);
        _historyIndex = _history.Count - 1;
        Apply(_historyIndex);
    }

    private void Apply(int index) { Width = _history[index].Width; Height = _history[index].Height; }
    private static void ValidateBuffer(int width, int height, byte[] pixels)
    {
        if (width is < 1 or > 4096 || height is < 1 or > 4096 || pixels.Length != checked(width * height * 4))
            throw new ArgumentException("The RGBA buffer does not match the bounded document dimensions.", nameof(pixels));
    }

    private void PaintCircle(byte[] pixels, int centerX, int centerY, int radius, byte r, byte g, byte b, byte a)
    {
        for (var y = centerY - radius; y <= centerY + radius; y++)
        for (var x = centerX - radius; x <= centerX + radius; x++)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height || (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) > radius * radius) continue;
            var index = (y * Width + x) * 4;
            (pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]) = (r, g, b, a);
        }
    }

    private static (byte R, byte G, byte B, byte A) ParseColor(string value)
    {
        var hex = value.TrimStart('#');
        return hex.Length == 6 && byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r)
            && byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g)
            && byte.TryParse(hex[4..], System.Globalization.NumberStyles.HexNumber, null, out var b)
            ? (r, g, b, (byte)255) : ((byte)51, (byte)255, (byte)51, (byte)255);
    }
}
