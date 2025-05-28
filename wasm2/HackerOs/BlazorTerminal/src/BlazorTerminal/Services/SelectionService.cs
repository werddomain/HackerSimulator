using BlazorTerminal.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Text;
using System.Threading.Tasks;

namespace BlazorTerminal.Services
{
    /// <summary>
    /// Service for managing text selection within the terminal
    /// </summary>
    public class SelectionService
    {
        private readonly IJSRuntime _jsRuntime;
        
        /// <summary>
        /// Start position of the selection
        /// </summary>
        public (int Row, int Column)? SelectionStart { get; private set; }
        
        /// <summary>
        /// End position of the selection
        /// </summary>
        public (int Row, int Column)? SelectionEnd { get; private set; }
        
        /// <summary>
        /// Whether a selection is currently active
        /// </summary>
        public bool HasSelection => SelectionStart.HasValue && SelectionEnd.HasValue;
        
        /// <summary>
        /// Event raised when the selection changes
        /// </summary>
        public event EventHandler<EventArgs>? SelectionChanged;
        
        /// <summary>
        /// Creates a new selection service
        /// </summary>
        /// <param name="jsRuntime">JS runtime for clipboard operations</param>
        public SelectionService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }
        
        /// <summary>
        /// Start a new selection at the specified coordinates
        /// </summary>
        /// <param name="row">Row index (zero-based)</param>
        /// <param name="column">Column index (zero-based)</param>
        public void StartSelection(int row, int column)
        {
            SelectionStart = (row, column);
            SelectionEnd = (row, column); // Initially, selection end is the same as start
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Update the end position of the current selection
        /// </summary>
        /// <param name="row">Row index (zero-based)</param>
        /// <param name="column">Column index (zero-based)</param>
        public void UpdateSelection(int row, int column)
        {
            if (!SelectionStart.HasValue)
            {
                StartSelection(row, column);
                return;
            }
            
            SelectionEnd = (row, column);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Clear the current selection
        /// </summary>
        public void ClearSelection()
        {
            if (SelectionStart.HasValue || SelectionEnd.HasValue)
            {
                SelectionStart = null;
                SelectionEnd = null;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        
        /// <summary>
        /// Check if a position is within the current selection
        /// </summary>
        /// <param name="row">Row index (zero-based)</param>
        /// <param name="column">Column index (zero-based)</param>
        /// <returns>True if the position is within the selection</returns>
        public bool IsPositionSelected(int row, int column)
        {
            if (!HasSelection)
            {
                return false;
            }
            
            // Normalize selection (start should be before end)
            var start = NormalizeSelectionStart();
            var end = NormalizeSelectionEnd();
            
            if (row < start.Item1 || row > end.Item1)
            {
                return false;
            }
            
            if (row == start.Item1 && row == end.Item1)
            {
                return column >= start.Item2 && column <= end.Item2;
            }
            
            if (row == start.Item1)
            {
                return column >= start.Item2;
            }
            
            if (row == end.Item1)
            {
                return column <= end.Item2;
            }
            
            return true;
        }
        
        /// <summary>
        /// Extracts the selected text from the terminal buffer
        /// </summary>
        /// <param name="buffer">The terminal buffer</param>
        /// <returns>The selected text</returns>
        public string GetSelectedText(TerminalBuffer buffer)
        {
            if (!HasSelection || buffer == null)
            {
                return string.Empty;
            }
            
            // Normalize selection (start should be before end)
            var start = NormalizeSelectionStart();
            var end = NormalizeSelectionEnd();
            
            var sb = new StringBuilder();
            
            for (int row = start.Item1; row <= end.Item1; row++)
            {
                var line = buffer.GetLine(row);
                int startCol = (row == start.Item1) ? start.Item2 : 0;
                int endCol = (row == end.Item1) ? end.Item2 : line.Length - 1;
                
                // Handle case where selection extends beyond line length
                endCol = Math.Min(endCol, line.Length - 1);
                
                for (int col = startCol; col <= endCol && col < line.Length; col++)
                {
                    sb.Append(line[col].Character);
                }
                
                // Add newline except for the last row
                if (row < end.Item1)
                {
                    sb.AppendLine();
                }
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Copies the selected text to clipboard
        /// </summary>
        /// <param name="buffer">The terminal buffer</param>
        /// <returns>A task representing the async operation</returns>
        public async Task CopyToClipboardAsync(TerminalBuffer buffer)
        {
            if (!HasSelection || buffer == null)
            {
                return;
            }
            
            string selectedText = GetSelectedText(buffer);
            if (!string.IsNullOrEmpty(selectedText))
            {
                await _jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", selectedText);
            }
        }
        
        /// <summary>
        /// Pastes text from clipboard
        /// </summary>
        /// <returns>The text from clipboard</returns>
        public async Task<string> PasteFromClipboardAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string>("navigator.clipboard.readText");
            }
            catch (JSException)
            {
                // Handle permission issues or other clipboard errors
                return string.Empty;
            }
        }
        
        private (int, int) NormalizeSelectionStart()
        {
            if (!HasSelection)
            {
                throw new InvalidOperationException("No active selection");
            }
            
            var start = SelectionStart!.Value;
            var end = SelectionEnd!.Value;
            
            // Check if selection is reversed
            if (start.Row > end.Row || (start.Row == end.Row && start.Column > end.Column))
            {
                return end;
            }
            
            return start;
        }
        
        private (int, int) NormalizeSelectionEnd()
        {
            if (!HasSelection)
            {
                throw new InvalidOperationException("No active selection");
            }
            
            var start = SelectionStart!.Value;
            var end = SelectionEnd!.Value;
            
            // Check if selection is reversed
            if (start.Row > end.Row || (start.Row == end.Row && start.Column > end.Column))
            {
                return start;
            }
            
            return end;
        }
    }
}
