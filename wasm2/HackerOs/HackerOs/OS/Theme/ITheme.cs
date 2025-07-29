using System.Collections.Generic;

namespace HackerOs.OS.Theme
{
    /// <summary>
    /// Interface representing a HackerOS theme
    /// </summary>
    public interface ITheme
    {
        /// <summary>
        /// Name of the theme
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of the theme
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Author of the theme
        /// </summary>
        string Author { get; }

        /// <summary>
        /// Version of the theme
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Primary color scheme
        /// </summary>
        ThemeColorScheme ColorScheme { get; }

        /// <summary>
        /// Window styling properties
        /// </summary>
        ThemeWindowStyle WindowStyle { get; }

        /// <summary>
        /// Desktop styling properties
        /// </summary>
        ThemeDesktopStyle DesktopStyle { get; }

        /// <summary>
        /// Terminal styling properties
        /// </summary>
        ThemeTerminalStyle TerminalStyle { get; }

        /// <summary>
        /// CSS variables for the theme
        /// </summary>
        Dictionary<string, string> CssVariables { get; }

        /// <summary>
        /// Custom CSS rules
        /// </summary>
        string CustomCss { get; }
    }

    /// <summary>
    /// Color scheme for a theme
    /// </summary>
    public class ThemeColorScheme
    {
        public string Primary { get; set; } = "#00ff00";
        public string Secondary { get; set; } = "#008000";
        public string Background { get; set; } = "#000000";
        public string Surface { get; set; } = "#1a1a1a";
        public string Text { get; set; } = "#00ff00";
        public string TextSecondary { get; set; } = "#008000";
        public string Border { get; set; } = "#00ff00";
        public string Accent { get; set; } = "#ffff00";
        public string Warning { get; set; } = "#ff8800";
        public string Error { get; set; } = "#ff0000";
        public string Success { get; set; } = "#00ff00";
    }

    /// <summary>
    /// Window styling properties
    /// </summary>
    public class ThemeWindowStyle
    {
        public string TitleBarBackground { get; set; } = "#1a1a1a";
        public string TitleBarText { get; set; } = "#00ff00";
        public string WindowBackground { get; set; } = "#000000";
        public string WindowBorder { get; set; } = "#00ff00";
        public string WindowShadow { get; set; } = "0 4px 8px rgba(0, 255, 0, 0.3)";
        public int BorderRadius { get; set; } = 4;
        public int BorderWidth { get; set; } = 1;
    }

    /// <summary>
    /// Desktop styling properties
    /// </summary>
    public class ThemeDesktopStyle
    {
        public string BackgroundImage { get; set; } = "";
        public string BackgroundColor { get; set; } = "#000000";
        public string IconColor { get; set; } = "#00ff00";
        public string IconHoverColor { get; set; } = "#ffff00";
        public string TaskbarBackground { get; set; } = "#1a1a1a";
        public string TaskbarBorder { get; set; } = "#00ff00";
        public int IconSize { get; set; } = 48;
    }

    /// <summary>
    /// Terminal styling properties
    /// </summary>
    public class ThemeTerminalStyle
    {
        public string Background { get; set; } = "#000000";
        public string Text { get; set; } = "#00ff00";
        public string Cursor { get; set; } = "#00ff00";
        public string Selection { get; set; } = "rgba(0, 255, 0, 0.3)";
        public string FontFamily { get; set; } = "Consolas, 'Courier New', monospace";
        public int FontSize { get; set; } = 14;
        public double LineHeight { get; set; } = 1.2;
    }
}
