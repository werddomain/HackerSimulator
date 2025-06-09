using System.Collections.Generic;

namespace HackerOs.OS.Theme
{
    /// <summary>
    /// Classic green-on-black hacker theme
    /// </summary>
    public class HackerTheme : ITheme
    {
        public string Name => "Classic Hacker";
        public string Description => "Traditional green-on-black terminal aesthetic";
        public string Author => "HackerOS";
        public string Version => "1.0.0";

        public ThemeColorScheme ColorScheme { get; }
        public ThemeWindowStyle WindowStyle { get; }
        public ThemeDesktopStyle DesktopStyle { get; }
        public ThemeTerminalStyle TerminalStyle { get; }
        public Dictionary<string, string> CssVariables { get; }
        public string CustomCss { get; }

        public HackerTheme()
        {
            ColorScheme = new ThemeColorScheme
            {
                Primary = "#00ff00",
                Secondary = "#008000",
                Background = "#000000",
                Surface = "#1a1a1a",
                Text = "#00ff00",
                TextSecondary = "#008000",
                Border = "#00ff00",
                Accent = "#ffff00",
                Warning = "#ff8800",
                Error = "#ff0000",
                Success = "#00ff00"
            };

            WindowStyle = new ThemeWindowStyle
            {
                TitleBarBackground = "#1a1a1a",
                TitleBarText = "#00ff00",
                WindowBackground = "#000000",
                WindowBorder = "#00ff00",
                WindowShadow = "0 4px 8px rgba(0, 255, 0, 0.3)",
                BorderRadius = 4,
                BorderWidth = 1
            };

            DesktopStyle = new ThemeDesktopStyle
            {
                BackgroundColor = "#000000",
                IconColor = "#00ff00",
                IconHoverColor = "#ffff00",
                TaskbarBackground = "#1a1a1a",
                TaskbarBorder = "#00ff00",
                IconSize = 48
            };

            TerminalStyle = new ThemeTerminalStyle
            {
                Background = "#000000",
                Text = "#00ff00",
                Cursor = "#00ff00",
                Selection = "rgba(0, 255, 0, 0.3)",
                FontFamily = "Consolas, 'Courier New', monospace",
                FontSize = 14,
                LineHeight = 1.2
            };

            CssVariables = new Dictionary<string, string>
            {
                ["--font-family-mono"] = "Consolas, 'Courier New', monospace",
                ["--font-family-ui"] = "Arial, sans-serif",
                ["--border-radius"] = "4px",
                ["--transition-duration"] = "0.2s"
            };

            CustomCss = @"
                .hacker-glow {
                    text-shadow: 0 0 5px currentColor;
                }
                
                .hacker-border {
                    border: 1px solid var(--color-primary);
                    box-shadow: 0 0 10px rgba(0, 255, 0, 0.3);
                }
            ";
        }
    }

    /// <summary>
    /// Matrix-inspired theme with cascading characters
    /// </summary>
    public class MatrixTheme : ITheme
    {
        public string Name => "Matrix";
        public string Description => "Matrix movie inspired theme with digital rain";
        public string Author => "HackerOS";
        public string Version => "1.0.0";

        public ThemeColorScheme ColorScheme { get; }
        public ThemeWindowStyle WindowStyle { get; }
        public ThemeDesktopStyle DesktopStyle { get; }
        public ThemeTerminalStyle TerminalStyle { get; }
        public Dictionary<string, string> CssVariables { get; }
        public string CustomCss { get; }

