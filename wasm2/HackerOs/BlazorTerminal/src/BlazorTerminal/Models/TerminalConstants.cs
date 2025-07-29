namespace BlazorTerminal.Models
{
    /// <summary>
    /// Constants used throughout the terminal implementation
    /// </summary>
    public static class TerminalConstants
    {
        /// <summary>
        /// ANSI escape sequence initiator
        /// </summary>
        public const char ESC = '\u001B';
        
        /// <summary>
        /// Default foreground color (ANSI white)
        /// </summary>
        public const int DefaultForegroundColor = 37;
        
        /// <summary>
        /// Default background color (ANSI black)
        /// </summary>
        public const int DefaultBackgroundColor = 40;

        /// <summary>
        /// ASCII/ANSI control characters
        /// </summary>
        public static class ControlCharacters
        {
            /// <summary>
            /// Null character
            /// </summary>
            public const char NUL = '\u0000';
            
            /// <summary>
            /// Start of Heading
            /// </summary>
            public const char SOH = '\u0001';
            
            /// <summary>
            /// Start of Text
            /// </summary>
            public const char STX = '\u0002';
            
            /// <summary>
            /// End of Text
            /// </summary>
            public const char ETX = '\u0003';
            
            /// <summary>
            /// End of Transmission
            /// </summary>
            public const char EOT = '\u0004';
            
            /// <summary>
            /// Enquiry
            /// </summary>
            public const char ENQ = '\u0005';
            
            /// <summary>
            /// Acknowledge
            /// </summary>
            public const char ACK = '\u0006';
            
            /// <summary>
            /// Bell
            /// </summary>
            public const char BEL = '\u0007';
            
            /// <summary>
            /// Backspace
            /// </summary>
            public const char BS = '\u0008';
            
            /// <summary>
            /// Horizontal Tab
            /// </summary>
            public const char HT = '\u0009';
            
            /// <summary>
            /// Line Feed
            /// </summary>
            public const char LF = '\u000A';
            
            /// <summary>
            /// Vertical Tab
            /// </summary>
            public const char VT = '\u000B';
            
            /// <summary>
            /// Form Feed
            /// </summary>
            public const char FF = '\u000C';
            
            /// <summary>
            /// Carriage Return
            /// </summary>
            public const char CR = '\u000D';
            
            /// <summary>
            /// Shift Out
            /// </summary>
            public const char SO = '\u000E';
            
            /// <summary>
            /// Shift In
            /// </summary>
            public const char SI = '\u000F';
            
            /// <summary>
            /// Data Link Escape
            /// </summary>
            public const char DLE = '\u0010';
            
            /// <summary>
            /// Device Control 1 (XON)
            /// </summary>
            public const char DC1 = '\u0011';
            
            /// <summary>
            /// Device Control 2
            /// </summary>
            public const char DC2 = '\u0012';
            
            /// <summary>
            /// Device Control 3 (XOFF)
            /// </summary>
            public const char DC3 = '\u0013';
            
            /// <summary>
            /// Device Control 4
            /// </summary>
            public const char DC4 = '\u0014';
            
            /// <summary>
            /// Negative Acknowledge
            /// </summary>
            public const char NAK = '\u0015';
            
            /// <summary>
            /// Synchronous Idle
            /// </summary>
            public const char SYN = '\u0016';
            
            /// <summary>
            /// End of Transmission Block
            /// </summary>
            public const char ETB = '\u0017';
            
            /// <summary>
            /// Cancel
            /// </summary>
            public const char CAN = '\u0018';
            
            /// <summary>
            /// End of Medium
            /// </summary>
            public const char EM = '\u0019';
            
            /// <summary>
            /// Substitute
            /// </summary>
            public const char SUB = '\u001A';
            
            /// <summary>
            /// Escape
            /// </summary>
            public const char ESC = '\u001B';
            
            /// <summary>
            /// File Separator
            /// </summary>
            public const char FS = '\u001C';
            
            /// <summary>
            /// Group Separator
            /// </summary>
            public const char GS = '\u001D';
            
            /// <summary>
            /// Record Separator
            /// </summary>
            public const char RS = '\u001E';
            
            /// <summary>
            /// Unit Separator
            /// </summary>
            public const char US = '\u001F';
            
            /// <summary>
            /// Delete
            /// </summary>
            public const char DEL = '\u007F';
        }        /// <summary>
        /// ANSI escape sequences for various terminal operations
        /// </summary>
        public static class AnsiSequences
        {
            /// <summary>
            /// CSI - Control Sequence Introducer
            /// </summary>
            public static readonly string CSI = ESC + "[";
            
            /// <summary>
            /// Reset all attributes
            /// </summary>
            public static readonly string Reset = CSI + "0m";
            
            /// <summary>
            /// Bold/Increased intensity
            /// </summary>
            public static readonly string Bold = CSI + "1m";
            
            /// <summary>
            /// Faint/Decreased intensity
            /// </summary>
            public static readonly string Faint = CSI + "2m";
            
            /// <summary>
            /// Italic
            /// </summary>
            public static readonly string Italic = CSI + "3m";
            
            /// <summary>
            /// Underline
            /// </summary>
            public static readonly string Underline = CSI + "4m";
            
            /// <summary>
            /// Slow blink
            /// </summary>
            public static readonly string SlowBlink = CSI + "5m";
            
            /// <summary>
            /// Rapid blink
            /// </summary>
            public static readonly string RapidBlink = CSI + "6m";
            
            /// <summary>
            /// Reverse video (swap foreground and background)
            /// </summary>
            public static readonly string Reverse = CSI + "7m";
            
            /// <summary>
            /// Conceal/Hide
            /// </summary>
            public static readonly string Conceal = CSI + "8m";
            
            /// <summary>
            /// Crossed-out/Strike
            /// </summary>
            public static readonly string CrossedOut = CSI + "9m";
            
            /// <summary>
            /// Primary font (default)
            /// </summary>
            public static readonly string PrimaryFont = CSI + "10m";
            
            /// <summary>
            /// Clear all formatting
            /// </summary>
            public static readonly string NormalIntensity = CSI + "22m";
            
            /// <summary>
            /// Not italic
            /// </summary>
            public static readonly string NotItalic = CSI + "23m";
            
            /// <summary>
            /// Not underlined
            /// </summary>
            public static readonly string NotUnderlined = CSI + "24m";
            
            /// <summary>
            /// Not blinking
            /// </summary>
            public static readonly string NotBlinking = CSI + "25m";
            
            /// <summary>
            /// Not reversed
            /// </summary>
            public static readonly string NotReversed = CSI + "27m";
            
            /// <summary>
            /// Not concealed
            /// </summary>
            public static readonly string NotConcealed = CSI + "28m";
            
            /// <summary>
            /// Not crossed out
            /// </summary>
            public static readonly string NotCrossedOut = CSI + "29m";
            
            /// <summary>
            /// Clear screen
            /// </summary>
            public static readonly string ClearScreen = CSI + "2J";
            
            /// <summary>
            /// Clear screen from cursor to end
            /// </summary>
            public static readonly string ClearScreenFromCursor = CSI + "0J";
            
            /// <summary>
            /// Clear screen from beginning to cursor
            /// </summary>
            public static readonly string ClearScreenToCursor = CSI + "1J";
            
            /// <summary>
            /// Clear line
            /// </summary>
            public static readonly string ClearLine = CSI + "2K";
            
            /// <summary>
            /// Clear line from cursor to end
            /// </summary>
            public static readonly string ClearLineFromCursor = CSI + "0K";
            
            /// <summary>
            /// Clear line from beginning to cursor
            /// </summary>
            public static readonly string ClearLineToCursor = CSI + "1K";
            
            /// <summary>
            /// Move cursor to home position (0,0)
            /// </summary>
            public static readonly string CursorHome = CSI + "H";
            
            /// <summary>
            /// Save cursor position
            /// </summary>
            public static readonly string SaveCursorPosition = CSI + "s";
            
            /// <summary>
            /// Restore cursor position
            /// </summary>
            public static readonly string RestoreCursorPosition = CSI + "u";
            
            /// <summary>
            /// Hide cursor
            /// </summary>
            public static readonly string HideCursor = CSI + "?25l";
            
            /// <summary>
            /// Show cursor
            /// </summary>
            public static readonly string ShowCursor = CSI + "?25h";
        }
          /// <summary>
        /// ANSI color codes
        /// </summary>
        public static class AnsiColors
        {
            // Standard foreground colors
            /// <summary>
            /// Black foreground color (ANSI code 30)
            /// </summary>
            public const int FgBlack = 30;
            /// <summary>
            /// Red foreground color (ANSI code 31)
            /// </summary>
            public const int FgRed = 31;
            /// <summary>
            /// Green foreground color (ANSI code 32)
            /// </summary>
            public const int FgGreen = 32;
            /// <summary>
            /// Yellow foreground color (ANSI code 33)
            /// </summary>
            public const int FgYellow = 33;
            /// <summary>
            /// Blue foreground color (ANSI code 34)
            /// </summary>
            public const int FgBlue = 34;
            /// <summary>
            /// Magenta foreground color (ANSI code 35)
            /// </summary>
            public const int FgMagenta = 35;
            /// <summary>
            /// Cyan foreground color (ANSI code 36)
            /// </summary>
            public const int FgCyan = 36;
            /// <summary>
            /// White foreground color (ANSI code 37)
            /// </summary>
            public const int FgWhite = 37;
            /// <summary>
            /// Default foreground color (ANSI code 39)
            /// </summary>
            public const int FgDefault = 39;
            
            // Bright foreground colors
            /// <summary>
            /// Bright black foreground color (ANSI code 90)
            /// </summary>
            public const int FgBrightBlack = 90;
            /// <summary>
            /// Bright red foreground color (ANSI code 91)
            /// </summary>
            public const int FgBrightRed = 91;
            /// <summary>
            /// Bright green foreground color (ANSI code 92)
            /// </summary>
            public const int FgBrightGreen = 92;
            /// <summary>
            /// Bright yellow foreground color (ANSI code 93)
            /// </summary>
            public const int FgBrightYellow = 93;
            /// <summary>
            /// Bright blue foreground color (ANSI code 94)
            /// </summary>
            public const int FgBrightBlue = 94;
            /// <summary>
            /// Bright magenta foreground color (ANSI code 95)
            /// </summary>
            public const int FgBrightMagenta = 95;
            /// <summary>
            /// Bright cyan foreground color (ANSI code 96)
            /// </summary>
            public const int FgBrightCyan = 96;
            /// <summary>
            /// Bright white foreground color (ANSI code 97)
            /// </summary>
            public const int FgBrightWhite = 97;
            
            // Standard background colors
            /// <summary>
            /// Black background color (ANSI code 40)
            /// </summary>
            public const int BgBlack = 40;
            /// <summary>
            /// Red background color (ANSI code 41)
            /// </summary>
            public const int BgRed = 41;
            /// <summary>
            /// Green background color (ANSI code 42)
            /// </summary>
            public const int BgGreen = 42;
            /// <summary>
            /// Yellow background color (ANSI code 43)
            /// </summary>
            public const int BgYellow = 43;
            /// <summary>
            /// Blue background color (ANSI code 44)
            /// </summary>
            public const int BgBlue = 44;
            /// <summary>
            /// Magenta background color (ANSI code 45)
            /// </summary>
            public const int BgMagenta = 45;
            /// <summary>
            /// Cyan background color (ANSI code 46)
            /// </summary>
            public const int BgCyan = 46;
            /// <summary>
            /// White background color (ANSI code 47)
            /// </summary>
            public const int BgWhite = 47;
            /// <summary>
            /// Default background color (ANSI code 49)
            /// </summary>
            public const int BgDefault = 49;
            
            // Bright background colors
            /// <summary>
            /// Bright black background color (ANSI code 100)
            /// </summary>
            public const int BgBrightBlack = 100;
            /// <summary>
            /// Bright red background color (ANSI code 101)
            /// </summary>
            public const int BgBrightRed = 101;
            /// <summary>
            /// Bright green background color (ANSI code 102)
            /// </summary>
            public const int BgBrightGreen = 102;
            /// <summary>
            /// Bright yellow background color (ANSI code 103)
            /// </summary>
            public const int BgBrightYellow = 103;
            /// <summary>
            /// Bright blue background color (ANSI code 104)
            /// </summary>
            public const int BgBrightBlue = 104;
            /// <summary>
            /// Bright magenta background color (ANSI code 105)
            /// </summary>
            public const int BgBrightMagenta = 105;
            /// <summary>
            /// Bright cyan background color (ANSI code 106)
            /// </summary>
            public const int BgBrightCyan = 106;
            /// <summary>
            /// Bright white background color (ANSI code 107)
            /// </summary>
            public const int BgBrightWhite = 107;
        }
    }

    /// <summary>
    /// Defines the type of cursor to display in the terminal
    /// </summary>
    public enum CursorStyle
    {
        /// <summary>
        /// Full block cursor
        /// </summary>
        Block,
        
        /// <summary>
        /// Underline cursor
        /// </summary>
        Underline,
        
        /// <summary>
        /// Vertical bar cursor
        /// </summary>
        Bar
    }
}
