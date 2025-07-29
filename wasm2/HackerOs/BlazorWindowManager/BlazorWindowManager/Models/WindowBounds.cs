namespace BlazorWindowManager.Models;

/// <summary>
/// Represents the position and size information of a window.
/// </summary>
public class WindowBounds
{
    /// <summary>
    /// The X coordinate of the window's left edge in pixels
    /// </summary>
    public double Left { get; set; }
    
    /// <summary>
    /// The Y coordinate of the window's top edge in pixels
    /// </summary>
    public double Top { get; set; }
    
    /// <summary>
    /// The width of the window in pixels
    /// </summary>
    public double Width { get; set; }
    
    /// <summary>
    /// The height of the window in pixels
    /// </summary>
    public double Height { get; set; }
    
    /// <summary>
    /// Gets the right edge coordinate (Left + Width)
    /// </summary>
    public double Right => Left + Width;
      /// <summary>
    /// Gets the bottom edge coordinate (Top + Height)
    /// </summary>
    public double Bottom => Top + Height;
    
    /// <summary>
    /// Gets the X coordinate (alias for Left)
    /// </summary>
    public double X => Left;
    
    /// <summary>
    /// Gets the Y coordinate (alias for Top)
    /// </summary>
    public double Y => Top;
    
    /// <summary>
    /// Creates a new WindowBounds instance
    /// </summary>
    public WindowBounds(double left = 0, double top = 0, double width = 400, double height = 300)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }
    
    /// <summary>
    /// Creates a copy of this WindowBounds instance
    /// </summary>
    public WindowBounds Clone()
    {
        return new WindowBounds(Left, Top, Width, Height);
    }
    
    /// <summary>
    /// Checks if this bounds intersects with another bounds
    /// </summary>
    public bool IntersectsWith(WindowBounds other)
    {
        return Left < other.Right && Right > other.Left && 
               Top < other.Bottom && Bottom > other.Top;
    }
}
