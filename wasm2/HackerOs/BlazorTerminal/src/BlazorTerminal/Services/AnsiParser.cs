using BlazorTerminal.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace BlazorTerminal.Services
{
    /// <summary>
    /// Service responsible for parsing ANSI escape sequences and applying them to terminal state
    /// </summary>
    public class AnsiParser
    {
        private readonly AnsiStateMachine _stateMachine;
        private readonly StringBuilder _textBuffer;
        
        // Event for non-escape sequence text
        public event EventHandler<string>? TextReceived;
        
        // Event for cursor changes
        public event EventHandler<CursorUpdateEventArgs>? CursorUpdate;
        
        // Event for style changes
        public event EventHandler<StyleUpdateEventArgs>? StyleUpdate;
        
        // Event for screen clearing
        public event EventHandler<EraseEventArgs>? EraseRequest;
        
        /// <summary>
        /// Creates a new ANSI parser
        /// </summary>
        public AnsiParser()
        {
            _stateMachine = new AnsiStateMachine();
            _textBuffer = new StringBuilder();
            
            // Set up sequence processing
            _stateMachine.SequenceProcessed += HandleSequence;
        }
        
        /// <summary>
        /// Process a string of text that may contain ANSI sequences
        /// </summary>
        /// <param name="text">The text to parse</param>
        public void Parse(string text)
        {
            foreach (char c in text)
            {
                if (!_stateMachine.Process(c))
                {
                    // Not part of an escape sequence, append to text buffer
                    _textBuffer.Append(c);
                }
                else
                {
                    // Part of an escape sequence, flush any pending text
                    FlushTextBuffer();
                }
            }
            
            // Flush any remaining text
            FlushTextBuffer();
        }
        
        private void FlushTextBuffer()
        {
            if (_textBuffer.Length > 0)
            {
                TextReceived?.Invoke(this, _textBuffer.ToString());
                _textBuffer.Clear();
            }
        }
        
        private void HandleSequence(object? sender, AnsiSequence sequence)
        {
            switch (sequence.Command)
            {
                // Cursor Movement
                case 'A': // CUU - Cursor Up
                    CursorUpdate?.Invoke(this, new CursorUpdateEventArgs 
                    { 
                        Direction = CursorDirection.Up, 
                        Count = sequence.Parameters.Length > 0 ? Math.Max(1, sequence.Parameters[0]) : 1 
                    });
                    break;
                    
                case 'B': // CUD - Cursor Down
                    CursorUpdate?.Invoke(this, new CursorUpdateEventArgs 
                    { 
                        Direction = CursorDirection.Down, 
                        Count = sequence.Parameters.Length > 0 ? Math.Max(1, sequence.Parameters[0]) : 1 
                    });
                    break;
                    
                case 'C': // CUF - Cursor Forward
                    CursorUpdate?.Invoke(this, new CursorUpdateEventArgs 
                    { 
                        Direction = CursorDirection.Forward, 
                        Count = sequence.Parameters.Length > 0 ? Math.Max(1, sequence.Parameters[0]) : 1 
                    });
                    break;
                    
                case 'D': // CUB - Cursor Back
                    CursorUpdate?.Invoke(this, new CursorUpdateEventArgs 
                    { 
                        Direction = CursorDirection.Back, 
                        Count = sequence.Parameters.Length > 0 ? Math.Max(1, sequence.Parameters[0]) : 1 
                    });
                    break;
                    
                case 'H': // CUP - Cursor Position
                case 'f': // HVP - Horizontal & Vertical Position (same as CUP)
                    CursorUpdate?.Invoke(this, new CursorUpdateEventArgs 
                    { 
                        Direction = CursorDirection.Absolute, 
                        Row = sequence.Parameters.Length > 0 ? Math.Max(1, sequence.Parameters[0]) - 1 : 0,  // Convert to 0-based
                        Column = sequence.Parameters.Length > 1 ? Math.Max(1, sequence.Parameters[1]) - 1 : 0  // Convert to 0-based
                    });
                    break;
                
                // Erasing
                case 'J': // ED - Erase in Display
                    EraseRequest?.Invoke(this, new EraseEventArgs 
                    { 
                        Type = EraseType.Display, 
                        Mode = sequence.Parameters.Length > 0 ? sequence.Parameters[0] : 0 
                    });
                    break;
                    
                case 'K': // EL - Erase in Line
                    EraseRequest?.Invoke(this, new EraseEventArgs 
                    { 
                        Type = EraseType.Line, 
                        Mode = sequence.Parameters.Length > 0 ? sequence.Parameters[0] : 0 
                    });
                    break;
                
                // Styling (SGR)
                case 'm': // SGR - Select Graphic Rendition
                    HandleSgrSequence(sequence.Parameters);
                    break;
                
                // Add more command handlers as needed
            }
        }
        
        private void HandleSgrSequence(int[] parameters)
        {
            if (parameters.Length == 0)
            {
                // Empty parameter list defaults to 0 (reset)
                StyleUpdate?.Invoke(this, new StyleUpdateEventArgs { StyleOperation = StyleOperation.Reset });
                return;
            }
            
            for (int i = 0; i < parameters.Length; i++)
            {
                int param = parameters[i];
                
                if (param == 0) // Reset all attributes
                {
                    StyleUpdate?.Invoke(this, new StyleUpdateEventArgs { StyleOperation = StyleOperation.Reset });
                }
                else if (param == 1) // Bold
                {
                    StyleUpdate?.Invoke(this, new StyleUpdateEventArgs { StyleOperation = StyleOperation.Bold, Value = true });
                }
                else if (param == 3) // Italic
                {
                    StyleUpdate?.Invoke(this, new StyleUpdateEventArgs { StyleOperation = StyleOperation.Italic, Value = true });
                }
                else if (param == 4) // Underline
                {
                    StyleUpdate?.Invoke(this, new StyleUpdateEventArgs { StyleOperation = StyleOperation.Underline, Value = true });
                }
                else if (param == 22) // Not bold
                {
                    StyleUpdate?.Invoke(this, new StyleUpdateEventArgs { StyleOperation = StyleOperation.Bold, Value = false });
                }
                else if (param == 23) // Not italic
                {
                    StyleUpdate?.Invoke(this, new StyleUpdateEventArgs { StyleOperation = StyleOperation.Italic, Value = false });
                }
                else if (param == 24) // Not underlined
                {
                    StyleUpdate?.Invoke(this, new StyleUpdateEventArgs { StyleOperation = StyleOperation.Underline, Value = false });
                }
                else if (param >= 30 && param <= 37) // Foreground color
                {
                    StyleUpdate?.Invoke(this, new StyleUpdateEventArgs { StyleOperation = StyleOperation.ForegroundColor, Value = param - 30 });
                }
                else if (param >= 40 && param <= 47) // Background color
                {
                    StyleUpdate?.Invoke(this, new StyleUpdateEventArgs { StyleOperation = StyleOperation.BackgroundColor, Value = param - 40 });
                }
                else if (param == 38 || param == 48) // Extended color (foreground: 38, background: 48)
                {
                    if (i + 1 < parameters.Length)
                    {
                        bool isForeground = param == 38;
                        int colorMode = parameters[i + 1];
                        
                        if (colorMode == 5 && i + 2 < parameters.Length) // 8-bit color (256 colors)
                        {
                            int colorValue = parameters[i + 2];
                            StyleUpdate?.Invoke(this, new StyleUpdateEventArgs 
                            { 
                                StyleOperation = isForeground ? StyleOperation.ForegroundColor256 : StyleOperation.BackgroundColor256, 
                                Value = colorValue 
                            });
                            i += 2; // Skip the additional parameters
                        }
                        else if (colorMode == 2 && i + 4 < parameters.Length) // 24-bit RGB color
                        {
                            int r = parameters[i + 2];
                            int g = parameters[i + 3];
                            int b = parameters[i + 4];
                            int rgb = (r << 16) | (g << 8) | b;
                            
                            StyleUpdate?.Invoke(this, new StyleUpdateEventArgs 
                            { 
                                StyleOperation = isForeground ? StyleOperation.ForegroundColorRgb : StyleOperation.BackgroundColorRgb, 
                                Value = rgb 
                            });
                            i += 4; // Skip the additional parameters
                        }
                        else
                        {
                            i++; // Skip just the color mode parameter if we don't understand the format
                        }
                    }
                }
                else if (param == 39) // Default foreground color
                {
                    StyleUpdate?.Invoke(this, new StyleUpdateEventArgs { StyleOperation = StyleOperation.DefaultForeground });
                }
                else if (param == 49) // Default background color
                {
                    StyleUpdate?.Invoke(this, new StyleUpdateEventArgs { StyleOperation = StyleOperation.DefaultBackground });
                }
                else if (param >= 90 && param <= 97) // Bright foreground color
                {
                    StyleUpdate?.Invoke(this, new StyleUpdateEventArgs { StyleOperation = StyleOperation.BrightForegroundColor, Value = param - 90 });
                }
                else if (param >= 100 && param <= 107) // Bright background color
                {
                    StyleUpdate?.Invoke(this, new StyleUpdateEventArgs { StyleOperation = StyleOperation.BrightBackgroundColor, Value = param - 100 });
                }
            }
        }
    }
    
    /// <summary>
    /// Direction of cursor movement
    /// </summary>
    public enum CursorDirection
    {
        Up,
        Down,
        Forward,
        Back,
        Absolute
    }
    
    /// <summary>
    /// Type of erase operation
    /// </summary>
    public enum EraseType
    {
        Display,
        Line
    }
    
    /// <summary>
    /// Style operations that can be performed
    /// </summary>
    public enum StyleOperation
    {
        Reset,
        Bold,
        Italic,
        Underline,
        ForegroundColor,
        BackgroundColor,
        ForegroundColor256,
        BackgroundColor256,
        ForegroundColorRgb,
        BackgroundColorRgb,
        DefaultForeground,
        DefaultBackground,
        BrightForegroundColor,
        BrightBackgroundColor
    }
    
    /// <summary>
    /// Event arguments for cursor update events
    /// </summary>
    public class CursorUpdateEventArgs : EventArgs
    {
        public CursorDirection Direction { get; set; }
        public int Count { get; set; } = 1;
        public int Row { get; set; }
        public int Column { get; set; }
    }
    
    /// <summary>
    /// Event arguments for erase events
    /// </summary>
    public class EraseEventArgs : EventArgs
    {
        public EraseType Type { get; set; }
        public int Mode { get; set; }
    }
    
    /// <summary>
    /// Event arguments for style update events
    /// </summary>
    public class StyleUpdateEventArgs : EventArgs
    {
        public StyleOperation StyleOperation { get; set; }
        public object? Value { get; set; }
    }
}