        public MatrixTheme()
        {
            ColorScheme = new ThemeColorScheme
            {
                Primary = "#00ff41",
                Secondary = "#008f11",
                Background = "#0d1117",
                Surface = "#21262d",
                Text = "#00ff41",
                TextSecondary = "#008f11",
                Border = "#00ff41",
                Accent = "#ffffff",
                Warning = "#f85149",
                Error = "#ff6b6b",
                Success = "#00ff41"
            };

            WindowStyle = new ThemeWindowStyle
            {
                TitleBarBackground = "#21262d",
                TitleBarText = "#00ff41",
                WindowBackground = "#0d1117",
                WindowBorder = "#00ff41",
                WindowShadow = "0 4px 12px rgba(0, 255, 65, 0.4)",
                BorderRadius = 6,
                BorderWidth = 1
            };

            DesktopStyle = new ThemeDesktopStyle
            {
                BackgroundColor = "#0d1117",
                IconColor = "#00ff41",
                IconHoverColor = "#ffffff",
                TaskbarBackground = "#21262d",
                TaskbarBorder = "#00ff41",
                IconSize = 48
            };

            TerminalStyle = new ThemeTerminalStyle
            {
                Background = "#0d1117",
                Text = "#00ff41",
                Cursor = "#00ff41",
                Selection = "rgba(0, 255, 65, 0.3)",
                FontFamily = "'Fira Code', Consolas, monospace",
                FontSize = 14,
                LineHeight = 1.3
            };

            CssVariables = new Dictionary<string, string>
            {
                ["--font-family-mono"] = "'Fira Code', Consolas, monospace",
                ["--font-family-ui"] = "'Segoe UI', Arial, sans-serif",
                ["--border-radius"] = "6px",
                ["--transition-duration"] = "0.3s"
            };

            CustomCss = @"
                .matrix-glow {
                    text-shadow: 0 0 8px currentColor, 0 0 15px currentColor;
                }
                
                .matrix-border {
                    border: 1px solid var(--color-primary);
                    box-shadow: 0 0 15px rgba(0, 255, 65, 0.4);
                }
            ";
        }
    }

    /// <summary>
    /// Cyberpunk neon theme
    /// </summary>
    public class CyberpunkTheme : ITheme
    {
        public string Name => "Cyberpunk";
        public string Description => "Neon cyberpunk aesthetic with purple and pink accents";
        public string Author => "HackerOS";
        public string Version => "1.0.0";

        public ThemeColorScheme ColorScheme { get; }
        public ThemeWindowStyle WindowStyle { get; }
        public ThemeDesktopStyle DesktopStyle { get; }
        public ThemeTerminalStyle TerminalStyle { get; }
        public Dictionary<string, string> CssVariables { get; }
        public string CustomCss { get; }

        public CyberpunkTheme()
        {
            ColorScheme = new ThemeColorScheme
            {
                Primary = "#ff00ff",
                Secondary = "#8b00ff",
                Background = "#0a0a0a",
                Surface = "#1a0d1a",
                Text = "#ff00ff",
                TextSecondary = "#bf00bf",
                Border = "#ff00ff",
                Accent = "#00ffff",
                Warning = "#ffff00",
                Error = "#ff4444",
                Success = "#00ff88"
            };

            WindowStyle = new ThemeWindowStyle
            {
                TitleBarBackground = "#1a0d1a",
                TitleBarText = "#ff00ff",
                WindowBackground = "#0a0a0a",
                WindowBorder = "#ff00ff",
                WindowShadow = "0 4px 15px rgba(255, 0, 255, 0.5)",
                BorderRadius = 8,
                BorderWidth = 2
            };

            DesktopStyle = new ThemeDesktopStyle
            {
                BackgroundColor = "#0a0a0a",
                IconColor = "#ff00ff",
                IconHoverColor = "#00ffff",
                TaskbarBackground = "#1a0d1a",
                TaskbarBorder = "#ff00ff",
                IconSize = 52
            };

            TerminalStyle = new ThemeTerminalStyle
            {
                Background = "#0a0a0a",
                Text = "#ff00ff",
                Cursor = "#00ffff",
                Selection = "rgba(255, 0, 255, 0.3)",
                FontFamily = "'JetBrains Mono', Consolas, monospace",
                FontSize = 15,
                LineHeight = 1.4
            };

            CssVariables = new Dictionary<string, string>
            {
                ["--font-family-mono"] = "'JetBrains Mono', Consolas, monospace",
                ["--font-family-ui"] = "'Roboto', Arial, sans-serif",
                ["--border-radius"] = "8px",
                ["--transition-duration"] = "0.4s"
            };

            CustomCss = @"
                .cyberpunk-glow {
                    text-shadow: 0 0 10px currentColor, 0 0 20px currentColor, 0 0 30px currentColor;
                }
                
                .cyberpunk-border {
                    border: 2px solid var(--color-primary);
                    box-shadow: 0 0 20px rgba(255, 0, 255, 0.5), inset 0 0 20px rgba(255, 0, 255, 0.1);
                }
            ";
        }
    }
}
