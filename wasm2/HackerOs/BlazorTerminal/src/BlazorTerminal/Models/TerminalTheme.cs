using System;
using System.Collections.Generic;
using System.Drawing;

namespace BlazorTerminal.Models
{
    /// <summary>
    /// Configuration for terminal appearance
    /// </summary>
    public class TerminalTheme
    {
        // Default ANSI colors (0-15)
        private readonly string[] _defaultColors = new string[16]
        {
            "#000000", // Black
            "#CD3131", // Red
            "#0DBC79", // Green
            "#E5E510", // Yellow
            "#2472C8", // Blue
            "#BC3FBC", // Magenta
            "#11A8CD", // Cyan
            "#E5E5E5", // White
            "#666666", // Bright Black
            "#F14C4C", // Bright Red
            "#23D18B", // Bright Green
            "#F5F543", // Bright Yellow
            "#3B8EEA", // Bright Blue
            "#D670D6", // Bright Magenta
            "#29B8DB", // Bright Cyan
            "#FFFFFF"  // Bright White
        };

        // Theme properties
        
        /// <summary>
        /// Font family for the terminal
        /// </summary>
        public string FontFamily { get; set; } = "Consolas, 'Courier New', monospace";
        
        /// <summary>
        /// Font size in pixels
        /// </summary>
        public int FontSize { get; set; } = 14;
        
        /// <summary>
        /// Line height multiplier
        /// </summary>
        public float LineHeight { get; set; } = 1.2f;
        
        /// <summary>
        /// Terminal background color
        /// </summary>
        public string Background { get; set; } = "#1E1E1E";
        
        /// <summary>
        /// Default foreground text color
        /// </summary>
        public string Foreground { get; set; } = "#CCCCCC";
        
        /// <summary>
        /// Cursor color
        /// </summary>
        public string CursorColor { get; set; } = "#FFFFFF";
        
        /// <summary>
        /// Selection background color
        /// </summary>
        public string SelectionBackground { get; set; } = "rgba(255, 255, 255, 0.3)";
        
        /// <summary>
        /// Whether to use bright colors when text is bold
        /// </summary>
        public bool BoldAsBright { get; set; } = true;
        
        /// <summary>
        /// The cursor style to use
        /// </summary>
        public CursorStyle CursorStyle { get; set; } = CursorStyle.Block;
        
        /// <summary>
        /// Whether the cursor should blink
        /// </summary>
        public bool CursorBlink { get; set; } = true;
        
        /// <summary>
        /// Cursor blink interval in milliseconds
        /// </summary>
        public int CursorBlinkInterval { get; set; } = 500;
        
        /// <summary>
        /// The ANSI color palette (0-15)
        /// </summary>
        public string[] Colors { get; }
        
        /// <summary>
        /// Creates a new terminal theme with default values
        /// </summary>
        public TerminalTheme()
        {
            Colors = new string[16];
            Array.Copy(_defaultColors, Colors, 16);
        }
        
        /// <summary>
        /// Creates a deep copy of this theme
        /// </summary>
        /// <returns>A new instance with the same properties</returns>
        public TerminalTheme Clone()
        {
            var theme = new TerminalTheme
            {
                FontFamily = FontFamily,
                FontSize = FontSize,
                LineHeight = LineHeight,
                Background = Background,
                Foreground = Foreground,
                CursorColor = CursorColor,
                SelectionBackground = SelectionBackground,
                BoldAsBright = BoldAsBright,
                CursorStyle = CursorStyle,
                CursorBlink = CursorBlink,
                CursorBlinkInterval = CursorBlinkInterval
            };
            
            Array.Copy(Colors, theme.Colors, Colors.Length);
            
            return theme;
        }
        
        /// <summary>
        /// Gets a dark theme
        /// </summary>
        public static TerminalTheme Dark()
        {
            return new TerminalTheme
            {
                Background = "#1E1E1E",
                Foreground = "#CCCCCC"
            };
        }
        
        /// <summary>
        /// Gets a light theme
        /// </summary>
        public static TerminalTheme Light()
        {
            var theme = new TerminalTheme
            {
                Background = "#FFFFFF",
                Foreground = "#333333",
                CursorColor = "#000000",
                SelectionBackground = "rgba(0, 0, 0, 0.3)"
            };
            
            // Adjust colors for light theme
            theme.Colors[0] = "#000000"; // Black
            theme.Colors[7] = "#666666"; // White
            theme.Colors[8] = "#777777"; // Bright Black
            theme.Colors[15] = "#000000"; // Bright White
            
            return theme;
        }
        
        /// <summary>
        /// Gets a "retro" green theme
        /// </summary>
        public static TerminalTheme Retro()
        {
            var theme = new TerminalTheme
            {
                Background = "#001100",
                Foreground = "#00FF00",
                CursorColor = "#00FF00",
                FontFamily = "'VT323', 'Px437 IBM VGA8', monospace",
                SelectionBackground = "rgba(0, 255, 0, 0.3)"
            };
            
            // Set all colors to shades of green
            for (int i = 0; i < theme.Colors.Length; i++)
            {
                switch (i % 8)
                {
                    case 0: theme.Colors[i] = "#001100"; break; // Black
                    case 1: theme.Colors[i] = "#005500"; break; // Red
                    case 2: theme.Colors[i] = "#00AA00"; break; // Green
                    case 3: theme.Colors[i] = "#00FF00"; break; // Yellow
                    case 4: theme.Colors[i] = "#007700"; break; // Blue
                    case 5: theme.Colors[i] = "#00AA00"; break; // Magenta
                    case 6: theme.Colors[i] = "#00CC00"; break; // Cyan
                    case 7: theme.Colors[i] = "#00FF00"; break; // White
                }
                
                // Bright variants are brighter
                if (i >= 8)
                {
                    theme.Colors[i] = theme.Colors[i];
                }
            }
            
            return theme;
        }
    }
}
