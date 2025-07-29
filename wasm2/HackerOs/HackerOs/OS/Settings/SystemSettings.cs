
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Manages system-wide configuration settings stored in /etc/.
    /// Provides read-only access for non-admin users and system defaults.
    /// </summary>
    public class SystemSettings
    {
        private readonly ISettingsService _settingsService;
        private readonly ILogger<SystemSettings> _logger;

        /// <summary>
        /// Initializes a new instance of the SystemSettings class.
        /// </summary>
        /// <param name="settingsService">The underlying settings service</param>
        /// <param name="logger">Logger for system settings</param>
        public SystemSettings(ISettingsService settingsService, ILogger<SystemSettings> logger)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region System Configuration Properties

        /// <summary>
        /// Gets the system hostname.
        /// </summary>
        public async Task<string> GetHostnameAsync()
        {
            return await _settingsService.GetSettingAsync("System", "hostname", "hackeros-sim", SettingScope.System) ?? "hackeros-sim";
        }

        /// <summary>
        /// Gets the system version.
        /// </summary>
        public async Task<string> GetVersionAsync()
        {
            return await _settingsService.GetSettingAsync("System", "version", "1.0.0", SettingScope.System) ?? "1.0.0";
        }

        /// <summary>
        /// Gets the kernel version.
        /// </summary>
        public async Task<string> GetKernelVersionAsync()
        {
            return await _settingsService.GetSettingAsync("System", "kernel_version", "1.0.0-hackeros", SettingScope.System) ?? "1.0.0-hackeros";
        }

        /// <summary>
        /// Gets the default shell path.
        /// </summary>
        public async Task<string> GetDefaultShellAsync()
        {
            return await _settingsService.GetSettingAsync("System", "default_shell", "/bin/bash", SettingScope.System) ?? "/bin/bash";
        }

        /// <summary>
        /// Gets the system timezone.
        /// </summary>
        public async Task<string> GetTimezoneAsync()
        {
            return await _settingsService.GetSettingAsync("System", "timezone", "UTC", SettingScope.System) ?? "UTC";
        }

        /// <summary>
        /// Gets the system locale.
        /// </summary>
        public async Task<string> GetLocaleAsync()
        {
            return await _settingsService.GetSettingAsync("System", "locale", "en_US.UTF-8", SettingScope.System) ?? "en_US.UTF-8";
        }

        #endregion

        #region Display Configuration

        /// <summary>
        /// Gets the system display resolution.
        /// </summary>
        public async Task<string> GetResolutionAsync()
        {
            return await _settingsService.GetSettingAsync("Display", "resolution", "1920x1080", SettingScope.System) ?? "1920x1080";
        }

        /// <summary>
        /// Gets the color depth setting.
        /// </summary>
        public async Task<int> GetColorDepthAsync()
        {
            return await _settingsService.GetSettingAsync("Display", "color_depth", 32, SettingScope.System);
        }

        /// <summary>
        /// Gets the system theme.
        /// </summary>
        public async Task<string> GetThemeAsync()
        {
            return await _settingsService.GetSettingAsync("Display", "theme", "dark-hacker", SettingScope.System) ?? "dark-hacker";
        }

        /// <summary>
        /// Gets whether desktop effects are enabled.
        /// </summary>
        public async Task<bool> GetDesktopEffectsAsync()
        {
            return await _settingsService.GetSettingAsync("Display", "desktop_effects", true, SettingScope.System);
        }

        /// <summary>
        /// Gets the terminal transparency level.
        /// </summary>
        public async Task<double> GetTerminalTransparencyAsync()
        {
            return await _settingsService.GetSettingAsync("Display", "terminal_transparency", 0.8, SettingScope.System);
        }

        #endregion

        #region Security Configuration

        /// <summary>
        /// Gets the minimum password length.
        /// </summary>
        public async Task<int> GetPasswordMinLengthAsync()
        {
            return await _settingsService.GetSettingAsync("Security", "password_min_length", 8, SettingScope.System);
        }

        /// <summary>
        /// Gets the session timeout in seconds.
        /// </summary>
        public async Task<int> GetSessionTimeoutAsync()
        {
            return await _settingsService.GetSettingAsync("Security", "session_timeout", 1800, SettingScope.System);
        }

        /// <summary>
        /// Gets the auto-lock delay in seconds.
        /// </summary>
        public async Task<int> GetAutoLockDelayAsync()
        {
            return await _settingsService.GetSettingAsync("Security", "auto_lock_delay", 300, SettingScope.System);
        }

        /// <summary>
        /// Gets whether the firewall is enabled.
        /// </summary>
        public async Task<bool> GetFirewallEnabledAsync()
        {
            return await _settingsService.GetSettingAsync("Security", "enable_firewall", true, SettingScope.System);
        }

        #endregion

        #region Network Configuration

        /// <summary>
        /// Gets whether networking is enabled.
        /// </summary>
        public async Task<bool> GetNetworkingEnabledAsync()
        {
            return await _settingsService.GetSettingAsync("Network", "enable_networking", true, SettingScope.System);
        }

        /// <summary>
        /// Gets the DNS servers list.
        /// </summary>
        public async Task<string[]> GetDnsServersAsync()
        {
            var dnsString = await _settingsService.GetSettingAsync("Network", "dns_servers", "8.8.8.8,8.8.4.4", SettingScope.System) ?? "8.8.8.8,8.8.4.4";
            return dnsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => s.Trim())
                           .ToArray();
        }

        /// <summary>
        /// Gets whether proxy is enabled.
        /// </summary>
        public async Task<bool> GetProxyEnabledAsync()
        {
            return await _settingsService.GetSettingAsync("Network", "proxy_enabled", false, SettingScope.System);
        }

        /// <summary>
        /// Gets whether port scan detection is enabled.
        /// </summary>
        public async Task<bool> GetPortScanDetectionAsync()
        {
            return await _settingsService.GetSettingAsync("Network", "port_scan_detection", true, SettingScope.System);
        }

        #endregion

        #region Performance Configuration

        /// <summary>
        /// Gets the maximum number of processes allowed.
        /// </summary>
        public async Task<int> GetMaxProcessesAsync()
        {
            return await _settingsService.GetSettingAsync("Performance", "max_processes", 100, SettingScope.System);
        }

        /// <summary>
        /// Gets the memory limit in megabytes.
        /// </summary>
        public async Task<int> GetMemoryLimitMbAsync()
        {
            return await _settingsService.GetSettingAsync("Performance", "memory_limit_mb", 1024, SettingScope.System);
        }

        /// <summary>
        /// Gets whether CPU throttling is enabled.
        /// </summary>
        public async Task<bool> GetCpuThrottleAsync()
        {
            return await _settingsService.GetSettingAsync("Performance", "cpu_throttle", false, SettingScope.System);
        }

        /// <summary>
        /// Gets the maximum number of background tasks.
        /// </summary>
        public async Task<int> GetBackgroundTasksAsync()
        {
            return await _settingsService.GetSettingAsync("Performance", "background_tasks", 10, SettingScope.System);
        }

        #endregion

        #region Administrative Methods

        /// <summary>
        /// Sets a system configuration value (requires admin privileges in real implementation).
        /// </summary>
        /// <typeparam name="T">The type of the setting value</typeparam>
        /// <param name="section">The configuration section</param>
        /// <param name="key">The setting key</param>
        /// <param name="value">The value to set</param>
        /// <returns>True if the setting was successfully saved</returns>
        public async Task<bool> SetSystemSettingAsync<T>(string section, string key, T value)
        {
            try
            {
                // In a real implementation, this would check for admin privileges
                var success = await _settingsService.SetSettingAsync(section, key, value, SettingScope.System);
                if (success)
                {
                    _logger.LogInformation("System setting changed: {Section}.{Key} = {Value}", section, key, value);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set system setting {Section}.{Key}", section, key);
                return false;
            }
        }

        /// <summary>
        /// Gets all settings in a system configuration section.
        /// </summary>
        /// <param name="section">The section name</param>
        /// <returns>Dictionary of all settings in the section</returns>
        public async Task<Dictionary<string, object>> GetSystemSectionAsync(string section)
        {
            try
            {
                return await _settingsService.GetSectionAsync(section, SettingScope.System);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get system section {Section}", section);
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Reloads all system configuration files.
        /// </summary>
        /// <returns>True if reload was successful</returns>
        public async Task<bool> ReloadSystemConfigsAsync()
        {
            try
            {
                _logger.LogInformation("Reloading system configuration files");
                return await _settingsService.ReloadAllConfigsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload system configuration files");
                return false;
            }
        }

        /// <summary>
        /// Creates default system configuration if it doesn't exist.
        /// </summary>
        /// <returns>True if default config was created successfully</returns>
        public async Task<bool> EnsureDefaultConfigAsync()
        {
            try
            {
                return await _settingsService.CreateDefaultConfigsAsync(SettingScope.System);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create default system configuration");
                return false;
            }
        }

        #endregion
    }
}
