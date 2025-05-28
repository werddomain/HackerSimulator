using BlazorTerminal.Models;
using BlazorTerminal.Services;
using BlazorTerminal.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;

namespace BlazorTerminal.Components;

/// <summary>
/// A Blazor terminal emulator component
/// </summary>
public partial class Terminal : ComponentBase, IDisposable
{    private TerminalBuffer _buffer = null!;
    private TerminalCursor _cursor = null!;
    private TerminalStyle _currentStyle = null!;
    private CursorService _cursorService = null!;
    private KeyboardService _keyboardService = null!;
    private AnsiParser _ansiParser = null!;    private SelectionService _selectionService = null!;
    private ScrollbackBuffer _scrollbackBuffer = null!;
    private VirtualizationService _virtualizationService = null!;
    private PerformanceProfiler _performanceProfiler = null!;
    private bool _initialized = false;
    private System.Timers.Timer? _renderTimer;
    private bool _renderQueued = false;
    private ElementReference _terminalElement;
    private string _uniqueId = Guid.NewGuid().ToString("N");
    private bool _autoScrollToBottom = true;
    private int _firstVisibleRow = 0;
    private bool _isDragging = false;
    private bool _virtualizationEnabled = true;
      // Performance optimization fields
    private readonly object _renderLock = new object();
    private bool _isRenderInProgress = false;
    private DateTime _lastRenderTime = DateTime.MinValue;
    private const int MinRenderIntervalMs = 16; // ~60 FPS
    private HashSet<int> _dirtyRows = new HashSet<int>();
    private bool _fullRedrawNeeded = false;
    private string? _cachedTerminalStyle = null;

    // Character style caching for better performance
    private readonly Dictionary<string, string> _characterStyleCache = new Dictionary<string, string>();
    private readonly object _styleCacheLock = new object();

    /// <summary>
    /// Gets or sets the width of the terminal in characters
    /// </summary>
    [Parameter]
    public int Columns { get; set; } = 80;

    /// <summary>
    /// Gets or sets the height of the terminal in characters
    /// </summary>
    [Parameter]
    public int Rows { get; set; } = 24;

    /// <summary>
    /// Gets or sets the font size in pixels
    /// </summary>
    [Parameter]
    public int FontSize { get; set; } = 16;

    /// <summary>
    /// Gets or sets the font family
    /// </summary>
    [Parameter]
    public string FontFamily { get; set; } = "Consolas, monospace";

    /// <summary>
    /// Gets or sets the cursor style
    /// </summary>
    [Parameter]
    public CursorStyle CursorStyle { get; set; } = CursorStyle.Block;

    /// <summary>
    /// Gets or sets whether the cursor blinks
    /// </summary>
    [Parameter]
    public bool CursorBlink { get; set; } = true;

    /// <summary>
    /// Gets or sets the cursor blink rate in milliseconds
    /// </summary>
    [Parameter]
    public int CursorBlinkRate { get; set; } = 500;
    
    /// <summary>
    /// Gets or sets the terminal theme
    /// </summary>
    [Parameter]
    public TerminalTheme Theme { get; set; } = new TerminalTheme();
    
    /// <summary>
    /// Gets or sets the maximum number of lines in the scrollback buffer
    /// </summary>
    [Parameter]
    public int ScrollbackBufferSize { get; set; } = 1000;
    
