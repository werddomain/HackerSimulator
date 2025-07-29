using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.Theme
{
    /// <summary>
    /// Interface for managing themes in HackerOS
    /// </summary>
    public interface IThemeManager
    {
        /// <summary>
        /// Event raised when the theme changes
        /// </summary>
        event EventHandler ThemeChanged;

        /// <summary>
        /// Gets the current theme
        /// </summary>
        ITheme CurrentTheme { get; }

        /// <summary>
        /// Gets all available themes
        /// </summary>
        IEnumerable<ITheme> AvailableThemes { get; }

        /// <summary>
        /// Applies a theme by name
        /// </summary>
        /// <param name="themeName">Name of the theme to apply</param>
        /// <returns>True if theme was applied successfully</returns>
        Task<bool> ApplyThemeAsync(string themeName);

        /// <summary>
        /// Loads a theme from a theme file
        /// </summary>
        /// <param name="themeData">Theme configuration data</param>
        /// <returns>The loaded theme</returns>
        Task<ITheme> LoadThemeAsync(string themeData);

        /// <summary>
        /// Gets the CSS variables for the current theme
        /// </summary>
        /// <returns>CSS variables as a dictionary</returns>
        Dictionary<string, string> GetThemeCssVariables();

        /// <summary>
        /// Registers a new theme
        /// </summary>
        /// <param name="theme">Theme to register</param>
        void RegisterTheme(ITheme theme);
    }
}
