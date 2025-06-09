using System;
using HackerOs.OS.Theme;

namespace HackerOs.OS.UI.Components
{
    /// <summary>
    /// Provides data for theme changed events.
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the previous theme.
        /// </summary>
        public ITheme? PreviousTheme { get; }

        /// <summary>
        /// Gets the new current theme.
        /// </summary>
        public ITheme NewTheme { get; }

        /// <summary>
        /// Gets the name of the theme that was applied.
        /// </summary>
        public string ThemeName { get; }

        /// <summary>
        /// Initializes a new instance of the ThemeChangedEventArgs class.
        /// </summary>
        public ThemeChangedEventArgs(ITheme newTheme, string themeName, ITheme? previousTheme = null)
        {
            NewTheme = newTheme ?? throw new ArgumentNullException(nameof(newTheme));
            ThemeName = themeName ?? throw new ArgumentNullException(nameof(themeName));
            PreviousTheme = previousTheme;
        }
    }
}
