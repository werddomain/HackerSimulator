
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Main interface for HackerOS configuration management service.
    /// Provides access to system, user, and application settings following Linux conventions.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets a specific setting value from configuration files.
        /// </summary>
        /// <typeparam name="T">The type to convert the setting value to</typeparam>
        /// <param name="section">The configuration section name</param>
        /// <param name="key">The setting key within the section</param>
        /// <param name="defaultValue">The default value to return if setting is not found</param>
        /// <param name="scope">The scope to search in (defaults to User)</param>
        /// <returns>The setting value or default if not found</returns>
        Task<T?> GetSettingAsync<T>(string section, string key, T? defaultValue = default, SettingScope scope = SettingScope.User);

        /// <summary>
        /// Sets a setting value in the appropriate configuration file.
        /// </summary>
        /// <typeparam name="T">The type of the setting value</typeparam>
        /// <param name="section">The configuration section name</param>
        /// <param name="key">The setting key within the section</param>
        /// <param name="value">The value to set</param>
        /// <param name="scope">The scope to set the value in (defaults to User)</param>
        /// <returns>True if the setting was successfully saved</returns>
        Task<bool> SetSettingAsync<T>(string section, string key, T value, SettingScope scope = SettingScope.User);

        /// <summary>
        /// Gets all settings within a specific section.
        /// </summary>
        /// <param name="section">The configuration section name</param>
        /// <param name="scope">The scope to search in (defaults to User)</param>
        /// <returns>Dictionary of key-value pairs for the section</returns>
        Task<Dictionary<string, object>> GetSectionAsync(string section, SettingScope scope = SettingScope.User);

        /// <summary>
        /// Gets an effective setting value considering the hierarchy (system → user → app).
        /// </summary>
        /// <typeparam name="T">The type to convert the setting value to</typeparam>
        /// <param name="section">The configuration section name</param>
        /// <param name="key">The setting key within the section</param>
        /// <param name="maxScope">The maximum scope to search up to (defaults to User)</param>
        /// <param name="defaultValue">The default value if not found in any scope</param>
        /// <returns>The effective setting value from the highest priority scope</returns>
        Task<T?> GetEffectiveSettingAsync<T>(string section, string key, SettingScope maxScope = SettingScope.User, T? defaultValue = default);

        /// <summary>
        /// Loads a specific configuration file into memory.
        /// </summary>
        /// <param name="configPath">The virtual file system path to the configuration file</param>
        /// <returns>True if the file was successfully loaded</returns>
        Task<bool> LoadConfigFileAsync(string configPath);

        /// <summary>
        /// Saves a specific configuration file from memory to storage.
        /// </summary>
        /// <param name="configPath">The virtual file system path to save the configuration file</param>
        /// <returns>True if the file was successfully saved</returns>
        Task<bool> SaveConfigFileAsync(string configPath);

        /// <summary>
        /// Reloads all configuration files from storage.
        /// </summary>
        /// <returns>True if all files were successfully reloaded</returns>
        Task<bool> ReloadAllConfigsAsync();

        /// <summary>
        /// Validates the syntax and content of a configuration file.
        /// </summary>
        /// <param name="configPath">The virtual file system path to the configuration file</param>
        /// <returns>True if the configuration file is valid</returns>
        Task<bool> ValidateConfigAsync(string configPath);

        /// <summary>
        /// Gets the list of available configuration files for a specific scope.
        /// </summary>
        /// <param name="scope">The scope to search for configuration files</param>
        /// <returns>List of configuration file paths</returns>
        Task<IEnumerable<string>> GetConfigFilesAsync(SettingScope scope);

        /// <summary>
        /// Creates default configuration files if they don't exist.
        /// </summary>
        /// <param name="scope">The scope to create default configurations for</param>
        /// <returns>True if default configurations were created successfully</returns>
        Task<bool> CreateDefaultConfigsAsync(SettingScope scope);

        /// <summary>
        /// Event raised when any configuration file changes.
        /// </summary>
        event EventHandler<ConfigurationChangedEventArgs> OnConfigurationChanged;
    }
}
