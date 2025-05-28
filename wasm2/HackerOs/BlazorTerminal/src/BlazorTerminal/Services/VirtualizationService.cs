using BlazorTerminal.Models;

namespace BlazorTerminal.Services
{
    /// <summary>
    /// Service for virtualizing large terminal buffers to improve performance
    /// </summary>
    public class VirtualizationService
    {
        private readonly TerminalBuffer _buffer;
        private readonly ScrollbackBuffer _scrollbackBuffer;
        
        private int _viewportStart = 0;
        private int _viewportSize = 24;
        private int _renderBuffer = 5; // Extra lines to render above/below viewport
        
        /// <summary>
        /// Gets the current viewport start position
        /// </summary>
        public int ViewportStart => _viewportStart;
        
        /// <summary>
        /// Gets the current viewport size
        /// </summary>
        public int ViewportSize => _viewportSize;
        
        /// <summary>
        /// Gets the total number of lines available (scrollback + visible)
        /// </summary>
        public int TotalLines => _scrollbackBuffer.Count + _buffer.Height;
        
        /// <summary>
        /// Gets the render start position (viewport start minus render buffer)
        /// </summary>
        public int RenderStart => Math.Max(0, _viewportStart - _renderBuffer);
        
        /// <summary>
        /// Gets the render end position (viewport end plus render buffer)
        /// </summary>
        public int RenderEnd => Math.Min(TotalLines, _viewportStart + _viewportSize + _renderBuffer);
        
        /// <summary>
        /// Gets the number of lines to render
        /// </summary>
        public int RenderCount => RenderEnd - RenderStart;
        
        /// <summary>
        /// Event raised when the viewport changes
        /// </summary>
        public event EventHandler<ViewportChangedEventArgs>? ViewportChanged;
        
        /// <summary>
        /// Creates a new virtualization service
        /// </summary>
        /// <param name="buffer">The terminal buffer</param>
        /// <param name="scrollbackBuffer">The scrollback buffer</param>
        public VirtualizationService(TerminalBuffer buffer, ScrollbackBuffer scrollbackBuffer)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _scrollbackBuffer = scrollbackBuffer ?? throw new ArgumentNullException(nameof(scrollbackBuffer));
            
            // Subscribe to buffer changes to auto-scroll when at bottom
            _buffer.ContentChanged += OnBufferContentChanged;
            _scrollbackBuffer.BufferChanged += OnScrollbackChanged;
        }
        
