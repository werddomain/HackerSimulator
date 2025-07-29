using System.Collections.Generic;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Provides default system configuration templates and values for HackerOS
    /// </summary>
    public static class DefaultSystemConfiguration
    {
        /// <summary>
        /// Gets the default system configuration template content for /etc/hackeros.conf
        /// </summary>
        public static string GetSystemConfigTemplate()
        {
            return @"# HackerOS System Configuration
# /etc/hackeros.conf
# This file contains system-wide configuration settings for HackerOS

[system]
# System identification
hostname=hackeros
domain=localhost
timezone=UTC
language=en_US
encoding=UTF-8

# Boot and startup settings
boot_timeout=10
auto_login=false
splash_screen=true

[kernel]
# Kernel configuration
max_processes=256
max_memory_mb=1024
virtual_memory=true
swap_enabled=false
debug_mode=false

# Process management
process_timeout=30000
max_file_descriptors=1024
enable_process_isolation=true

[security]
# Security settings
password_min_length=8
password_require_uppercase=true
password_require_lowercase=true
password_require_numbers=true
password_require_symbols=false
session_timeout=3600
max_failed_attempts=3
lockout_duration=300

# File system security
default_file_permissions=644
default_directory_permissions=755
enable_file_permissions=true
enable_user_isolation=true

[network]
# Network configuration
enable_networking=true
default_interface=eth0
enable_ipv6=false
dns_servers=8.8.8.8,8.8.4.4

# Web server settings
webserver_enabled=true
webserver_port=80
webserver_ssl_port=443
enable_ssl=false

[display]
# Display and UI settings
theme=dark
terminal_font_family=monospace
terminal_font_size=14
terminal_columns=80
terminal_rows=24
enable_transparency=true
transparency_level=0.9

# Window management
max_windows=10
enable_window_animations=true
window_animation_duration=300

[logging]
# Logging configuration
log_level=info
log_to_file=true
log_file_path=/var/log/hackeros.log
max_log_size_mb=100
log_rotation_count=5
enable_debug_logging=false

[services]
# System services configuration
auto_start_services=filesystem,settings,user,shell
enable_service_isolation=true
service_timeout=10000

# File system service
filesystem_cache_enabled=true
filesystem_cache_size_mb=64
auto_save_interval=30000

[performance]
# Performance tuning
enable_caching=true
cache_size_mb=128
gc_collection_interval=60000
enable_profiling=false
";
        }

        /// <summary>
        /// Gets the default user configuration template content for ~/.config/hackeros/user.conf
        /// </summary>
        public static string GetUserConfigTemplate()
        {
            return @"# HackerOS User Configuration
# ~/.config/hackeros/user.conf
# This file contains user-specific configuration settings

[user]
# User preferences
preferred_shell=/bin/bash
home_directory_layout=standard
enable_command_history=true
history_size=1000
auto_save_session=true

# Personal information
display_name=
email=
timezone=inherit

[desktop]
# Desktop environment settings
wallpaper=default
icon_theme=default
cursor_theme=default
enable_desktop_effects=true

# Window preferences
default_window_size=800x600
remember_window_positions=true
enable_window_grouping=true

[terminal]
# Terminal preferences (overrides system defaults)
font_family=inherit
font_size=inherit
color_scheme=dark
cursor_style=block
enable_bell=false

# Terminal behavior
scroll_on_output=true
scroll_on_keystroke=true
scrollback_lines=10000
enable_mouse_reporting=true

[applications]
# Application preferences
default_text_editor=nano
default_file_manager=fm
default_web_browser=browser
enable_application_sandboxing=true

# Application launch settings
auto_start_applications=
remember_application_state=true

[privacy]
# Privacy settings
enable_usage_tracking=false
enable_crash_reporting=false
clear_history_on_exit=false
enable_private_mode=false
";
        }

        /// <summary>
        /// Gets the default desktop configuration template content for ~/.config/hackeros/desktop.conf
        /// </summary>
        public static string GetDesktopConfigTemplate()
        {
            return @"# HackerOS Desktop Configuration
# ~/.config/hackeros/desktop.conf
# This file contains desktop environment specific settings

[appearance]
# Visual appearance
theme=inherit
icon_size=32
font_scaling=1.0
enable_smooth_fonts=true
enable_subpixel_rendering=true

[behavior]
# Desktop behavior
click_policy=single
auto_arrange_icons=false
show_hidden_files=false
confirm_delete=true
confirm_overwrite=true

[shortcuts]
# Keyboard shortcuts
new_terminal=Ctrl+Alt+T
new_window=Ctrl+N
close_window=Ctrl+Q
switch_window=Alt+Tab
show_desktop=Super+D
lock_screen=Ctrl+Alt+L

[panels]
# Panel configuration
show_taskbar=true
taskbar_position=bottom
taskbar_auto_hide=false
show_system_tray=true
show_clock=true
clock_format=24h

[workspace]
# Workspace settings
number_of_workspaces=4
workspace_switching=true
workspace_wraparound=true
remember_workspace_layout=true
";
        }

        /// <summary>
        /// Gets the default theme configuration template content for ~/.config/hackeros/theme.conf
        /// </summary>
        public static string GetThemeConfigTemplate()
        {
            return @"# HackerOS Theme Configuration
# ~/.config/hackeros/theme.conf
# This file contains theme and visual customization settings

[colors]
# Color scheme
primary_color=#00ff00
secondary_color=#0080ff
accent_color=#ff8000
background_color=#000000
foreground_color=#ffffff
warning_color=#ffff00
error_color=#ff0000
success_color=#00ff00

[terminal_colors]
# Terminal color palette
black=#000000
red=#ff0000
green=#00ff00
yellow=#ffff00
blue=#0000ff
magenta=#ff00ff
cyan=#00ffff
white=#ffffff

# Bright colors
bright_black=#808080
bright_red=#ff8080
bright_green=#80ff80
bright_yellow=#ffff80
bright_blue=#8080ff
bright_magenta=#ff80ff
bright_cyan=#80ffff
bright_white=#ffffff

[fonts]
# Font configuration
system_font=Arial
monospace_font=Courier New
ui_font=Segoe UI
terminal_font=Consolas

# Font sizes
system_font_size=12
ui_font_size=11
terminal_font_size=14
title_font_size=16

[effects]
# Visual effects
enable_shadows=true
shadow_opacity=0.5
enable_blur=false
blur_radius=10
enable_animations=true
animation_speed=normal

[custom]
# Custom theme overrides
css_overrides=
custom_properties=
";
        }

        /// <summary>
        /// Gets the default configuration values as a dictionary
        /// </summary>
        public static Dictionary<string, object> GetDefaultValues()
        {
            return new Dictionary<string, object>
            {
                // System defaults
                ["system.hostname"] = "hackeros",
                ["system.domain"] = "localhost",
                ["system.timezone"] = "UTC",
                ["system.language"] = "en_US",
                ["system.encoding"] = "UTF-8",
                ["system.boot_timeout"] = 10,
                ["system.auto_login"] = false,
                ["system.splash_screen"] = true,

                // Kernel defaults
                ["kernel.max_processes"] = 256,
                ["kernel.max_memory_mb"] = 1024,
                ["kernel.virtual_memory"] = true,
                ["kernel.swap_enabled"] = false,
                ["kernel.debug_mode"] = false,
                ["kernel.process_timeout"] = 30000,
                ["kernel.max_file_descriptors"] = 1024,
                ["kernel.enable_process_isolation"] = true,

                // Security defaults
                ["security.password_min_length"] = 8,
                ["security.password_require_uppercase"] = true,
                ["security.password_require_lowercase"] = true,
                ["security.password_require_numbers"] = true,
                ["security.password_require_symbols"] = false,
                ["security.session_timeout"] = 3600,
                ["security.max_failed_attempts"] = 3,
                ["security.lockout_duration"] = 300,
                ["security.default_file_permissions"] = "644",
                ["security.default_directory_permissions"] = "755",
                ["security.enable_file_permissions"] = true,
                ["security.enable_user_isolation"] = true,

                // Network defaults
                ["network.enable_networking"] = true,
                ["network.default_interface"] = "eth0",
                ["network.enable_ipv6"] = false,
                ["network.dns_servers"] = "8.8.8.8,8.8.4.4",
                ["network.webserver_enabled"] = true,
                ["network.webserver_port"] = 80,
                ["network.webserver_ssl_port"] = 443,
                ["network.enable_ssl"] = false,

                // Display defaults
                ["display.theme"] = "dark",
                ["display.terminal_font_family"] = "monospace",
                ["display.terminal_font_size"] = 14,
                ["display.terminal_columns"] = 80,
                ["display.terminal_rows"] = 24,
                ["display.enable_transparency"] = true,
                ["display.transparency_level"] = 0.9f,
                ["display.max_windows"] = 10,
                ["display.enable_window_animations"] = true,
                ["display.window_animation_duration"] = 300,

                // Logging defaults
                ["logging.log_level"] = "info",
                ["logging.log_to_file"] = true,
                ["logging.log_file_path"] = "/var/log/hackeros.log",
                ["logging.max_log_size_mb"] = 100,
                ["logging.log_rotation_count"] = 5,
                ["logging.enable_debug_logging"] = false,

                // Services defaults
                ["services.auto_start_services"] = "filesystem,settings,user,shell",
                ["services.enable_service_isolation"] = true,
                ["services.service_timeout"] = 10000,
                ["services.filesystem_cache_enabled"] = true,
                ["services.filesystem_cache_size_mb"] = 64,
                ["services.auto_save_interval"] = 30000,

                // Performance defaults
                ["performance.enable_caching"] = true,
                ["performance.cache_size_mb"] = 128,
                ["performance.gc_collection_interval"] = 60000,
                ["performance.enable_profiling"] = false
            };
        }

        /// <summary>
        /// Gets the list of required configuration keys that must be present
        /// </summary>
        public static HashSet<string> GetRequiredSettings()
        {
            return new HashSet<string>
            {
                "system.hostname",
                "system.timezone",
                "kernel.max_processes",
                "kernel.max_memory_mb",
                "security.session_timeout",
                "display.terminal_font_size",
                "logging.log_level"
            };
        }

        /// <summary>
        /// Gets configuration value validation rules
        /// </summary>
        public static Dictionary<string, ConfigurationValidationRule> GetValidationRules()
        {
            return new Dictionary<string, ConfigurationValidationRule>
            {
                ["system.boot_timeout"] = new ConfigurationValidationRule
                {
                    Type = typeof(int),
                    MinValue = 1,
                    MaxValue = 60,
                    Required = false
                },
                ["kernel.max_processes"] = new ConfigurationValidationRule
                {
                    Type = typeof(int),
                    MinValue = 16,
                    MaxValue = 4096,
                    Required = true
                },
                ["kernel.max_memory_mb"] = new ConfigurationValidationRule
                {
                    Type = typeof(int),
                    MinValue = 64,
                    MaxValue = 8192,
                    Required = true
                },
                ["security.password_min_length"] = new ConfigurationValidationRule
                {
                    Type = typeof(int),
                    MinValue = 4,
                    MaxValue = 64,
                    Required = false
                },
                ["security.session_timeout"] = new ConfigurationValidationRule
                {
                    Type = typeof(int),
                    MinValue = 300,
                    MaxValue = 86400,
                    Required = true
                },
                ["display.terminal_font_size"] = new ConfigurationValidationRule
                {
                    Type = typeof(int),
                    MinValue = 8,
                    MaxValue = 72,
                    Required = true
                },
                ["display.transparency_level"] = new ConfigurationValidationRule
                {
                    Type = typeof(float),
                    MinValue = 0.0f,
                    MaxValue = 1.0f,
                    Required = false
                },
                ["logging.log_level"] = new ConfigurationValidationRule
                {
                    Type = typeof(string),
                    AllowedValues = new[] { "debug", "info", "warning", "error", "critical" },
                    Required = true
                }
            };
        }
    }

    /// <summary>
    /// Represents validation rules for configuration values
    /// </summary>
    public class ConfigurationValidationRule
    {
        /// <summary>
        /// The expected type of the configuration value
        /// </summary>
        public Type Type { get; set; } = typeof(object);

        /// <summary>
        /// Whether this configuration value is required
        /// </summary>
        public bool Required { get; set; } = false;

        /// <summary>
        /// Minimum allowed value (for numeric types)
        /// </summary>
        public object? MinValue { get; set; }

        /// <summary>
        /// Maximum allowed value (for numeric types)
        /// </summary>
        public object? MaxValue { get; set; }

        /// <summary>
        /// Allowed values (for string/enum types)
        /// </summary>
        public string[]? AllowedValues { get; set; }

        /// <summary>
        /// Custom validation pattern (regex for strings)
        /// </summary>
        public string? Pattern { get; set; }

        /// <summary>
        /// Custom error message for validation failures
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
