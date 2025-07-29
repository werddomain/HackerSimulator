
namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Defines the scope levels for configuration settings in HackerOS.
    /// Follows Linux-style configuration hierarchy.
    /// </summary>
    public enum SettingScope
    {
        /// <summary>
        /// System-wide configuration stored in /etc/
        /// Read-only for non-admin users, affects all users
        /// </summary>
        System,

        /// <summary>
        /// User-specific configuration stored in ~/.config/
        /// Writable by the owning user, inherits from system settings
        /// </summary>
        User,

        /// <summary>
        /// Application-specific configuration
        /// Stored in app-specific directories under ~/.config/
        /// </summary>
        Application
    }
}