    /// <summary>
    /// Gets or sets whether selection is enabled
    /// </summary>
    [Parameter]
    public bool EnableSelection { get; set; } = true;
      /// <summary>
    /// Gets or sets whether the terminal should auto-scroll to the bottom on new output
    /// </summary>
    [Parameter]
    public bool AutoScrollToBottom { get; set; } = true;
      /// <summary>
    /// Gets or sets whether virtualization is enabled for better performance with large buffers
    /// </summary>
    [Parameter]
    public bool EnableVirtualization { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether performance profiling is enabled
    /// </summary>
    [Parameter]
    public bool EnableProfiling { get; set; } = false;
    
    /// <summary>
    /// Event triggered when text is selected in the terminal
    /// </summary>
    [Parameter]
    public EventCallback<string> OnSelection { get; set; }

    /// <summary>
    /// Event triggered when the user presses a key
    /// </summary>
    [Parameter]
    public EventCallback<string> OnInput { get; set; }

    /// <summary>
    /// Event triggered when the terminal needs to be resized
    /// </summary>
    [Parameter]
    public EventCallback<(int Width, int Height)> OnResize { get; set; }
    
    /// <summary>
    /// Event triggered when the terminal content changes
    /// </summary>
    [Parameter]
    public EventCallback<EventArgs> OnChange { get; set; }

    /// <summary>
    /// Gets the terminal buffer
    /// </summary>
    public TerminalBuffer Buffer => _buffer;

    /// <summary>
    /// Gets the terminal cursor
    /// </summary>
    public TerminalCursor Cursor => _cursor;

    /// <summary>
    /// Gets the performance profiler for monitoring terminal performance
    /// </summary>
    public PerformanceProfiler PerformanceProfiler => _performanceProfiler;

    /// <summary>
    /// Gets performance metrics summary
    /// </summary>
    public string GetPerformanceMetrics()
    {
        return _performanceProfiler.GetPerformanceSummary();
    }

    /// <summary>
    /// Clears all performance metrics
    /// </summary>
    public void ClearPerformanceMetrics()
    {
        _performanceProfiler.ClearMetrics();
    }

    /// <summary>
    /// Initializes the component
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        
        _currentStyle = new TerminalStyle();
        _buffer = new TerminalBuffer(Columns, Rows);
        _cursor = new TerminalCursor
        {
            Style = CursorStyle,
            BlinkEnabled = CursorBlink,
            BlinkRate = CursorBlinkRate
        };
        _cursorService = new CursorService(_cursor, _buffer);        _keyboardService = new KeyboardService();
        _ansiParser = new AnsiParser();        _selectionService = new SelectionService(this.JSRuntime);
        _scrollbackBuffer = new ScrollbackBuffer(ScrollbackBufferSize);
        _virtualizationService = new VirtualizationService(_buffer, _scrollbackBuffer);
        _performanceProfiler = new PerformanceProfiler(EnableProfiling);
        _autoScrollToBottom = AutoScrollToBottom;
        _virtualizationEnabled = EnableVirtualization;
        
        // Set up event handlers
        _buffer.BufferChanged += HandleBufferChanged;
        _cursor.CursorChanged += HandleCursorChanged;
        _keyboardService.InputReceived += HandleInputReceived;
        _ansiParser.TextReceived += HandleAnsiTextReceived;
        _ansiParser.CursorUpdate += HandleAnsiCursorUpdate;
        _ansiParser.StyleUpdate += HandleAnsiStyleUpdate;
        _ansiParser.EraseRequest += HandleAnsiEraseRequest;
        _scrollbackBuffer.BufferChanged += HandleScrollbackChanged;
        _virtualizationService.ViewportChanged += HandleViewportChanged;
        
        // Set up cursor blinking
        _cursor.StartBlinking();
        
        _initialized = true;
    }

