
using System;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Event arguments for configuration change notifications.
    /// Used to notify components when configuration files are modified.
    /// </summary>
    public class ConfigurationChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the path of the configuration file that changed.
        /// </summary>
        public string ConfigPath { get; }

        /// <summary>
        /// Gets the section that was modified in the configuration file.
        /// </summary>
        public string Section { get; }

        /// <summary>
        /// Gets the specific key that was changed, if known.
        /// </summary>
        public string? Key { get; }

        /// <summary>
        /// Gets the scope of the configuration change.
        /// </summary>
        public SettingScope Scope { get; }

        /// <summary>
        /// Gets the type of change that occurred.
        /// </summary>
        public ConfigurationChangeType ChangeType { get; }

        /// <summary>
        /// Initializes a new instance of the ConfigurationChangedEventArgs class.
        /// </summary>
        /// <param name="configPath">The path of the configuration file that changed</param>
        /// <param name="section">The section that was modified</param>
        /// <param name="key">The specific key that was changed (optional)</param>
        /// <param name="scope">The scope of the configuration change</param>
        /// <param name="changeType">The type of change that occurred</param>
        public ConfigurationChangedEventArgs(
            string configPath, 
            string section, 
            string? key, 
            SettingScope scope, 
            ConfigurationChangeType changeType)
        {
            ConfigPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
            Section = section ?? throw new ArgumentNullException(nameof(section));
            Key = key;
            Scope = scope;
            ChangeType = changeType;
        }
    }    /// <summary>
    /// Defines the types of configuration changes that can occur.
    /// </summary>
    public enum ConfigurationChangeType
    {
        /// <summary>
        /// A setting value was modified.
        /// </summary>
        Modified,

        /// <summary>
        /// A new setting was added.
        /// </summary>
        Added,

        /// <summary>
        /// A setting was removed.
        /// </summary>
        Removed,

        /// <summary>
        /// The entire configuration file was reloaded.
        /// </summary>
        Reloaded,

        /// <summary>
        /// Configuration was restored from backup.
        /// </summary>
        Restored
    }
}
