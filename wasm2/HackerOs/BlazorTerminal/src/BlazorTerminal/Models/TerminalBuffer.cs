using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BlazorTerminal.Models
{
    /// <summary>
    /// Represents a character in the terminal buffer with its style
    /// </summary>
    public class TerminalCharacter
    {
        /// <summary>
        /// Gets or sets the character
        /// </summary>
        public char Character { get; set; } = ' ';
        
        /// <summary>
        /// Gets the style for this character
        /// </summary>
        public TerminalStyle Style { get; }
        
        /// <summary>
        /// Creates a new terminal character with the specified value and style
        /// </summary>
        /// <param name="character">The character</param>
        /// <param name="style">The style</param>
        public TerminalCharacter(char character, TerminalStyle style)
        {
            Character = character;
            Style = style.Clone();
        }
        
        /// <summary>
        /// Creates an empty space character with the specified style
        /// </summary>
        /// <param name="style">The style</param>
        public TerminalCharacter(TerminalStyle style)
        {
            Style = style.Clone();
        }
    }
    
    /// <summary>
    /// Represents a line (row) of characters in the terminal buffer
    /// </summary>
    public class TerminalLine
    {
        private readonly List<TerminalCharacter> _characters;
        
        /// <summary>
        /// Gets the characters in this line
        /// </summary>
        public ReadOnlyCollection<TerminalCharacter> Characters => _characters.AsReadOnly();
        
        /// <summary>
        /// Gets or sets a character at the specified position
        /// </summary>
        /// <param name="index">The character index</param>
        /// <returns>The terminal character at the specified position</returns>
        public TerminalCharacter this[int index]
        {
            get
            {
                EnsureWidth(index + 1);
                return _characters[index];
            }
            set
            {
                EnsureWidth(index + 1);
                _characters[index] = value;
            }
        }
        
        /// <summary>
        /// Gets the number of characters in the line
        /// </summary>
        public int Length => _characters.Count;
        
        /// <summary>
        /// Creates a new terminal line with the specified width
        /// </summary>
        /// <param name="width">Initial line width</param>
        /// <param name="defaultStyle">Default style for uninitialized characters</param>
        public TerminalLine(int width, TerminalStyle defaultStyle)
        {
            _characters = new List<TerminalCharacter>(width);
            EnsureWidth(width, defaultStyle);
        }
        
        /// <summary>
        /// Creates a new empty terminal line
        /// </summary>
        /// <param name="defaultStyle">Default style for uninitialized characters</param>
        public TerminalLine(TerminalStyle defaultStyle)
        {
            _characters = new List<TerminalCharacter>();
        }
        
        /// <summary>
        /// Ensures the line has at least the specified width
        /// </summary>
        /// <param name="width">The minimum width</param>
        /// <param name="style">Style for new characters</param>
        public void EnsureWidth(int width, TerminalStyle? style = null)
        {
            if (width <= _characters.Count) return;
            
            var defaultStyle = style ?? new TerminalStyle();
            for (int i = _characters.Count; i < width; i++)
            {
                _characters.Add(new TerminalCharacter(' ', defaultStyle));
            }
        }
        
        /// <summary>
        /// Clears the line with the specified style
        /// </summary>
        /// <param name="style">The style to use</param>
        public void Clear(TerminalStyle style)
        {
            for (int i = 0; i < _characters.Count; i++)
            {
                _characters[i] = new TerminalCharacter(' ', style);
            }
        }
        
        /// <summary>
        /// Clears a portion of the line with the specified style
        /// </summary>
        /// <param name="startIndex">Start index (inclusive)</param>
        /// <param name="endIndex">End index (exclusive)</param>
        /// <param name="style">The style to use</param>
        public void Clear(int startIndex, int endIndex, TerminalStyle style)
        {
            EnsureWidth(endIndex, style);
            
            for (int i = startIndex; i < endIndex && i < _characters.Count; i++)
            {
                _characters[i] = new TerminalCharacter(' ', style);
            }
        }
        
        /// <summary>
        /// Writes text to the line at the specified position with the specified style
        /// </summary>
        /// <param name="text">The text to write</param>
        /// <param name="startIndex">The position to start writing</param>
        /// <param name="style">The style to use</param>
        /// <returns>Number of characters written</returns>
        public int Write(string text, int startIndex, TerminalStyle style)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            
            EnsureWidth(startIndex + text.Length, style);
            
            for (int i = 0; i < text.Length; i++)
            {
                if (startIndex + i < _characters.Count)
                {
                    _characters[startIndex + i] = new TerminalCharacter(text[i], style);
                }
            }
            
            return text.Length;
        }
    }
      /// <summary>
    /// Represents the terminal buffer containing all visible text and styling
    /// </summary>
    public class TerminalBuffer : INotifyPropertyChanged
    {        /// <summary>
        /// The terminal lines
        /// </summary>
        protected readonly List<TerminalLine> _lines;
        
        /// <summary>
        /// The default style for new characters
        /// </summary>
        protected readonly TerminalStyle _defaultStyle;
        private int _width;
        private int _height;
        
        // Performance optimization fields
        private readonly Queue<TerminalLine> _linePool = new Queue<TerminalLine>();
        private readonly object _bufferLock = new object();
        private HashSet<int> _dirtyLines = new HashSet<int>();
        private bool _bulkUpdateMode = false;
        
        /// <summary>
        /// Event raised when a property changes
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        
        /// <summary>
        /// Event raised when the buffer content changes
        /// </summary>
        public event EventHandler<EventArgs>? ContentChanged;
        
        /// <summary>
        /// Event raised when the buffer content changes
        /// </summary>
        public event EventHandler<EventArgs>? BufferChanged;
        
        /// <summary>
        /// Gets the lines in the buffer
        /// </summary>
        public ReadOnlyCollection<TerminalLine> Lines => _lines.AsReadOnly();
        
        /// <summary>
        /// Gets or sets the width of the buffer (columns)
        /// </summary>
        public int Width
        {
            get => _width;
            set
            {
                if (_width != value)
                {
                    _width = value;
                    ResizeBuffer();
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// Gets or sets the height of the buffer (rows)
        /// </summary>
        public int Height
        {
            get => _height;
            set
            {
                if (_height != value)
                {
                    _height = value;
                    ResizeBuffer();
                    OnPropertyChanged();
                }
            }
        }
          /// <summary>
        /// Creates a new terminal buffer with the specified dimensions
        /// </summary>
        /// <param name="width">Width in columns</param>
        /// <param name="height">Height in rows</param>
        /// <param name="defaultStyle">Default style for new characters</param>
        public TerminalBuffer(int width, int height, TerminalStyle? defaultStyle = null)
        {
            _width = Math.Max(1, width);
            _height = Math.Max(1, height);
            _defaultStyle = defaultStyle ?? new TerminalStyle();
            
            _lines = new List<TerminalLine>();
            ResizeBuffer(true);
        }
        
        /// <summary>
        /// Gets a line at the specified index
        /// </summary>
        /// <param name="index">Line index</param>
        /// <returns>The TerminalLine</returns>
        public TerminalLine GetLine(int index)
        {
            EnsureLineExists(index);
            return _lines[index];
        }
        
        /// <summary>
        /// Clears the entire buffer with the specified or default style
        /// </summary>
        /// <param name="style">The style to use (or null for default style)</param>
        public void Clear(TerminalStyle? style = null)
        {
            var clearStyle = style ?? _defaultStyle;
            
            foreach (var line in _lines)
            {
                line.Clear(clearStyle);
            }
            
            NotifyContentChanged();
        }
        
        /// <summary>
        /// Clears a portion of the buffer from the specified position to the end
        /// </summary>
        /// <param name="startX">Starting X coordinate</param>
        /// <param name="startY">Starting Y coordinate</param>
        /// <param name="style">The style to use (or null for default style)</param>
        public void ClearFromCursor(int startX, int startY, TerminalStyle? style = null)
        {
            var clearStyle = style ?? _defaultStyle;
            
            if (startY >= 0 && startY < _lines.Count)
            {
                // Clear first line from cursor to end
                if (startX < _width)
                {
                    _lines[startY].Clear(startX, _width, clearStyle);
                }
                
                // Clear all lines below
                for (int y = startY + 1; y < _lines.Count; y++)
                {
                    _lines[y].Clear(clearStyle);
                }
                
                NotifyContentChanged();
            }
        }
        
        /// <summary>
        /// Clears a portion of the buffer from the beginning to the specified position
        /// </summary>
        /// <param name="endX">Ending X coordinate</param>
        /// <param name="endY">Ending Y coordinate</param>
        /// <param name="style">The style to use (or null for default style)</param>
        public void ClearToCursor(int endX, int endY, TerminalStyle? style = null)
        {
            var clearStyle = style ?? _defaultStyle;
            
            if (endY >= 0 && endY < _lines.Count)
            {
                // Clear all lines above
                for (int y = 0; y < endY; y++)
                {
                    _lines[y].Clear(clearStyle);
                }
                
                // Clear last line from beginning to cursor
                if (endX > 0)
                {
                    _lines[endY].Clear(0, endX + 1, clearStyle);
                }
                
                NotifyContentChanged();
            }
        }
        
        /// <summary>
        /// Clears a single line
        /// </summary>
        /// <param name="line">Line index</param>
        /// <param name="style">The style to use (or null for default style)</param>
        public void ClearLine(int line, TerminalStyle? style = null)
        {
            var clearStyle = style ?? _defaultStyle;
            
            if (line >= 0 && line < _lines.Count)
            {
                _lines[line].Clear(clearStyle);
                NotifyContentChanged();
            }
        }
        
        /// <summary>
        /// Clears a portion of a line from the cursor to the end
        /// </summary>
        /// <param name="line">Line index</param>
        /// <param name="startX">Starting X coordinate</param>
        /// <param name="style">The style to use (or null for default style)</param>
        public void ClearLineFromCursor(int line, int startX, TerminalStyle? style = null)
        {
            var clearStyle = style ?? _defaultStyle;
            
            if (line >= 0 && line < _lines.Count && startX < _width)
            {
                _lines[line].Clear(startX, _width, clearStyle);
                NotifyContentChanged();
            }
        }
        
        /// <summary>
        /// Clears a portion of a line from the beginning to the cursor
        /// </summary>
        /// <param name="line">Line index</param>
        /// <param name="endX">Ending X coordinate</param>
        /// <param name="style">The style to use (or null for default style)</param>
        public void ClearLineToCursor(int line, int endX, TerminalStyle? style = null)
        {
            var clearStyle = style ?? _defaultStyle;
            
            if (line >= 0 && line < _lines.Count && endX >= 0)
            {
                _lines[line].Clear(0, endX + 1, clearStyle);
                NotifyContentChanged();
            }
        }
        
        /// <summary>
        /// Writes text to the buffer at the specified position with the specified style
        /// </summary>
        /// <param name="text">The text to write</param>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="style">The style to use (or null for default style)</param>
        /// <returns>The new cursor position after writing</returns>
        public (int X, int Y) Write(string text, int x, int y, TerminalStyle? style = null)
        {
            if (string.IsNullOrEmpty(text)) return (x, y);
            
            var writeStyle = style ?? _defaultStyle;
            int currentX = x;
            int currentY = y;
            
            // Split text by newlines
            var lines = text.Split('\n');
            
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    // Move to the beginning of the next line for each newline character
                    currentX = 0;
                    currentY++;
                }
                
                var line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                
                // Check if we need to wrap to the next line
                if (currentX + line.Length > _width)
                {
                    // Handle wrapping
                    int remainingWidth = _width - currentX;
                    int offset = 0;
                    
                    while (offset < line.Length)
                    {
                        int charsToWrite = Math.Min(remainingWidth, line.Length - offset);
                        string segment = line.Substring(offset, charsToWrite);
                        
                        EnsureLineExists(currentY);
                        _lines[currentY].Write(segment, currentX, writeStyle);
                        
                        offset += charsToWrite;
                        
                        if (offset < line.Length)
                        {
                            // Wrap to the next line
                            currentX = 0;
                            currentY++;
                            remainingWidth = _width;
                        }
                        else
                        {
                            // Update X position
                            currentX += charsToWrite;
                        }
                    }
                }
                else
                {
                    // No wrapping needed
                    EnsureLineExists(currentY);
                    _lines[currentY].Write(line, currentX, writeStyle);
                    currentX += line.Length;
                }
            }
            
            NotifyContentChanged();
            return (currentX, currentY);
        }
          /// <summary>
        /// Resizes the buffer to match the current width and height
        /// </summary>
        /// <param name="force">Whether to force full resize even when not needed</param>
        private void ResizeBuffer(bool force = false)
        {
            BeginBulkUpdate();
            
            try
            {
                // Add lines if needed
                while (_lines.Count < _height)
                {
                    _lines.Add(GetPooledLine());
                }
                
                // Remove excess lines and return them to pool
                if (_lines.Count > _height)
                {
                    var excessLines = _lines.GetRange(_height, _lines.Count - _height);
                    foreach (var line in excessLines)
                    {
                        ReturnLineToPool(line);
                    }
                    _lines.RemoveRange(_height, _lines.Count - _height);
                }
                
                // Resize existing lines to match width
                if (force)
                {
                    foreach (var line in _lines)
                    {
                        line.EnsureWidth(_width, _defaultStyle);
                    }
                }
            }
            finally
            {
                EndBulkUpdate();
            }
        }
        
        /// <summary>
        /// Ensures a line exists at the specified index
        /// </summary>
        /// <param name="index">Line index</param>
        private void EnsureLineExists(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Line index cannot be negative");
            }
            
            // Add lines if needed
            while (_lines.Count <= index)
            {
                _lines.Add(new TerminalLine(_width, _defaultStyle));
            }
        }
          /// <summary>
        /// Notifies that the content has changed
        /// </summary>
        protected void NotifyContentChanged()
        {
            if (!_bulkUpdateMode)
            {
                ContentChanged?.Invoke(this, EventArgs.Empty);
                BufferChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        
        /// <summary>
        /// Notifies that a property has changed
        /// </summary>
        /// <param name="propertyName">Name of the property</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
          /// <summary>
        /// Scrolls the buffer up by the specified number of lines
        /// </summary>
        /// <param name="lines">Number of lines to scroll</param>
        /// <param name="defaultStyle">Style to use for new lines</param>
        public void ScrollUp(int lines, TerminalStyle defaultStyle)
        {
            if (lines <= 0)
                return;
                
            // Don't scroll more than the buffer height
            lines = Math.Min(lines, _lines.Count);
            
            BeginBulkUpdate();
            
            try
            {
                // Store the scrolled lines for the scrollback buffer
                var scrolledLines = new TerminalLine[lines];
                for (int i = 0; i < lines; i++)
                {
                    scrolledLines[i] = _lines[i];
                }
                
                // Shift lines up
                for (int i = 0; i < _lines.Count - lines; i++)
                {
                    _lines[i] = _lines[i + lines];
                }
                
                // Add new empty lines at the bottom using object pool
                for (int i = _lines.Count - lines; i < _lines.Count; i++)
                {
                    _lines[i] = GetPooledLine();
                }
                
                // Mark all lines as dirty since scrolling affects the entire buffer
                for (int i = 0; i < _height; i++)
                {
                    MarkLineDirty(i);
                }
            }
            finally
            {
                EndBulkUpdate();
            }
        }
        
        /// <summary>
        /// Sets a character at the specified position in the buffer
        /// </summary>
        /// <param name="x">X coordinate (column)</param>
        /// <param name="y">Y coordinate (row)</param>
        /// <param name="character">Terminal character to set</param>
        public void SetCharacter(int x, int y, TerminalCharacter character)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return;
                
            _lines[y][x] = character;
            BufferChanged?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Erases from the cursor position to the end of the screen
        /// </summary>
        /// <param name="startX">Starting X coordinate</param>
        /// <param name="startY">Starting Y coordinate</param>
        /// <param name="style">Style to use for cleared cells</param>
        public void EraseFromCursor(int startX, int startY, TerminalStyle style)
        {
            if (startX < 0 || startY < 0 || startX >= Width || startY >= Height)
                return;
                
            // Clear from cursor to end of line
            EraseLineFromCursor(startX, startY, style);
            
            // Clear all lines below
            for (int y = startY + 1; y < Height; y++)
            {
                EraseLine(y, style);
            }
            
            BufferChanged?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Erases from the beginning of the screen to the cursor position
        /// </summary>
        /// <param name="endX">Ending X coordinate</param>
        /// <param name="endY">Ending Y coordinate</param>
        /// <param name="style">Style to use for cleared cells</param>
        public void EraseToCursor(int endX, int endY, TerminalStyle style)
        {
            if (endX < 0 || endY < 0 || endX >= Width || endY >= Height)
                return;
                
            // Clear all lines above
            for (int y = 0; y < endY; y++)
            {
                EraseLine(y, style);
            }
            
            // Clear from beginning of line to cursor
            EraseLineToCursor(endX, endY, style);
            
            BufferChanged?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Erases the entire line
        /// </summary>
        /// <param name="y">Line number</param>
        /// <param name="style">Style to use for cleared cells</param>
        public void EraseLine(int y, TerminalStyle style)
        {
            if (y < 0 || y >= Height)
                return;
                
            TerminalLine line = _lines[y];
            
            for (int x = 0; x < Width; x++)
            {
                line[x] = new TerminalCharacter(' ', style);
            }
            
            BufferChanged?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Erases from cursor position to end of line
        /// </summary>
        /// <param name="startX">Starting X coordinate</param>
        /// <param name="y">Line number</param>
        /// <param name="style">Style to use for cleared cells</param>
        public void EraseLineFromCursor(int startX, int y, TerminalStyle style)
        {
            if (startX < 0 || y < 0 || startX >= Width || y >= Height)
                return;
                
            TerminalLine line = _lines[y];
            
            for (int x = startX; x < Width; x++)
            {
                line[x] = new TerminalCharacter(' ', style);
            }
            
            BufferChanged?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Erases from beginning of line to cursor position
        /// </summary>
        /// <param name="endX">Ending X coordinate</param>
        /// <param name="y">Line number</param>
        /// <param name="style">Style to use for cleared cells</param>
        public void EraseLineToCursor(int endX, int y, TerminalStyle style)
        {
            if (endX < 0 || y < 0 || endX >= Width || y >= Height)
                return;
                
            TerminalLine line = _lines[y];
            
            for (int x = 0; x <= endX; x++)
            {
                line[x] = new TerminalCharacter(' ', style);            }
            
            BufferChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Begins a bulk update operation to reduce event notifications
        /// </summary>
        public void BeginBulkUpdate()
        {
            lock (_bufferLock)
            {
                _bulkUpdateMode = true;
            }
        }
        
        /// <summary>
        /// Ends a bulk update operation and fires change notification if needed
        /// </summary>
        public void EndBulkUpdate()
        {
            lock (_bufferLock)
            {
                _bulkUpdateMode = false;
                
                if (_dirtyLines.Count > 0)
                {
                    _dirtyLines.Clear();
                    NotifyContentChanged();
                }
            }
        }
        
        /// <summary>
        /// Gets a pooled line if available, otherwise creates a new one
        /// </summary>
        private TerminalLine GetPooledLine()
        {
            if (_linePool.Count > 0)
            {
                var line = _linePool.Dequeue();
                line.EnsureWidth(_width, _defaultStyle);
                return line;
            }
            
            return new TerminalLine(_width, _defaultStyle);
        }
        
        /// <summary>
        /// Returns a line to the pool for reuse
        /// </summary>
        private void ReturnLineToPool(TerminalLine line)
        {
            if (_linePool.Count < _height * 2) // Limit pool size
            {
                line.Clear(_defaultStyle);
                _linePool.Enqueue(line);
            }
        }
        
        /// <summary>
        /// Marks a line as dirty for selective rendering
        /// </summary>
        public void MarkLineDirty(int lineIndex)
        {
            if (lineIndex >= 0 && lineIndex < _height)
            {
                lock (_bufferLock)
                {
                    _dirtyLines.Add(lineIndex);
                }
            }
        }
        
        /// <summary>
        /// Gets the set of dirty line indices
        /// </summary>
        public HashSet<int> GetDirtyLines()
        {
            lock (_bufferLock)
            {
                return new HashSet<int>(_dirtyLines);
            }
        }
        
        /// <summary>
        /// Clears the dirty lines set
        /// </summary>
        public void ClearDirtyLines()
        {
            lock (_bufferLock)
            {
                _dirtyLines.Clear();
            }
        }
    }
}
