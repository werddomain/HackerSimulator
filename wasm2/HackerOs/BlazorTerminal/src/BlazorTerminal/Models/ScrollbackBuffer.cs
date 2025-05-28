using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BlazorTerminal.Models
{
    /// <summary>
    /// Buffer for storing lines that have scrolled off the visible terminal screen
    /// </summary>
    public class ScrollbackBuffer
    {        
        private readonly List<TerminalLine> _lines;
        private int _maxLines; // Changed from readonly to support Resize method
        private readonly Queue<TerminalLine> _linePool = new Queue<TerminalLine>();
        private bool _bulkUpdateMode = false;
        private int _pendingChanges = 0;
        
        /// <summary>
        /// Gets the current lines in the scrollback buffer
        /// </summary>
        public ReadOnlyCollection<TerminalLine> Lines => _lines.AsReadOnly();
        
        /// <summary>
        /// Gets the number of lines currently in the scrollback buffer
        /// </summary>
        public int Count => _lines.Count;
        
        /// <summary>
        /// Gets the maximum capacity of the scrollback buffer
        /// </summary>
        public int MaxLines => _maxLines;
        
        /// <summary>
        /// Event that is raised when the scrollback buffer content changes
        /// </summary>
        public event EventHandler<EventArgs>? BufferChanged;
        
        /// <summary>
        /// Creates a new scrollback buffer with the specified maximum line capacity
        /// </summary>
        /// <param name="maxLines">The maximum number of lines to store</param>
        public ScrollbackBuffer(int maxLines = 1000)
        {
            if (maxLines <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLines), "Scrollback buffer size must be greater than zero");
            }
            
            _maxLines = maxLines;
            _lines = new List<TerminalLine>(maxLines);
        }
        
        /// <summary>
        /// Adds a line to the scrollback buffer, removing oldest line if at capacity
        /// </summary>
        /// <param name="line">The terminal line to add</param>
        public void AddLine(TerminalLine line)
        {
            if (_bulkUpdateMode)
            {
                _linePool.Enqueue(line);
                _pendingChanges++;
                return;
            }

            if (_lines.Count >= _maxLines)
            {
                var removedLine = _lines[0];
                _lines.RemoveAt(0);
                
                // Return removed line to pool if possible
                ReturnLineToPool(removedLine);
            }
            
            _lines.Add(line);
            _pendingChanges++;
            BufferChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Begins a bulk update operation to reduce event notifications
        /// </summary>
        public void BeginBulkUpdate()
        {
            _bulkUpdateMode = true;
            _pendingChanges = 0;
        }
        
        /// <summary>
        /// Ends a bulk update operation and fires change notification if needed
        /// </summary>
        public void EndBulkUpdate()
        {
            _bulkUpdateMode = false;
            
            if (_pendingChanges > 0)
            {
                BufferChanged?.Invoke(this, EventArgs.Empty);
                _pendingChanges = 0;
            }
        }
        
        /// <summary>
        /// Optimized method to add multiple lines at once
        /// </summary>
        /// <param name="lines">Lines to add</param>
        public void AddLines(IEnumerable<TerminalLine> lines)
        {
            BeginBulkUpdate();
            
            try
            {
                foreach (var line in lines)
                {
                    AddLineInternal(line);
                }
            }
            finally
            {
                EndBulkUpdate();
            }
        }
        
        /// <summary>
        /// Internal method for adding a line without triggering events
        /// </summary>
        private void AddLineInternal(TerminalLine line)
        {
            if (_lines.Count >= _maxLines)
            {
                var removedLine = _lines[0];
                _lines.RemoveAt(0);
                
                // Return removed line to pool if possible
                ReturnLineToPool(removedLine);
            }
            
            _lines.Add(line);
            _pendingChanges++;
        }
        
        /// <summary>
        /// Returns a line to the pool for reuse
        /// </summary>
        private void ReturnLineToPool(TerminalLine line)
        {
            if (_linePool.Count < _maxLines / 2) // Limit pool size
            {
                line.Clear(new TerminalStyle()); // Reset the line
                _linePool.Enqueue(line);
            }
        }
        
        /// <summary>
        /// Gets a line at the specified index in the scrollback buffer
        /// </summary>
        /// <param name="index">Zero-based index</param>
        /// <returns>The terminal line at the specified position</returns>
        public TerminalLine GetLine(int index)
        {
            if (index < 0 || index >= _lines.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            
            return _lines[index];
        }
        
        /// <summary>
        /// Gets a range of lines from the scrollback buffer
        /// </summary>
        /// <param name="startIndex">Start index (inclusive)</param>
        /// <param name="count">Number of lines to retrieve</param>
        /// <returns>Array of terminal lines</returns>
        public TerminalLine[] GetLines(int startIndex, int count)
        {
            if (startIndex < 0 || startIndex >= _lines.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }
            
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            
            int actualCount = Math.Min(count, _lines.Count - startIndex);
            TerminalLine[] result = new TerminalLine[actualCount];
            
            for (int i = 0; i < actualCount; i++)
            {
                result[i] = _lines[startIndex + i];
            }
            
            return result;
        }
        
        /// <summary>
        /// Clears all lines from the scrollback buffer
        /// </summary>
        public void Clear()
        {
            if (_lines.Count > 0)
            {
                _lines.Clear();
                BufferChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        
        /// <summary>
        /// Removes a specified number of oldest lines from the buffer
        /// </summary>
        /// <param name="count">Number of lines to remove</param>
        public void TrimStart(int count)
        {
            if (count <= 0)
            {
                return;
            }
            
            int actualCount = Math.Min(count, _lines.Count);
            if (actualCount > 0)
            {
                _lines.RemoveRange(0, actualCount);
                BufferChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        
        /// <summary>
        /// Resizes the scrollback buffer capacity
        /// </summary>
        /// <param name="newMaxLines">New maximum line count</param>
        public void Resize(int newMaxLines)
        {
            if (newMaxLines <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newMaxLines), "Scrollback buffer size must be greater than zero");
            }
            
            _maxLines = newMaxLines;
            
            // Trim if necessary
            if (_lines.Count > _maxLines)
            {
                int linesToRemove = _lines.Count - _maxLines;
                _lines.RemoveRange(0, linesToRemove);
                BufferChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
