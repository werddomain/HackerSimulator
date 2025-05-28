using System.Drawing;

namespace BlazorTerminal.Models
{
    /// <summary>
    /// Represents the styling for terminal text, including foreground/background colors and text attributes
    /// </summary>
    public class TerminalStyle
    {
        /// <summary>
        /// Gets or sets the foreground color
        /// </summary>
        public Color Foreground { get; set; } = Color.White;
        
        /// <summary>
        /// Gets or sets the foreground color as an ANSI color index
        /// </summary>
        public int ForegroundColor {
            get {
                // Try to find the closest ANSI color match
                for (int i = 0; i < 16; i++) {
                    if (Utilities.ColorExtensions.GetAnsiColor(i) == Foreground)
                        return i;
                }
                return TerminalConstants.DefaultForegroundColor;
            }
            set {
                Foreground = Utilities.ColorExtensions.GetAnsiColor(value % 16);
            }
        }
        
        /// <summary>
        /// Gets or sets whether extended (256 color) foreground is used
        /// </summary>
        public bool UseExtendedForeground { get; set; }
        
        /// <summary>
        /// Gets or sets the background color
        /// </summary>
        public Color Background { get; set; } = Color.Black;
        
        /// <summary>
        /// Gets or sets the background color as an ANSI color index
        /// </summary>
        public int BackgroundColor {
            get {
                // Try to find the closest ANSI color match
                for (int i = 0; i < 16; i++) {
                    if (Utilities.ColorExtensions.GetAnsiColor(i) == Background)
                        return i;
                }
                return TerminalConstants.DefaultBackgroundColor;
            }
            set {
                Background = Utilities.ColorExtensions.GetAnsiColor(value % 16);
            }
        }
        
        /// <summary>
        /// Gets or sets whether extended (256 color) background is used
        /// </summary>
        public bool UseExtendedBackground { get; set; }
        
    /// <summary>
    /// Gets or sets whether the text is bold
    /// </summary>
    public bool IsBold { get; set; }
    
    /// <summary>
    /// Gets or sets whether the text is bold (alternate property name)
    /// </summary>
    public bool Bold { 
        get => IsBold; 
        set => IsBold = value; 
    }
    
    /// <summary>
    /// Gets or sets whether the text is italic
    /// </summary>
    public bool IsItalic { get; set; }
    
    /// <summary>
    /// Gets or sets whether the text is italic (alternate property name)
    /// </summary>
    public bool Italic { 
        get => IsItalic; 
        set => IsItalic = value; 
    }
    
    /// <summary>
    /// Gets or sets whether the text is underlined
    /// </summary>
    public bool IsUnderlined { get; set; }
    
    /// <summary>
    /// Gets or sets whether the text is underlined (alternate property name)
    /// </summary>
    public bool Underline { 
        get => IsUnderlined; 
        set => IsUnderlined = value; 
    }
        
        /// <summary>
        /// Gets or sets whether the text is blinking
        /// </summary>
        public bool IsBlinking { get; set; }
        
        /// <summary>
        /// Gets or sets whether foreground and background colors are reversed
        /// </summary>
        public bool IsReversed { get; set; }
        
        /// <summary>
        /// Gets or sets whether the text is crossed out
        /// </summary>
        public bool IsCrossedOut { get; set; }
        
        /// <summary>
        /// Gets or sets whether the text is concealed (hidden)
        /// </summary>
        public bool IsConcealed { get; set; }
        
        /// <summary>
        /// Creates a new instance of the TerminalStyle class with default settings
        /// </summary>
        public TerminalStyle()
        {
        }
        
        /// <summary>
        /// Creates a new instance of the TerminalStyle class with specified colors
        /// </summary>
        /// <param name="foreground">The foreground color</param>
        /// <param name="background">The background color</param>
        public TerminalStyle(Color foreground, Color background)
        {
            Foreground = foreground;
            Background = background;
        }
        
        /// <summary>
        /// Creates a copy of this TerminalStyle
        /// </summary>
        /// <returns>A new TerminalStyle instance with the same properties as this one</returns>
        public TerminalStyle Clone()
        {
            return new TerminalStyle
            {
                Foreground = this.Foreground,
                Background = this.Background,
                IsBold = this.IsBold,
                IsItalic = this.IsItalic,
                IsUnderlined = this.IsUnderlined,
                IsBlinking = this.IsBlinking,
                IsReversed = this.IsReversed,
                IsCrossedOut = this.IsCrossedOut,
                IsConcealed = this.IsConcealed
            };
        }
        
        /// <summary>
        /// Resets all style properties to their default values
        /// </summary>
        public void Reset()
        {
            Foreground = Color.White;
            Background = Color.Black;
            IsBold = false;
            IsItalic = false;
            IsUnderlined = false;
            IsBlinking = false;
            IsReversed = false;
            IsCrossedOut = false;
            IsConcealed = false;
        }
        
