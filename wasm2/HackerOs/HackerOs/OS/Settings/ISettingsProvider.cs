using System.Collections.Generic;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Interface for settings providers that can retrieve settings from specific scopes.
    /// Used by the inheritance manager to access settings across different scopes.
    /// </summary>
    public interface ISettingsProvider
    {
        /// <summary>
        /// Checks if a setting value exists.
        /// </summary>
        /// <param name="section">The configuration section name</param>
        /// <param name="key">The setting key within the section (optional for section-only checks)</param>
        /// <returns>True if the setting exists</returns>
        bool HasValue(string section, string? key = null);

        /// <summary>
        /// Gets a setting value from this provider.
        /// </summary>
        /// <typeparam name="T">The type to convert the setting value to</typeparam>
        /// <param name="section">The configuration section name</param>
        /// <param name="key">The setting key within the section (optional for section retrieval)</param>
        /// <param name="defaultValue">The default value to return if setting is not found</param>
        /// <returns>The setting value or default if not found</returns>
        T GetValue<T>(string section, string? key = null, T defaultValue = default!);

        /// <summary>
        /// Gets all settings from this provider.
        /// </summary>
        /// <param name="section">The configuration section name (optional for all sections)</param>
        /// <returns>Dictionary of all settings</returns>
        Dictionary<string, object> GetAllSettings(string? section = null);
    }
}
