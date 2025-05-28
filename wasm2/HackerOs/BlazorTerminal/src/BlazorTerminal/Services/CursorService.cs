using BlazorTerminal.Models;

namespace BlazorTerminal.Services
{
    /// <summary>
    /// Service for handling terminal cursor operations
    /// </summary>
    public class CursorService
    {
        private readonly TerminalCursor _cursor;
        private readonly TerminalBuffer _buffer;
        private (int X, int Y)? _savedPosition;

        /// <summary>
        /// Creates a new CursorService
        /// </summary>
        /// <param name="cursor">The terminal cursor</param>
        /// <param name="buffer">The terminal buffer</param>
        public CursorService(TerminalCursor cursor, TerminalBuffer buffer)
        {
            _cursor = cursor;
            _buffer = buffer;
        }

        /// <summary>
        /// Moves the cursor to the specified position, clamped to the buffer bounds
        /// </summary>
        /// <param name="x">X coordinate (column)</param>
        /// <param name="y">Y coordinate (row)</param>
        public void MoveTo(int x, int y)
        {
            int clampedX = Math.Clamp(x, 0, _buffer.Width - 1);
            int clampedY = Math.Clamp(y, 0, _buffer.Height - 1);
            
            _cursor.MoveTo(clampedX, clampedY);
        }

        /// <summary>
        /// Moves the cursor up by the specified number of rows
        /// </summary>
        /// <param name="count">Number of rows to move (default: 1)</param>
        public void MoveUp(int count = 1)
        {
            int newY = Math.Max(0, _cursor.Y - count);
            _cursor.MoveTo(_cursor.X, newY);
        }

        /// <summary>
        /// Moves the cursor down by the specified number of rows
        /// </summary>
        /// <param name="count">Number of rows to move (default: 1)</param>
        public void MoveDown(int count = 1)
        {
            int newY = Math.Min(_buffer.Height - 1, _cursor.Y + count);
            _cursor.MoveTo(_cursor.X, newY);
        }

        /// <summary>
        /// Moves the cursor left by the specified number of columns
        /// </summary>
        /// <param name="count">Number of columns to move (default: 1)</param>
        public void MoveLeft(int count = 1)
        {
            int newX = Math.Max(0, _cursor.X - count);
            _cursor.MoveTo(newX, _cursor.Y);
        }

        /// <summary>
        /// Moves the cursor right by the specified number of columns
        /// </summary>
        /// <param name="count">Number of columns to move (default: 1)</param>
        public void MoveRight(int count = 1)
        {
            int newX = Math.Min(_buffer.Width - 1, _cursor.X + count);
            _cursor.MoveTo(newX, _cursor.Y);
        }

        /// <summary>
        /// Moves the cursor to the beginning of the current line
        /// </summary>
        public void CarriageReturn()
        {
            _cursor.MoveTo(0, _cursor.Y);
        }

        /// <summary>
        /// Moves the cursor to the beginning of the next line
        /// </summary>
        public void LineFeed()
        {
            int newY = _cursor.Y + 1;
            if (newY >= _buffer.Height)
            {
                // In the future, we'll handle scrolling here
                // For now, we just clamp to the buffer height
                newY = _buffer.Height - 1;
            }
            
            _cursor.MoveTo(0, newY);
        }

        /// <summary>
        /// Moves the cursor to the home position (0,0)
        /// </summary>
        public void Home()
        {
            _cursor.MoveTo(0, 0);
        }

        /// <summary>
        /// Saves the current cursor position
        /// </summary>
        public void SavePosition()
        {
            _savedPosition = (_cursor.X, _cursor.Y);
        }

        /// <summary>
        /// Restores the cursor to the previously saved position
        /// </summary>
        public void RestorePosition()
        {
            if (_savedPosition.HasValue)
            {
                _cursor.MoveTo(_savedPosition.Value.X, _savedPosition.Value.Y);
            }
        }

        /// <summary>
        /// Sets the cursor visibility
        /// </summary>
        /// <param name="visible">Whether the cursor should be visible</param>
        public void SetVisible(bool visible)
        {
            _cursor.Visible = visible;
        }

        /// <summary>
        /// Sets the cursor style
        /// </summary>
        /// <param name="style">The cursor style</param>
        public void SetStyle(CursorStyle style)
        {
            _cursor.Style = style;
        }

        /// <summary>
        /// Sets the cursor blinking state
        /// </summary>
        /// <param name="blinking">Whether the cursor should blink</param>
        public void SetBlinking(bool blinking)
        {
            _cursor.Blinking = blinking;
        }

        /// <summary>
        /// Sets the cursor blink rate
        /// </summary>
        /// <param name="rate">Blink rate in milliseconds</param>
        public void SetBlinkRate(int rate)
        {
            if (rate > 0)
            {
                _cursor.BlinkRate = rate;
            }
        }

        /// <summary>
        /// Moves the cursor up by the specified number of rows (ANSI CUU)
        /// </summary>
        /// <param name="count">Number of rows to move</param>
        public void MoveCursorUp(int count)
        {
            MoveUp(count);
        }
        
        /// <summary>
        /// Moves the cursor down by the specified number of rows (ANSI CUD)
        /// </summary>
        /// <param name="count">Number of rows to move</param>
        public void MoveCursorDown(int count)
        {
            MoveDown(count);
        }
        
        /// <summary>
        /// Moves the cursor forward (right) by the specified number of columns (ANSI CUF)
        /// </summary>
        /// <param name="count">Number of columns to move</param>
        public void MoveCursorForward(int count)
        {
            MoveRight(count);
        }
        
        /// <summary>
        /// Moves the cursor backward (left) by the specified number of columns (ANSI CUB)
        /// </summary>
        /// <param name="count">Number of columns to move</param>
        public void MoveCursorBackward(int count)
        {
            MoveLeft(count);
        }
        
        /// <summary>
        /// Sets the cursor position to the specified coordinates (ANSI CUP)
        /// </summary>
        /// <param name="column">Column (X) coordinate (0-based)</param>
        /// <param name="row">Row (Y) coordinate (0-based)</param>
        public void SetCursorPosition(int column, int row)
        {
            MoveTo(column, row);
        }
    }
}