        /// <summary>
        /// Sets the viewport size
        /// </summary>
        /// <param name="size">The new viewport size</param>
        public void SetViewportSize(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size));
                
            var oldSize = _viewportSize;
            _viewportSize = size;
            
            // Ensure viewport doesn't go beyond available lines
            EnsureViewportBounds();
            
            ViewportChanged?.Invoke(this, new ViewportChangedEventArgs(_viewportStart, _viewportSize, oldSize));
        }
        
        /// <summary>
        /// Scrolls the viewport to the specified position
        /// </summary>
        /// <param name="position">The new viewport start position</param>
        public void ScrollToPosition(int position)
        {
            var oldStart = _viewportStart;
            _viewportStart = Math.Max(0, Math.Min(position, TotalLines - _viewportSize));
            
            if (_viewportStart != oldStart)
            {
                ViewportChanged?.Invoke(this, new ViewportChangedEventArgs(_viewportStart, _viewportSize, _viewportSize));
            }
        }
        
        /// <summary>
        /// Scrolls the viewport by the specified number of lines
        /// </summary>
        /// <param name="lines">Number of lines to scroll (positive = down, negative = up)</param>
        public void ScrollBy(int lines)
        {
            ScrollToPosition(_viewportStart + lines);
        }
        
        /// <summary>
        /// Scrolls to the bottom of the buffer
        /// </summary>
        public void ScrollToBottom()
        {
            ScrollToPosition(Math.Max(0, TotalLines - _viewportSize));
        }
        
        /// <summary>
        /// Scrolls to the top of the buffer
        /// </summary>
        public void ScrollToTop()
        {
            ScrollToPosition(0);
        }
        
        /// <summary>
        /// Checks if the viewport is currently at the bottom
        /// </summary>
        public bool IsAtBottom()
        {
            return _viewportStart + _viewportSize >= TotalLines;
        }
        
        /// <summary>
        /// Gets the lines that should be rendered for the current viewport
        /// </summary>
        /// <returns>Array of terminal lines to render</returns>
        public VirtualizedLine[] GetVisibleLines()
        {
            var visibleLines = new List<VirtualizedLine>();
            int totalLines = TotalLines;
            
            for (int i = RenderStart; i < RenderEnd; i++)
            {
                if (i < _scrollbackBuffer.Count)
                {
                    // Line from scrollback buffer
                    var line = _scrollbackBuffer.GetLine(i);
                    visibleLines.Add(new VirtualizedLine(i, line, VirtualizedLineType.Scrollback));
                }
                else
                {
                    // Line from visible buffer
                    int bufferIndex = i - _scrollbackBuffer.Count;
                    if (bufferIndex < _buffer.Height)
                    {
                        var line = _buffer.GetLine(bufferIndex);
                        visibleLines.Add(new VirtualizedLine(i, line, VirtualizedLineType.Visible));
                    }
                }
            }
            
            return visibleLines.ToArray();
        }
        
        /// <summary>
        /// Gets the scroll position as a percentage (0.0 to 1.0)
        /// </summary>
        public double GetScrollPercentage()
        {
            if (TotalLines <= _viewportSize)
                return 0.0;
                
            return (double)_viewportStart / (TotalLines - _viewportSize);
        }
        
        /// <summary>
        /// Sets the scroll position as a percentage (0.0 to 1.0)
        /// </summary>
        public void SetScrollPercentage(double percentage)
        {
            percentage = Math.Max(0.0, Math.Min(1.0, percentage));
            int maxScroll = Math.Max(0, TotalLines - _viewportSize);
            ScrollToPosition((int)(percentage * maxScroll));
        }
        
        /// <summary>
        /// Handles buffer content changes for auto-scrolling
        /// </summary>
        private void OnBufferContentChanged(object? sender, EventArgs e)
        {
            // Auto-scroll to bottom if we were already at the bottom
            if (IsAtBottom())
            {
                ScrollToBottom();
            }
        }
        
        /// <summary>
        /// Handles scrollback buffer changes
        /// </summary>
        private void OnScrollbackChanged(object? sender, EventArgs e)
        {
            EnsureViewportBounds();
        }
        
        /// <summary>
        /// Ensures the viewport is within valid bounds
        /// </summary>
        private void EnsureViewportBounds()
        {
            int maxStart = Math.Max(0, TotalLines - _viewportSize);
            if (_viewportStart > maxStart)
            {
                ScrollToPosition(maxStart);
            }
        }
        
        /// <summary>
        /// Disposes the virtualization service
        /// </summary>
        public void Dispose()
        {
            _buffer.ContentChanged -= OnBufferContentChanged;
            _scrollbackBuffer.BufferChanged -= OnScrollbackChanged;
        }
    }
    
    /// <summary>
    /// Represents a virtualized line with its position and type
    /// </summary>
    public class VirtualizedLine
    {
        /// <summary>
        /// Gets the absolute line index
        /// </summary>
        public int Index { get; }
        
        /// <summary>
        /// Gets the terminal line
        /// </summary>
        public TerminalLine Line { get; }
        
        /// <summary>
        /// Gets the type of the line (scrollback or visible)
        /// </summary>
        public VirtualizedLineType Type { get; }
        
        /// <summary>
        /// Creates a new virtualized line
        /// </summary>
        public VirtualizedLine(int index, TerminalLine line, VirtualizedLineType type)
        {
            Index = index;
            Line = line;
            Type = type;
        }
    }
    
    /// <summary>
    /// Type of virtualized line
    /// </summary>
    public enum VirtualizedLineType
    {
        /// <summary>
        /// Line from scrollback buffer
        /// </summary>
        Scrollback,
        
        /// <summary>
        /// Line from visible buffer
        /// </summary>
        Visible
    }
    
    /// <summary>
    /// Event arguments for viewport changes
    /// </summary>
    public class ViewportChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the new viewport start position
        /// </summary>
        public int ViewportStart { get; }
        
        /// <summary>
        /// Gets the current viewport size
        /// </summary>
        public int ViewportSize { get; }
        
        /// <summary>
        /// Gets the previous viewport size
        /// </summary>
        public int PreviousViewportSize { get; }
        
        /// <summary>
        /// Creates new viewport change event arguments
        /// </summary>
        public ViewportChangedEventArgs(int viewportStart, int viewportSize, int previousViewportSize)
        {
            ViewportStart = viewportStart;
            ViewportSize = viewportSize;
            PreviousViewportSize = previousViewportSize;
        }
    }
}
