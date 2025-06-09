using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.Core.Settings
{
    /// <summary>
    /// Interface for managing system settings
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Event raised when a setting changes
        /// </summary>
        event EventHandler<SettingChangedEventArgs> SettingChanged;

        /// <summary>
        /// Gets a setting value by key
        /// </summary>
        /// <typeparam name="T">Type of the setting value</typeparam>
        /// <param name="key">Setting key</param>
        /// <param name="defaultValue">Default value if setting doesn't exist</param>
        /// <returns>The setting value</returns>
        Task<T> GetSettingAsync<T>(string key, T defaultValue = default!);

        /// <summary>
        /// Sets a setting value
        /// </summary>
        /// <typeparam name="T">Type of the setting value</typeparam>
        /// <param name="key">Setting key</param>
        /// <param name="value">Setting value</param>
        Task SetSettingAsync<T>(string key, T value);

        /// <summary>
        /// Removes a setting
        /// </summary>
        /// <param name="key">Setting key</param>
        Task RemoveSettingAsync(string key);

        /// <summary>
        /// Checks if a setting exists
        /// </summary>
        /// <param name="key">Setting key</param>
        /// <returns>True if setting exists</returns>
        Task<bool> HasSettingAsync(string key);

        /// <summary>
        /// Gets all settings in a category
        /// </summary>
        /// <param name="category">Setting category prefix</param>
        /// <returns>Dictionary of settings</returns>
        Task<Dictionary<string, object>> GetCategoryAsync(string category);

        /// <summary>
        /// Clears all settings in a category
        /// </summary>
        /// <param name="category">Setting category prefix</param>
        Task ClearCategoryAsync(string category);

        /// <summary>
        /// Saves all pending changes to storage
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// Loads settings from storage
        /// </summary>
        Task LoadAsync();

        /// <summary>
        /// Resets all settings to defaults
        /// </summary>
        Task ResetToDefaultsAsync();
    }

    /// <summary>
    /// Event arguments for setting changed events
    /// </summary>
    public class SettingChangedEventArgs : EventArgs
    {
        public string Key { get; }
        public object? OldValue { get; }
        public object? NewValue { get; }

        public SettingChangedEventArgs(string key, object? oldValue, object? newValue)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