        /// <summary>
        /// Applies color from an ANSI color code
        /// </summary>
        /// <param name="code">The ANSI SGR code (30-37, 40-47, 90-97, 100-107)</param>
        public void ApplyAnsiColorCode(int code)
        {
            // Foreground colors (30-37)
            if (code >= 30 && code <= 37)
            {
                Foreground = GetColorFromAnsi(code - 30);
            }
            // Background colors (40-47)
            else if (code >= 40 && code <= 47)
            {
                Background = GetColorFromAnsi(code - 40);
            }
            // Bright foreground colors (90-97)
            else if (code >= 90 && code <= 97)
            {
                Foreground = GetBrightColorFromAnsi(code - 90);
            }
            // Bright background colors (100-107)
            else if (code >= 100 && code <= 107)
            {
                Background = GetBrightColorFromAnsi(code - 100);
            }
            // Reset foreground color
            else if (code == 39)
            {
                Foreground = Color.White;
            }
            // Reset background color
            else if (code == 49)
            {
                Background = Color.Black;
            }
        }
        
        /// <summary>
        /// Gets a standard ANSI color from index (0-7)
        /// </summary>
        /// <param name="colorIndex">Color index (0-7)</param>
        /// <returns>The corresponding color</returns>
        private static Color GetColorFromAnsi(int colorIndex)
        {
            return colorIndex switch
            {
                0 => Color.Black,
                1 => Color.Red,
                2 => Color.Green,
                3 => Color.Yellow,
                4 => Color.Blue,
                5 => Color.Magenta,
                6 => Color.Cyan,
                7 => Color.White,
                _ => Color.White
            };
        }
        
        /// <summary>
        /// Gets a bright ANSI color from index (0-7)
        /// </summary>
        /// <param name="colorIndex">Color index (0-7)</param>
        /// <returns>The corresponding bright color</returns>
        private static Color GetBrightColorFromAnsi(int colorIndex)
        {
            return colorIndex switch
            {
                0 => Color.DarkGray,
                1 => Color.FromArgb(255, 127, 0, 0),  // Bright Red
                2 => Color.FromArgb(255, 0, 255, 0),  // Bright Green
                3 => Color.FromArgb(255, 255, 255, 0), // Bright Yellow
                4 => Color.FromArgb(255, 0, 0, 255),  // Bright Blue
                5 => Color.FromArgb(255, 255, 0, 255), // Bright Magenta
                6 => Color.FromArgb(255, 0, 255, 255), // Bright Cyan
                7 => Color.White,
                _ => Color.White
            };
        }
        
        /// <summary>
        /// Gets the CSS classes that represent this style
        /// </summary>
        /// <returns>A string of CSS class names</returns>
        public string GetCssClasses()
        {
            var classes = new List<string>();
            
            if (IsBold) classes.Add("terminal-bold");
            if (IsItalic) classes.Add("terminal-italic");
            if (IsUnderlined) classes.Add("terminal-underline");
            if (IsBlinking) classes.Add("terminal-blink");
            if (IsCrossedOut) classes.Add("terminal-strikethrough");
            if (IsReversed) classes.Add("terminal-reverse");
            if (IsConcealed) classes.Add("terminal-concealed");
            
            return string.Join(" ", classes);
        }
        
        /// <summary>
        /// Gets the inline CSS style for this terminal style
        /// </summary>
        /// <returns>CSS style string</returns>
        public string GetInlineStyle()
        {
            var styles = new List<string>();
            
            // Apply colors, handling reversed if needed
            var fg = IsReversed ? Background : Foreground;
            var bg = IsReversed ? Foreground : Background;
            
            styles.Add($"color: {ColorToCss(fg)}");
            styles.Add($"background-color: {ColorToCss(bg)}");
            
            // Other styles
            if (IsBold) styles.Add("font-weight: bold");
            if (IsItalic) styles.Add("font-style: italic");
            if (IsUnderlined) styles.Add("text-decoration: underline");
            if (IsCrossedOut) styles.Add("text-decoration: line-through");
            if (IsConcealed) styles.Add("visibility: hidden");
            
            return string.Join("; ", styles);
        }
        
        /// <summary>
        /// Converts a System.Drawing.Color to CSS color string
        /// </summary>
        /// <param name="color">The color to convert</param>
        /// <returns>CSS color string in rgb() or rgba() format</returns>
        private string ColorToCss(Color color)
        {
            if (color.A < 255)
            {
                return $"rgba({color.R}, {color.G}, {color.B}, {color.A / 255.0})";
            }
            
            return $"rgb({color.R}, {color.G}, {color.B})";
        }
    }
}