    /// <summary>
    /// Updates parameters when they change
    /// </summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();        if (_initialized)
        {            // Update buffer size if needed
            if (_buffer.Width != Columns || _buffer.Height != Rows)
            {
                _buffer.Width = Columns;
                _buffer.Height = Rows;
                
                // Update virtualization service viewport size
                if (_virtualizationService != null)
                {
                    _virtualizationService.SetViewportSize(Rows);
                }
                
                InvalidateTerminalStyleCache();
                StateHasChanged();
            }

            // Update cursor properties if needed
            if (_cursor.Style != CursorStyle)
            {
                _cursor.Style = CursorStyle;
            }

            if (_cursor.Blinking != CursorBlink)
            {
                _cursor.Blinking = CursorBlink;
            }

            if (_cursor.BlinkRate != CursorBlinkRate)
            {
                _cursor.BlinkRate = CursorBlinkRate;
            }
            
            // Update virtualization settings
            if (_virtualizationEnabled != EnableVirtualization)
            {
                _virtualizationEnabled = EnableVirtualization;
                QueueRender();
            }
            
            // Invalidate style cache if theme-related parameters changed
            InvalidateTerminalStyleCache();
        }
    }    /// <summary>
    /// Cleans up resources when the component is disposed
    /// </summary>
    public void Dispose()
    {
        _renderTimer?.Stop();
        _renderTimer?.Dispose();
        _cursor.Dispose();
        _virtualizationService?.Dispose();
    }    /// <summary>
    /// Writes text to the terminal at the current cursor position
    /// </summary>
    /// <param name="text">The text to write</param>
    public void Write(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        using (_performanceProfiler.StartTiming("Write"))
        {
            _performanceProfiler.IncrementCounter("CharactersWritten", text.Length);
            ParseAndProcessText(text);
        }
    }

    /// <summary>
    /// Writes a line of text to the terminal followed by a line break
    /// </summary>
    /// <param name="text">The text to write</param>
    public void WriteLine(string text = "")
    {
        Write(text + "\n");
    }

    /// <summary>
    /// Clears the terminal screen
    /// </summary>
    public void Clear()
    {
        _buffer.Clear();
        _cursor.MoveTo(0, 0);
    }

    /// <summary>
    /// Scrolls the terminal viewport up by the specified number of lines
    /// </summary>
    /// <param name="lines">Number of lines to scroll up</param>
    public void ScrollUp(int lines = 1)
    {
        if (_virtualizationEnabled)
        {
            _virtualizationService.ScrollBy(-lines);
        }
        else
        {
            _firstVisibleRow = Math.Max(0, _firstVisibleRow - lines);
            QueueRender();
        }
    }

    /// <summary>
    /// Scrolls the terminal viewport down by the specified number of lines
    /// </summary>
    /// <param name="lines">Number of lines to scroll down</param>
    public void ScrollDown(int lines = 1)
    {
        if (_virtualizationEnabled)
        {
            _virtualizationService.ScrollBy(lines);
        }
        else
        {
            int maxScroll = Math.Max(0, _scrollbackBuffer.Count - 1);
            _firstVisibleRow = Math.Min(maxScroll, _firstVisibleRow + lines);
            QueueRender();
        }
    }

    /// <summary>
    /// Scrolls the terminal viewport to the top
    /// </summary>
    public void ScrollToTop()
    {
        if (_virtualizationEnabled)
        {
            _virtualizationService.ScrollToTop();
        }
        else
        {
            _firstVisibleRow = 0;
            QueueRender();
        }
    }

    /// <summary>
    /// Scrolls the terminal viewport to the bottom
    /// </summary>
    public void ScrollToBottom()
    {
        if (_virtualizationEnabled)
        {
            _virtualizationService.ScrollToBottom();
        }
        else
        {
            _firstVisibleRow = Math.Max(0, _scrollbackBuffer.Count - Rows);
            QueueRender();
        }
    }

    /// <summary>
    /// Gets the current scroll position as a percentage (0.0 to 1.0)
    /// </summary>
    public double GetScrollPercentage()
    {
        if (_virtualizationEnabled)
        {
            return _virtualizationService.GetScrollPercentage();
        }
        else
        {
            int maxScroll = Math.Max(0, _scrollbackBuffer.Count - Rows);
            return maxScroll > 0 ? (double)_firstVisibleRow / maxScroll : 0.0;
        }
    }

    /// <summary>
    /// Sets the scroll position as a percentage (0.0 to 1.0)
    /// </summary>
    public void SetScrollPercentage(double percentage)
    {
        if (_virtualizationEnabled)
        {
            _virtualizationService.SetScrollPercentage(percentage);
        }
        else
        {
            percentage = Math.Max(0.0, Math.Min(1.0, percentage));
            int maxScroll = Math.Max(0, _scrollbackBuffer.Count - Rows);
            _firstVisibleRow = (int)(percentage * maxScroll);
            QueueRender();
        }
    }

    /// <summary>
    /// Parses and processes text including ANSI escape sequences
    /// </summary>
    /// <param name="text">The text to process</param>
    private void ParseAndProcessText(string text)
    {
        // For now, this is a simple implementation without ANSI parsing
        // We'll expand this in Task 2.2 (ANSI Parser)
        
        // Handle basic control characters
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            
            switch (c)
            {
                case '\r': // Carriage return
                    _cursorService.CarriageReturn();
                    break;
                case '\n': // Line feed
                    _cursorService.LineFeed();
                    break;
                case '\t': // Tab
                    // Move cursor to the next tab stop (typically 8 spaces)
                    int spaces = 8 - (_cursor.X % 8);
                    _buffer.Write(new string(' ', spaces), _cursor.X, _cursor.Y, _currentStyle);
                    _cursor.MoveRight(spaces);
                    break;
                case '\b': // Backspace
                    _cursorService.MoveLeft();
                    break;
                case '\u001B': // ESC - beginning of escape sequence
                    // Will be handled in the ANSI parser in Task 2.2
                    // For now, just skip it
                    break;
                default:
                    // Regular character, write it to the buffer
                    string charStr = c.ToString();
                    var cursorPos = _buffer.Write(charStr, _cursor.X, _cursor.Y, _currentStyle);
                    _cursor.MoveTo(cursorPos.X, cursorPos.Y);
                    break;
            }
        }        // Ensure cursor stays within bounds
        if (_cursor.X >= Columns)
        {
            _cursor.X = 0;
            _cursor.Y++;
        }
        
        // Notify any listeners that terminal content has changed
        if (OnChange.HasDelegate)
        {
            OnChange.InvokeAsync(EventArgs.Empty);
        }
    }    /// <summary>
    /// Handles changes to the terminal buffer
    /// </summary>
    private void HandleBufferChanged(object? sender, EventArgs e)
    {
        // For now, use full redraw. In the future, we can implement selective rendering
        // by enhancing the TerminalBuffer to provide changed row information
        QueueFullRedraw();
        OnChange.InvokeAsync(EventArgs.Empty);
    }
    
    /// <summary>
    /// Handles changes to the cursor position or state
    /// </summary>
    private void HandleCursorChanged(object? sender, EventArgs e)
    {
        // Cursor changes usually only affect rendering, not a full redraw
        QueueRender();
    }
    
    /// <summary>
    /// Handles text received from the ANSI parser
    /// </summary>
    private void HandleAnsiTextReceived(object? sender, string text)
    {
        // Process regular text
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            
            if (c == '\n')
            {
                // Handle line feed
                _cursor.Y++;
                if (_cursor.Y >= _buffer.Height)
                {
                    _buffer.ScrollUp(1, _currentStyle);
                    _cursor.Y = _buffer.Height - 1;
                }
            }
            else if (c == '\r')
            {
                // Handle carriage return
                _cursor.X = 0;
            }
            else if (c == '\t')
            {
                // Handle tab (move to next tab stop - usually 8 spaces)
                int tabWidth = 8;
                int nextStop = (_cursor.X + tabWidth) / tabWidth * tabWidth;
                _cursor.X = Math.Min(nextStop, _buffer.Width - 1);
            }
            else if (c == '\b')
            {
                // Handle backspace
                if (_cursor.X > 0)
                {
                    _cursor.X--;
                }
            }
            else if (!char.IsControl(c))
            {
                // Regular character
                _buffer.SetCharacter(_cursor.X, _cursor.Y, new TerminalCharacter(c, _currentStyle));
                _cursor.X++;
                
                if (_cursor.X >= _buffer.Width)
                {
                    _cursor.X = 0;
                    _cursor.Y++;
                    
                    if (_cursor.Y >= _buffer.Height)
                    {
                        // Need to scroll
                        _buffer.ScrollUp(1, _currentStyle);
                        _cursor.Y = _buffer.Height - 1;
                    }
                }
            }
        }
        
        QueueRender();
    }
    
    /// <summary>
    /// Handles cursor update requests from the ANSI parser
    /// </summary>
    private void HandleAnsiCursorUpdate(object? sender, CursorUpdateEventArgs e)
    {
        switch (e.Direction)
        {
            case CursorDirection.Up:
                _cursorService.MoveCursorUp(e.Count);
                break;
                
            case CursorDirection.Down:
                _cursorService.MoveCursorDown(e.Count);
                break;
                
            case CursorDirection.Forward:
                _cursorService.MoveCursorForward(e.Count);
                break;
                
            case CursorDirection.Back:
                _cursorService.MoveCursorBackward(e.Count);
                break;
                
            case CursorDirection.Absolute:
                _cursorService.SetCursorPosition(e.Column, e.Row);
                break;
        }
        
        QueueRender();
    }
    
    /// <summary>
    /// Handles style update requests from the ANSI parser
    /// </summary>
    private void HandleAnsiStyleUpdate(object? sender, StyleUpdateEventArgs e)
    {
        switch (e.StyleOperation)
        {
            case StyleOperation.Reset:
                _currentStyle = new TerminalStyle();
                break;
                
            case StyleOperation.Bold:
                _currentStyle.Bold = (bool)e.Value!;
                break;
                
            case StyleOperation.Italic:
                _currentStyle.Italic = (bool)e.Value!;
                break;
                
            case StyleOperation.Underline:
                _currentStyle.Underline = (bool)e.Value!;
                break;
                
            case StyleOperation.ForegroundColor:
                _currentStyle.ForegroundColor = (int)e.Value!;
                break;
                
            case StyleOperation.BackgroundColor:
                _currentStyle.BackgroundColor = (int)e.Value!;
                break;
                
            case StyleOperation.ForegroundColor256:
                _currentStyle.ForegroundColor = (int)e.Value!;
                _currentStyle.UseExtendedForeground = true;
                break;
                
            case StyleOperation.BackgroundColor256:
                _currentStyle.BackgroundColor = (int)e.Value!;
                _currentStyle.UseExtendedBackground = true;
                break;
                
            case StyleOperation.DefaultForeground:
                _currentStyle.ForegroundColor = TerminalConstants.DefaultForegroundColor;
                _currentStyle.UseExtendedForeground = false;
                break;
                
            case StyleOperation.DefaultBackground:
                _currentStyle.BackgroundColor = TerminalConstants.DefaultBackgroundColor;
                _currentStyle.UseExtendedBackground = false;
                break;
        }
        
        QueueRender();
    }
    
    /// <summary>
    /// Handles erase requests from the ANSI parser
    /// </summary>
    private void HandleAnsiEraseRequest(object? sender, EraseEventArgs e)
    {
        if (e.Type == EraseType.Display)
        {
            switch (e.Mode)
            {
                case 0: // Erase from cursor to end of screen
                    _buffer.EraseFromCursor(_cursor.X, _cursor.Y, _currentStyle);
                    break;
                    
                case 1: // Erase from start of screen to cursor
                    _buffer.EraseToCursor(_cursor.X, _cursor.Y, _currentStyle);
                    break;
                    
                case 2: // Erase entire screen
                case 3: // Erase entire screen and scrollback (treat the same for now)
                    _buffer.Clear(_currentStyle);
                    break;
            }
        }
        else if (e.Type == EraseType.Line)
        {
            switch (e.Mode)
            {
                case 0: // Erase from cursor to end of line
                    _buffer.EraseLineFromCursor(_cursor.X, _cursor.Y, _currentStyle);
                    break;
                    
                case 1: // Erase from start of line to cursor
                    _buffer.EraseLineToCursor(_cursor.X, _cursor.Y, _currentStyle);
                    break;
                    
                case 2: // Erase entire line
                    _buffer.EraseLine(_cursor.Y, _currentStyle);
                    break;
            }
        }
        
        QueueRender();
    }
    
    /// <summary>
    /// Handles scrollback buffer changes
    /// </summary>
    private void HandleScrollbackChanged(object? sender, EventArgs e)
    {
        QueueRender();
    }
    
    /// <summary>
    /// Handles keyboard input from the keyboard service
    /// </summary>
    private void HandleInputReceived(object? sender, string input)
    {
        // Forward to parent component
        OnInput.InvokeAsync(input);
    }
    
    /// <summary>
    /// Handles key down events
    /// </summary>
    private void HandleKeyDown(KeyboardEventArgs e)
    {
        if (!_initialized) return;
        
        _keyboardService.HandleKeyDown(e);
    }    /// <summary>
    /// Queues a render update to improve performance
    /// </summary>
    private void QueueRender()
    {
        using (_performanceProfiler.StartTiming("QueueRender"))
        {
            lock (_renderLock)
            {
                if (!_renderQueued && !_isRenderInProgress)
                {
                    _renderQueued = true;
                    
                    // Use a timer to batch multiple updates together
                    var timeSinceLastRender = DateTime.UtcNow - _lastRenderTime;
                    if (timeSinceLastRender.TotalMilliseconds < MinRenderIntervalMs)
                    {
                        // Defer rendering to maintain frame rate
                        var delay = MinRenderIntervalMs - (int)timeSinceLastRender.TotalMilliseconds;
                        Task.Delay(delay).ContinueWith(_ => InvokeRender());
                    }
                    else
                    {
                        // Render immediately
                        InvokeRender();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Queues a render update for specific rows
    /// </summary>
    /// <param name="row">The row to mark as dirty</param>
    private void QueueRender(int row)
    {
        lock (_renderLock)
        {
            _dirtyRows.Add(row);
            QueueRender();
        }
    }

    /// <summary>
    /// Queues a full redraw
    /// </summary>
    private void QueueFullRedraw()
    {
        lock (_renderLock)
        {
            _fullRedrawNeeded = true;
            _dirtyRows.Clear();
            QueueRender();
        }
    }    /// <summary>
    /// Invokes a render update on the UI thread
    /// </summary>
    private async void InvokeRender()
    {
        lock (_renderLock)
        {
            if (_isRenderInProgress)
                return;
                
            _isRenderInProgress = true;
            _renderQueued = false;
        }

        try
        {
            await InvokeAsync(() =>
            {
                _lastRenderTime = DateTime.UtcNow;
                
                // Clear dirty tracking after render
                lock (_renderLock)
                {
                    _dirtyRows.Clear();
                    _fullRedrawNeeded = false;
                }
                
                StateHasChanged();
            });
        }
        finally
        {
            lock (_renderLock)
            {
                _isRenderInProgress = false;
            }
        }
    }

    /// <summary>
    /// Gets the CSS class for the cursor based on the current cursor style
    /// </summary>
    private string GetCursorClass()
    {
        string baseClass = "terminal-cursor";
        
        string styleClass = _cursor.Style switch
        {
            CursorStyle.Block => "terminal-cursor-block",
            CursorStyle.Underline => "terminal-cursor-underline",
            CursorStyle.Bar => "terminal-cursor-bar",
            _ => "terminal-cursor-block"
        };

        string blinkClass = _cursor.Blinking ? "terminal-cursor-blink" : "";
        
        return $"{baseClass} {styleClass} {blinkClass}".Trim();
    }

    /// <summary>
    /// Calculates the cursor style (position)
    /// </summary>
    private string GetCursorStyle()
    {
        var charWidth = 100.0 / Columns;
        var charHeight = 100.0 / Rows;
        
        var left = _cursor.X * charWidth;
        var top = _cursor.Y * charHeight;
        
        return $"left: {left}%; top: {top}%; width: {charWidth}%; height: {charHeight}%;";
    }

    /// <summary>
    /// Gets the character position from mouse coordinates
    /// </summary>
    /// <param name="clientX">Client X coordinate</param>
    /// <param name="clientY">Client Y coordinate</param>
    /// <returns>The row and column of the character, or null if outside bounds</returns>
    private (int row, int column)? GetCharacterPositionFromMouseCoordinates(double clientX, double clientY)
    {
        // Need to implement bounds checking and proper conversion
        // This is a simplified version
        
        // Convert client coordinates to element coordinates
        // This would require JavaScript interop in a real implementation
        // For now, we'll use a simple approximation
        
        int charWidth = FontSize / 2;  // Approximation for monospace fonts
        int charHeight = (int)(FontSize * 1.2);  // Approximation with line height
        
        // Calculate column and row
        int column = (int)(clientX / charWidth);
        int row = (int)(clientY / charHeight) + _firstVisibleRow;
        
        // Clamp to buffer bounds
        column = Math.Clamp(column, 0, Buffer.Width - 1);
        
        // Check if within scrollback or visible buffer
        if (row < -_scrollbackBuffer.Count)
            return null;
            
        if (row >= Buffer.Height)
            return null;
            
        return (row, column);
    }
    
    /// <summary>
    /// Handles mouse down events for selection
    /// </summary>
    private void HandleMouseDown(MouseEventArgs e)
    {
        if (!_initialized || !EnableSelection)
            return;
            
        _isDragging = true;
        
        var position = GetCharacterPositionFromMouseCoordinates(e.ClientX, e.ClientY);
        if (position != null)
        {
            _selectionService.StartSelection(position.Value.row, position.Value.column);
        }
    }
    
    /// <summary>
    /// Handles mouse move events for selection
    /// </summary>
    private void HandleMouseMove(MouseEventArgs e)
    {
        if (!_initialized || !EnableSelection || !_isDragging)
            return;
            
        var position = GetCharacterPositionFromMouseCoordinates(e.ClientX, e.ClientY);
        if (position != null)
        {
            _selectionService.UpdateSelection(position.Value.row, position.Value.column);
            QueueRender();
        }
    }
    
    /// <summary>
    /// Handles mouse up events for selection
    /// </summary>
    private async Task HandleMouseUp(MouseEventArgs e)
    {
        if (!_initialized || !EnableSelection)
            return;
            
        _isDragging = false;
        
        if (_selectionService.HasSelection)
        {
            string selectedText = _selectionService.GetSelectedText(_buffer);
            await OnSelection.InvokeAsync(selectedText);
            
            // If it was a double-click (need to implement detection), automatically copy
            // For now, we'll just copy on any selection
            await _selectionService.CopyToClipboardAsync(_buffer);
        }
    }
    
    /// <summary>
    /// Handles mouse wheel events for scrolling
    /// </summary>
    private void HandleMouseWheel(WheelEventArgs e)
    {
        if (!_initialized)
            return;
            
        // Wheel delta is typically 100-120 per "click"
        // Normalize to 1 line per wheel click
        int scrollLines = (int)Math.Sign(e.DeltaY);
        
        // Scroll up (negative) or down (positive)
        Scroll(scrollLines);
    }
    
    /// <summary>
    /// Scrolls the terminal view by the specified number of lines
    /// </summary>
    /// <param name="lines">Number of lines to scroll (negative = up, positive = down)</param>
    public void Scroll(int lines)
    {
        if (lines == 0)
            return;
            
        int newFirstVisibleRow = _firstVisibleRow + lines;
        
        // Limit scrolling up to the available scrollback buffer
        int scrollbackLimit = -_scrollbackBuffer.Count;
        newFirstVisibleRow = Math.Max(scrollbackLimit, newFirstVisibleRow);
        
        // Limit scrolling down to show the current buffer
        newFirstVisibleRow = Math.Min(0, newFirstVisibleRow);
        
        if (_firstVisibleRow != newFirstVisibleRow)
        {
            _firstVisibleRow = newFirstVisibleRow;
            _autoScrollToBottom = _firstVisibleRow == 0;
            QueueRender();
        }
    }
    
    /// <summary>
    /// Gets the inline style for a terminal character
    /// </summary>
    /// <param name="character">The terminal character</param>
    /// <returns>CSS style string</returns>
    private string GetCharacterStyle(TerminalCharacter character)
    {
        return character.Style.GetInlineStyle();
    }

    /// <summary>
    /// Gets the CSS style for a character, with optional selection
    /// </summary>
    private string GetCharacterStyle(TerminalCharacter character, bool isSelected = false)
    {
        var style = new List<string>();
        
        // Add foreground and background colors
        style.Add($"color: {character.Style.Foreground.ToHtmlString()};");
        style.Add($"background-color: {character.Style.Background.ToHtmlString()};");
        
        // Add text decorations
        if (character.Style.IsBold)
            style.Add("font-weight: bold;");
            
        if (character.Style.IsItalic)
            style.Add("font-style: italic;");
            
        if (character.Style.IsUnderlined)
            style.Add("text-decoration: underline;");
            
        if (character.Style.IsCrossedOut)
            style.Add("text-decoration: line-through;");
            
        if (character.Style.IsBlinking)
            style.Add("animation: terminal-text-blink 1s step-start infinite;");
            
        if (character.Style.IsReversed)
            style.Add("filter: invert(100%);");
            
        if (character.Style.IsConcealed)
            style.Add("visibility: hidden;");
            
        // Override background color if selected
        if (isSelected)
            style.Add($"background-color: {Theme.SelectionBackground};");
            
        return string.Join(" ", style);
    }

    /// <summary>
    /// Gets the CSS style for a character with caching for better performance
    /// </summary>
    private string GetCharacterStyleCached(TerminalCharacter character, bool isSelected = false)
    {
        // Create a cache key based on the character's style and selection state
        var cacheKey = $"{character.Style.GetHashCode()}:{isSelected}";
        
        lock (_styleCacheLock)
        {
            if (_characterStyleCache.TryGetValue(cacheKey, out var cachedStyle))
            {
                return cachedStyle;
            }
            
            var style = GetCharacterStyle(character, isSelected);
            
            // Limit cache size to prevent memory leaks
            if (_characterStyleCache.Count > 1000)
            {
                _characterStyleCache.Clear();
            }
            
            _characterStyleCache[cacheKey] = style;
            return style;
        }
    }

    /// <summary>
    /// Gets the cached terminal container style
    /// </summary>
    private string GetTerminalContainerStyle()
    {
        if (_cachedTerminalStyle == null)
        {
            _cachedTerminalStyle = $"font-family: {Theme.FontFamily}; font-size: {Theme.FontSize}; background-color: {Theme.Background}; color: {Theme.Foreground};";
        }
        return _cachedTerminalStyle;
    }
    
    /// <summary>
    /// Invalidates the cached terminal style when theme changes
    /// </summary>
    private void InvalidateTerminalStyleCache()
    {
        _cachedTerminalStyle = null;
    }

    /// <summary>
    /// Handles changes to the viewport for virtualization
    /// </summary>
    private void HandleViewportChanged(object? sender, ViewportChangedEventArgs e)
    {
        QueueRender();
    }
    
    /// <summary>
    /// Gets the lines that should be rendered based on virtualization settings
    /// </summary>
    private VirtualizedLine[] GetRenderableLines()
    {
        if (_virtualizationEnabled)
        {
            return _virtualizationService.GetVisibleLines();
        }
        else
        {
            // Return all visible buffer lines without virtualization
            var lines = new List<VirtualizedLine>();
            for (int i = 0; i < _buffer.Height; i++)
            {
                lines.Add(new VirtualizedLine(i, _buffer.GetLine(i), VirtualizedLineType.Visible));
            }
            return lines.ToArray();
        }
    }
}
