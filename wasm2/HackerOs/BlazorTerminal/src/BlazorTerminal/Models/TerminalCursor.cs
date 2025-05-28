using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BlazorTerminal.Models
{
    /// <summary>
    /// Represents the terminal cursor state and position
    /// </summary>
    public class TerminalCursor : INotifyPropertyChanged
    {
        private int _x;
        private int _y;
        private bool _visible = true;
        private bool _blinking = true;
        private CursorStyle _style = CursorStyle.Block;
        private int _blinkRate = 500; // milliseconds
        private bool _blinkState = true;
        private System.Timers.Timer? _blinkTimer;

        /// <summary>
        /// Event that fires when a property changes
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        
        /// <summary>
        /// Event that fires when the cursor state or position changes
        /// </summary>
        public event EventHandler<EventArgs>? CursorChanged;

        /// <summary>
        /// Gets or sets the X position (column) of the cursor
        /// </summary>
        public int X
        {
            get => _x;
            set
            {
                if (_x != value)
                {
                    _x = value;
                    OnPropertyChanged();
                    CursorChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets the Y position (row) of the cursor
        /// </summary>
        public int Y
        {
            get => _y;
            set
            {
                if (_y != value)
                {
                    _y = value;
                    OnPropertyChanged();
                    CursorChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the cursor is visible
        /// </summary>
        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible != value)
                {
                    _visible = value;
                    OnPropertyChanged();
                    
                    // Start or stop timer based on visibility
                    if (_visible && _blinking)
                    {
                        StartBlinking();
                    }
                    else
                    {
                        StopBlinking();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the cursor blinks
        /// </summary>
        public bool Blinking
        {
            get => _blinking;
            set
            {
                if (_blinking != value)
                {
                    _blinking = value;
                    OnPropertyChanged();
                    CursorChanged?.Invoke(this, EventArgs.Empty);
                    
                    // Start or stop timer based on blink setting
                    if (_blinking && _visible)
                    {
                        StartBlinking();
                    }
                    else
                    {
                        StopBlinking();
                        _blinkState = true;
                        OnPropertyChanged(nameof(BlinkState));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the cursor blinks (alternate property name)
        /// </summary>
        public bool BlinkEnabled
        {
            get => Blinking;
            set => Blinking = value;
        }
        
        /// <summary>
        /// Gets or sets the cursor style (block, underline, bar)
        /// </summary>
        public CursorStyle Style
        {
            get => _style;
            set
            {
                if (_style != value)
                {
                    _style = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the blink rate in milliseconds
        /// </summary>
        public int BlinkRate
        {
            get => _blinkRate;
            set
            {
                if (_blinkRate != value && value > 0)
                {
                    _blinkRate = value;
                    OnPropertyChanged();
                    
                    // Update timer if it exists
                    if (_blinkTimer != null)
                    {
                        _blinkTimer.Interval = _blinkRate;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the current blink state (true = visible, false = hidden)
        /// </summary>
        public bool BlinkState
        {
            get => _blinking ? _blinkState : true;
        }

        /// <summary>
        /// Creates a new cursor at position (0,0)
        /// </summary>
        public TerminalCursor()
        {
            X = 0;
            Y = 0;
            
            if (_visible && _blinking)
            {
                StartBlinking();
            }
        }

        /// <summary>
        /// Creates a new cursor at the specified position
        /// </summary>
        /// <param name="x">Initial X position (column)</param>
        /// <param name="y">Initial Y position (row)</param>
        public TerminalCursor(int x, int y)
        {
            X = x;
            Y = y;
            
            if (_visible && _blinking)
            {
                StartBlinking();
            }
        }

        /// <summary>
        /// Moves the cursor to the specified position
        /// </summary>
        /// <param name="x">X position (column)</param>
        /// <param name="y">Y position (row)</param>
        public void MoveTo(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Moves the cursor up by the specified number of rows
        /// </summary>
        /// <param name="rows">Number of rows to move (default: 1)</param>
        public void MoveUp(int rows = 1)
        {
            Y = Math.Max(0, Y - rows);
        }

        /// <summary>
        /// Moves the cursor down by the specified number of rows
        /// </summary>
        /// <param name="rows">Number of rows to move (default: 1)</param>
        public void MoveDown(int rows = 1)
        {
            Y += rows;
        }

        /// <summary>
        /// Moves the cursor left by the specified number of columns
        /// </summary>
        /// <param name="columns">Number of columns to move (default: 1)</param>
        public void MoveLeft(int columns = 1)
        {
            X = Math.Max(0, X - columns);
        }

        /// <summary>
        /// Moves the cursor right by the specified number of columns
        /// </summary>
        /// <param name="columns">Number of columns to move (default: 1)</param>
        public void MoveRight(int columns = 1)
        {
            X += columns;
        }

        /// <summary>
        /// Starts the cursor blinking
        /// </summary>
        public void StartBlinking()
        {
            StopBlinking();
            
            if (_blinking && _visible)
            {
                _blinkTimer = new System.Timers.Timer(_blinkRate);
                _blinkTimer.Elapsed += (sender, e) => 
                {
                    _blinkState = !_blinkState;
                    OnPropertyChanged(nameof(BlinkState));
                    CursorChanged?.Invoke(this, EventArgs.Empty);
                };
                _blinkTimer.Start();
            }
        }

        /// <summary>
        /// Stops the cursor blinking
        /// </summary>
        private void StopBlinking()
        {
            _blinkTimer?.Stop();
        }

        /// <summary>
        /// Saves the current cursor position
        /// </summary>
        /// <returns>A tuple containing the current X,Y position</returns>
        public (int X, int Y) SavePosition()
        {
            return (X, Y);
        }

        /// <summary>
        /// Restores the cursor position from saved coordinates
        /// </summary>
        /// <param name="position">Tuple containing X,Y coordinates</param>
        public void RestorePosition((int X, int Y) position)
        {
            MoveTo(position.X, position.Y);
        }

        /// <summary>
        /// Cleans up resources used by the cursor
        /// </summary>
        public void Dispose()
        {
            if (_blinkTimer != null)
            {
                _blinkTimer.Stop();
                _blinkTimer.Dispose();
                _blinkTimer = null;
            }
        }

        /// <summary>
        /// Handles property change notifications
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
