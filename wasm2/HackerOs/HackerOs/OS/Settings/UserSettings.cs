
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Manages user-specific configuration settings stored in ~/.config/.
    /// Provides user-writable settings with inheritance from system settings.
    /// </summary>
    public class UserSettings
    {
        private readonly ISettingsService _settingsService;
        private readonly SystemSettings _systemSettings;
        private readonly ILogger<UserSettings> _logger;
        private readonly string _currentUser;

        /// <summary>
        /// Initializes a new instance of the UserSettings class.
        /// </summary>
        /// <param name="settingsService">The underlying settings service</param>
        /// <param name="systemSettings">The system settings for inheritance</param>
        /// <param name="logger">Logger for user settings</param>
        /// <param name="currentUser">The current user context (defaults to 'user')</param>
        public UserSettings(
            ISettingsService settingsService,
            SystemSettings systemSettings,
            ILogger<UserSettings> logger,
            string currentUser = "user")
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _systemSettings = systemSettings ?? throw new ArgumentNullException(nameof(systemSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentUser = currentUser ?? "user";
        }

        /// <summary>
        /// Convenience wrapper to synchronously set a value in the user configuration.
        /// The key may optionally contain a section name separated by a dot
        /// (e.g. "apps.window.x"). If no section is supplied, "General" is used.
        /// </summary>
        /// <param name="fullKey">Section and key in dotted notation</param>
        /// <param name="value">Value to set</param>
        /// <returns>True if the setting was written successfully</returns>
        public bool SetValue(string fullKey, object value)
        {
            var (section, key) = ParseKey(fullKey);
            return _settingsService
                .SetSettingAsync(section, key, value, SettingScope.User)
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// Convenience wrapper to synchronously retrieve a value from the user configuration.
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="fullKey">Section and key in dotted notation</param>
        /// <param name="defaultValue">Default value if not present</param>
        /// <returns>Retrieved value or the provided default</returns>
        public T GetValue<T>(string fullKey, T defaultValue = default!)
        {
            var (section, key) = ParseKey(fullKey);
            var result = _settingsService
                .GetSettingAsync(section, key, defaultValue, SettingScope.User)
                .GetAwaiter().GetResult();
            return result == null ? defaultValue : result;
        }

        private static (string Section, string Key) ParseKey(string fullKey)
        {
            if (string.IsNullOrWhiteSpace(fullKey))
                throw new ArgumentException("Key cannot be null or empty", nameof(fullKey));

            var index = fullKey.IndexOf('.');
            return index > 0
                ? (fullKey.Substring(0, index), fullKey[(index + 1)..])
                : ("General", fullKey);
        }

        #region User Preferences

        /// <summary>
        /// Gets whether desktop icons are shown.
        /// </summary>
        public async Task<bool> GetShowDesktopIconsAsync()
        {
            return await _settingsService.GetEffectiveSettingAsync("Preferences", "desktop_icons", SettingScope.User, true);
        }

        /// <summary>
        /// Sets whether desktop icons are shown.
        /// </summary>
        public async Task<bool> SetShowDesktopIconsAsync(bool show)
        {
            return await _settingsService.SetSettingAsync("Preferences", "desktop_icons", show, SettingScope.User);
        }

        /// <summary>
        /// Gets whether hidden files are shown.
        /// </summary>
        public async Task<bool> GetShowHiddenFilesAsync()
        {
            return await _settingsService.GetEffectiveSettingAsync("Preferences", "show_hidden_files", SettingScope.User, false);
        }

        /// <summary>
        /// Sets whether hidden files are shown.
        /// </summary>
        public async Task<bool> SetShowHiddenFilesAsync(bool show)
        {
            return await _settingsService.SetSettingAsync("Preferences", "show_hidden_files", show, SettingScope.User);
        }

        /// <summary>
        /// Gets the terminal transparency level (0.0 - 1.0).
        /// </summary>
        public async Task<double> GetTerminalTransparencyAsync()
        {
            return await _settingsService.GetEffectiveSettingAsync("Preferences", "terminal_transparency", SettingScope.User, 0.8);
        }

        /// <summary>
        /// Sets the terminal transparency level.
        /// </summary>
        public async Task<bool> SetTerminalTransparencyAsync(double transparency)
        {
            if (transparency < 0.0 || transparency > 1.0)
                throw new ArgumentOutOfRangeException(nameof(transparency), "Transparency must be between 0.0 and 1.0");

            return await _settingsService.SetSettingAsync("Preferences", "terminal_transparency", transparency, SettingScope.User);
        }

        /// <summary>
        /// Gets the auto-save interval in seconds.
        /// </summary>
        public async Task<int> GetAutoSaveIntervalAsync()
        {
            return await _settingsService.GetEffectiveSettingAsync("Preferences", "auto_save_interval", SettingScope.User, 30);
        }

        /// <summary>
        /// Sets the auto-save interval in seconds.
        /// </summary>
        public async Task<bool> SetAutoSaveIntervalAsync(int seconds)
        {
            if (seconds < 5)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Auto-save interval must be at least 5 seconds");

            return await _settingsService.SetSettingAsync("Preferences", "auto_save_interval", seconds, SettingScope.User);
        }

        /// <summary>
        /// Gets whether deletion confirmations are shown.
        /// </summary>
        public async Task<bool> GetConfirmDeletionsAsync()
        {
            return await _settingsService.GetEffectiveSettingAsync("Preferences", "confirm_deletions", SettingScope.User, true);
        }

        /// <summary>
        /// Sets whether deletion confirmations are shown.
        /// </summary>
        public async Task<bool> SetConfirmDeletionsAsync(bool confirm)
        {
            return await _settingsService.SetSettingAsync("Preferences", "confirm_deletions", confirm, SettingScope.User);
        }

        #endregion

        #region Application Preferences

        /// <summary>
        /// Gets the default text editor.
        /// </summary>
        public async Task<string> GetDefaultEditorAsync()
        {
            return await _settingsService.GetEffectiveSettingAsync("Applications", "default_editor", SettingScope.User, "nano") ?? "nano";
        }

        /// <summary>
        /// Sets the default text editor.
        /// </summary>
        public async Task<bool> SetDefaultEditorAsync(string editor)
        {
            if (string.IsNullOrWhiteSpace(editor))
                throw new ArgumentException("Editor cannot be null or empty", nameof(editor));

            return await _settingsService.SetSettingAsync("Applications", "default_editor", editor, SettingScope.User);
        }

        /// <summary>
        /// Gets the default web browser.
        /// </summary>
        public async Task<string> GetDefaultBrowserAsync()
        {
            return await _settingsService.GetEffectiveSettingAsync("Applications", "default_browser", SettingScope.User, "hacker-browser") ?? "hacker-browser";
        }

        /// <summary>
        /// Sets the default web browser.
        /// </summary>
        public async Task<bool> SetDefaultBrowserAsync(string browser)
        {
            if (string.IsNullOrWhiteSpace(browser))
                throw new ArgumentException("Browser cannot be null or empty", nameof(browser));

            return await _settingsService.SetSettingAsync("Applications", "default_browser", browser, SettingScope.User);
        }

        /// <summary>
        /// Gets the default terminal application.
        /// </summary>
        public async Task<string> GetDefaultTerminalAsync()
        {
            return await _settingsService.GetEffectiveSettingAsync("Applications", "default_terminal", SettingScope.User, "bash") ?? "bash";
        }

        /// <summary>
        /// Sets the default terminal application.
        /// </summary>
        public async Task<bool> SetDefaultTerminalAsync(string terminal)
        {
            if (string.IsNullOrWhiteSpace(terminal))
                throw new ArgumentException("Terminal cannot be null or empty", nameof(terminal));

            return await _settingsService.SetSettingAsync("Applications", "default_terminal", terminal, SettingScope.User);
        }

        /// <summary>
        /// Gets the default file manager.
        /// </summary>
        public async Task<string> GetDefaultFileManagerAsync()
        {
            return await _settingsService.GetEffectiveSettingAsync("Applications", "default_file_manager", SettingScope.User, "files") ?? "files";
        }

        /// <summary>
        /// Sets the default file manager.
        /// </summary>
        public async Task<bool> SetDefaultFileManagerAsync(string fileManager)
        {
            if (string.IsNullOrWhiteSpace(fileManager))
                throw new ArgumentException("File manager cannot be null or empty", nameof(fileManager));

            return await _settingsService.SetSettingAsync("Applications", "default_file_manager", fileManager, SettingScope.User);
        }

        #endregion

        #region Theme Settings

        /// <summary>
        /// Gets the window theme.
        /// </summary>
        public async Task<string> GetWindowThemeAsync()
        {
            return await _settingsService.GetEffectiveSettingAsync("Theme", "window_theme", SettingScope.User, "dark") ?? "dark";
        }

        /// <summary>
        /// Sets the window theme.
        /// </summary>
        public async Task<bool> SetWindowThemeAsync(string theme)
        {
            if (string.IsNullOrWhiteSpace(theme))
                throw new ArgumentException("Theme cannot be null or empty", nameof(theme));

            return await _settingsService.SetSettingAsync("Theme", "window_theme", theme, SettingScope.User);
        }

        /// <summary>
        /// Gets the icon theme.
        /// </summary>
        public async Task<string> GetIconThemeAsync()
        {
            return await _settingsService.GetEffectiveSettingAsync("Theme", "icon_theme", SettingScope.User, "hacker-icons") ?? "hacker-icons";
        }

        /// <summary>
        /// Sets the icon theme.
        /// </summary>
        public async Task<bool> SetIconThemeAsync(string theme)
        {
            if (string.IsNullOrWhiteSpace(theme))
                throw new ArgumentException("Theme cannot be null or empty", nameof(theme));

            return await _settingsService.SetSettingAsync("Theme", "icon_theme", theme, SettingScope.User);
        }

        /// <summary>
        /// Gets the font family.
        /// </summary>
        public async Task<string> GetFontFamilyAsync()
        {
            return await _settingsService.GetEffectiveSettingAsync("Theme", "font_family", SettingScope.User, "Courier New") ?? "Courier New";
        }

        /// <summary>
        /// Sets the font family.
        /// </summary>
        public async Task<bool> SetFontFamilyAsync(string fontFamily)
        {
            if (string.IsNullOrWhiteSpace(fontFamily))
                throw new ArgumentException("Font family cannot be null or empty", nameof(fontFamily));

            return await _settingsService.SetSettingAsync("Theme", "font_family", fontFamily, SettingScope.User);
        }

        /// <summary>
        /// Gets the font size.
        /// </summary>
        public async Task<int> GetFontSizeAsync()
        {
            return await _settingsService.GetEffectiveSettingAsync("Theme", "font_size", SettingScope.User, 12);
        }

        /// <summary>
        /// Sets the font size.
        /// </summary>
        public async Task<bool> SetFontSizeAsync(int size)
        {
            if (size < 8 || size > 72)
                throw new ArgumentOutOfRangeException(nameof(size), "Font size must be between 8 and 72");

            return await _settingsService.SetSettingAsync("Theme", "font_size", size, SettingScope.User);
        }

        /// <summary>
        /// Gets whether syntax highlighting is enabled.
        /// </summary>
        public async Task<bool> GetSyntaxHighlightingAsync()
        {
            return await _settingsService.GetEffectiveSettingAsync("Theme", "syntax_highlighting", SettingScope.User, true);
        }

        /// <summary>
        /// Sets whether syntax highlighting is enabled.
        /// </summary>
        public async Task<bool> SetSyntaxHighlightingAsync(bool enabled)
        {
            return await _settingsService.SetSettingAsync("Theme", "syntax_highlighting", enabled, SettingScope.User);
        }

        #endregion

        #region Keyboard Shortcuts

        /// <summary>
        /// Gets a keyboard shortcut for a specific action.
        /// </summary>
        public async Task<string> GetShortcutAsync(string action)
        {
            var defaultShortcuts = new Dictionary<string, string>
            {
                ["copy"] = "Ctrl+C",
                ["paste"] = "Ctrl+V",
                ["new_terminal"] = "Ctrl+Alt+T",
                ["quick_search"] = "Ctrl+Space",
                ["save"] = "Ctrl+S",
                ["open"] = "Ctrl+O",
                ["quit"] = "Ctrl+Q",
                ["find"] = "Ctrl+F"
            };

            var defaultValue = defaultShortcuts.GetValueOrDefault(action, "");
            return await _settingsService.GetEffectiveSettingAsync("Shortcuts", action, SettingScope.User, defaultValue) ?? defaultValue;
        }

        /// <summary>
        /// Sets a keyboard shortcut for a specific action.
        /// </summary>
        public async Task<bool> SetShortcutAsync(string action, string shortcut)
        {
            if (string.IsNullOrWhiteSpace(action))
                throw new ArgumentException("Action cannot be null or empty", nameof(action));
            if (string.IsNullOrWhiteSpace(shortcut))
                throw new ArgumentException("Shortcut cannot be null or empty", nameof(shortcut));

            return await _settingsService.SetSettingAsync("Shortcuts", action, shortcut, SettingScope.User);
        }

        /// <summary>
        /// Gets all keyboard shortcuts.
        /// </summary>
        public async Task<Dictionary<string, string>> GetAllShortcutsAsync()
        {
            var shortcuts = await _settingsService.GetSectionAsync("Shortcuts", SettingScope.User);
            return shortcuts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? "");
        }

        #endregion

        #region User Management Methods

        /// <summary>
        /// Gets all user settings for a specific section.
        /// </summary>
        /// <param name="section">The section name</param>
        /// <returns>Dictionary of all settings in the section</returns>
        public async Task<Dictionary<string, object>> GetUserSectionAsync(string section)
        {
            try
            {
                return await _settingsService.GetSectionAsync(section, SettingScope.User);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user section {Section} for user {User}", section, _currentUser);
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Resets a user setting to its system default.
        /// </summary>
        /// <param name="section">The section name</param>
        /// <param name="key">The setting key</param>
        /// <returns>True if the setting was reset successfully</returns>
        public async Task<bool> ResetToDefaultAsync(string section, string key)
        {
            try
            {
                // Get the system default value
                var systemValue = await _settingsService.GetSettingAsync<object>(section, key, null, SettingScope.System);
                
                if (systemValue != null)
                {
                    return await _settingsService.SetSettingAsync(section, key, systemValue, SettingScope.User);
                }
                else
                {
                    // Remove user override if no system default exists
                    // This would require a RemoveSettingAsync method in ISettingsService
                    _logger.LogWarning("No system default found for {Section}.{Key}, cannot reset", section, key);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset setting {Section}.{Key} to default for user {User}", section, key, _currentUser);
                return false;
            }
        }

        /// <summary>
        /// Creates default user configuration if it doesn't exist.
        /// </summary>
        /// <returns>True if default config was created successfully</returns>
        public async Task<bool> EnsureDefaultConfigAsync()
        {
            try
            {
                return await _settingsService.CreateDefaultConfigsAsync(SettingScope.User);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create default user configuration for user {User}", _currentUser);
                return false;
            }
        }

        /// <summary>
        /// Exports user settings to a configuration string.
        /// </summary>
        /// <returns>INI formatted configuration string</returns>
        public async Task<string> ExportSettingsAsync()
        {
            try
            {
                var sections = new[] { "Preferences", "Applications", "Theme", "Shortcuts" };
                var exportParser = new ConfigFileParser();

                foreach (var section in sections)
                {
                    var sectionData = await GetUserSectionAsync(section);
                    foreach (var kvp in sectionData)
                    {
                        exportParser.SetValue(section, kvp.Key, kvp.Value);
                    }
                }

                return exportParser.ToIniContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export settings for user {User}", _currentUser);
                return string.Empty;
            }
        }

        /// <summary>
        /// Imports user settings from a configuration string.
        /// </summary>
        /// <param name="configContent">INI formatted configuration content</param>
        /// <returns>True if import was successful</returns>
        public async Task<bool> ImportSettingsAsync(string configContent)
        {
            try
            {
                var parser = new ConfigFileParser();
                if (!parser.ParseContent(configContent))
                {
                    _logger.LogError("Invalid configuration content for import");
                    return false;
                }

                var success = true;
                foreach (var section in parser.Sections)
                {
                    var sectionData = parser.GetSection(section);
                    foreach (var kvp in sectionData)
                    {
                        var setSuccess = await _settingsService.SetSettingAsync(section, kvp.Key, kvp.Value, SettingScope.User);
                        if (!setSuccess)
                        {
                            success = false;
                            _logger.LogWarning("Failed to import setting {Section}.{Key}", section, kvp.Key);
                        }
                    }
                }

                if (success)
                {
                    _logger.LogInformation("Successfully imported settings for user {User}", _currentUser);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import settings for user {User}", _currentUser);
                return false;
            }
        }

        #endregion
    }
}
