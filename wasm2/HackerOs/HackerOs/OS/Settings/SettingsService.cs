using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HackerOs.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Main implementation of the HackerOS settings service.
    /// Manages configuration files in the virtual file system following Linux conventions.
    /// </summary>
    public class SettingsService : ISettingsService, ISettingsProvider, IDisposable
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILogger<SettingsService> _logger;
        private readonly Dictionary<string, ConfigFileParser> _loadedConfigs;
        private readonly Dictionary<SettingScope, string> _configPaths;
        private readonly ConfigurationWatcher _configWatcher;
        private readonly SettingsInheritanceManager _inheritanceManager;
        private readonly Dictionary<SettingScope, ISettingsProvider> _scopeProviders;
        private readonly ILoggerFactory _loggerFactory;
        
        // Phase 2.1.3 services integration
        private readonly ConfigurationValidator _validator;
        private readonly ConfigurationBackupService _backupService;
        private readonly ConfigurationInitializationService _initializationService;
        
        private bool _disposed = false;

        /// <summary>
        /// Event raised when any configuration file changes.
        /// </summary>
        public event EventHandler<ConfigurationChangedEventArgs>? OnConfigurationChanged;

        /// <summary>
        /// Initializes a new instance of the SettingsService class.
        /// </summary>
        /// <param name="fileSystem">The virtual file system for config file access</param>
        /// <param name="logger">Logger for the settings service</param>
        /// <param name="loggerFactory">Logger factory for creating other loggers</param>
        public SettingsService(IVirtualFileSystem fileSystem, ILogger<SettingsService> logger, ILoggerFactory loggerFactory)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _loadedConfigs = new Dictionary<string, ConfigFileParser>();

            // Define standard configuration file paths following Linux conventions
            _configPaths = new Dictionary<SettingScope, string>
            {
                [SettingScope.System] = "/etc/hackeros.conf",
                [SettingScope.User] = "~/.config/hackeros/user.conf",
                [SettingScope.Application] = "~/.config/applications/"
            };

            // Initialize Phase 2.1.3 services
            var validatorLogger = _loggerFactory.CreateLogger<ConfigurationValidator>();
            _validator = new ConfigurationValidator(validatorLogger);
            
            var backupLogger = _loggerFactory.CreateLogger<ConfigurationBackupService>();
            _backupService = new ConfigurationBackupService(_fileSystem, backupLogger);
            
            var initLogger = _loggerFactory.CreateLogger<ConfigurationInitializationService>();
            _initializationService = new ConfigurationInitializationService(_fileSystem, _validator, initLogger);

            // Initialize the configuration watcher and inheritance manager
            var configWatcherLogger = _loggerFactory.CreateLogger<ConfigurationWatcher>();
            _configWatcher = new ConfigurationWatcher(_fileSystem, configWatcherLogger);
            
            // Create logger for inheritance manager
            var inheritanceLogger = _loggerFactory.CreateLogger<SettingsInheritanceManager>();
            _inheritanceManager = new SettingsInheritanceManager(inheritanceLogger);
            
            // Set up scope providers
            _scopeProviders = new Dictionary<SettingScope, ISettingsProvider>
            {
                [SettingScope.System] = this,
                [SettingScope.User] = this,
                [SettingScope.Application] = this
            };

            // Subscribe to configuration file change events
            _configWatcher.ConfigurationFileChanged += OnConfigurationFileChanged;

            // Start watching configuration files and initialize configuration
            _ = Task.Run(async () => await InitializeServiceAsync());
        }
            
            // Define standard configuration file paths following Linux conventions
            _configPaths = new Dictionary<SettingScope, string>
            {
                [SettingScope.System] = "/etc/hackeros.conf",
                [SettingScope.User] = "~/.config/hackeros/user.conf",
                [SettingScope.Application] = "~/.config/applications/"
            };            // Initialize Phase 2.1.3 services
            var validatorLogger = _loggerFactory.CreateLogger<ConfigurationValidator>();
            _validator = new ConfigurationValidator(validatorLogger);
            
            var backupLogger = _loggerFactory.CreateLogger<ConfigurationBackupService>();
            _backupService = new ConfigurationBackupService(_fileSystem, backupLogger);
            
            var initLogger = _loggerFactory.CreateLogger<ConfigurationInitializationService>();
            _initializationService = new ConfigurationInitializationService(_fileSystem, _validator, initLogger);

            // Initialize the configuration watcher and inheritance manager
            var configWatcherLogger = _loggerFactory.CreateLogger<ConfigurationWatcher>();
            _configWatcher = new ConfigurationWatcher(_fileSystem, configWatcherLogger);
            
            // Create logger for inheritance manager
            var inheritanceLogger = _loggerFactory.CreateLogger<SettingsInheritanceManager>();
            _inheritanceManager = new SettingsInheritanceManager(inheritanceLogger);
            
            // Set up scope providers
            _scopeProviders = new Dictionary<SettingScope, ISettingsProvider>
            {
                [SettingScope.System] = this,
                [SettingScope.User] = this,
                [SettingScope.Application] = this
            };

            // Subscribe to configuration file change events
            _configWatcher.ConfigurationFileChanged += OnConfigurationFileChanged;
              // Start watching configuration files and initialize configuration
            _ = Task.Run(async () => await InitializeServiceAsync());
        }

        /// <summary>
        /// Gets a specific setting value from configuration files.
        /// </summary>
        public async Task<T?> GetSettingAsync<T>(string section, string key, T? defaultValue = default, SettingScope scope = SettingScope.User)
        {
            try
            {
                var configPath = GetConfigPath(scope);
                var parser = await LoadConfigParserAsync(configPath);
                
                if (parser != null)
                {
                    return parser.GetValue(section, key, defaultValue);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get setting {Section}.{Key} from scope {Scope}", section, key, scope);
            }

            return defaultValue;
        }

        /// <summary>
        /// Sets a setting value in the appropriate configuration file.
        /// </summary>
        public async Task<bool> SetSettingAsync<T>(string section, string key, T value, SettingScope scope = SettingScope.User)
        {
            try
            {
                var configPath = GetConfigPath(scope);
                var parser = await LoadConfigParserAsync(configPath) ?? new ConfigFileParser();
                
                parser.SetValue(section, key, value!);
                
                var success = await SaveConfigParserAsync(configPath, parser);
                if (success)
                {
                    _loadedConfigs[configPath] = parser;
                    FireConfigurationChanged(configPath, section, key, scope, ConfigurationChangeType.Modified);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set setting {Section}.{Key} in scope {Scope}", section, key, scope);
                return false;
            }
        }

        /// <summary>
        /// Gets all settings within a specific section.
        /// </summary>
        public async Task<Dictionary<string, object>> GetSectionAsync(string section, SettingScope scope = SettingScope.User)
        {
            try
            {
                var configPath = GetConfigPath(scope);
                var parser = await LoadConfigParserAsync(configPath);
                
                if (parser != null)
                {
                    var sectionData = parser.GetSection(section);
                    return new Dictionary<string, object>(sectionData.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get section {Section} from scope {Scope}", section, scope);
            }

            return new Dictionary<string, object>();
        }

        /// <summary>
        /// Gets an effective setting value considering the hierarchy (system → user → app).
        /// </summary>
        public async Task<T?> GetEffectiveSettingAsync<T>(string section, string key, SettingScope maxScope = SettingScope.User, T? defaultValue = default)
        {
            try
            {
                // Search in order of precedence: Application → User → System
                var scopes = new List<SettingScope>();
                
                if (maxScope >= SettingScope.Application)
                    scopes.Add(SettingScope.Application);
                if (maxScope >= SettingScope.User)
                    scopes.Add(SettingScope.User);
                scopes.Add(SettingScope.System);

                foreach (var scope in scopes)
                {
                    var value = await GetSettingAsync(section, key, defaultValue, scope);
                    if (value != null && !value.Equals(defaultValue))
                    {
                        return value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get effective setting {Section}.{Key}", section, key);
            }

            return defaultValue;
        }

        /// <summary>
        /// Loads a specific configuration file into memory.
        /// </summary>
        public async Task<bool> LoadConfigFileAsync(string configPath)
        {
            try
            {
                var parser = await LoadConfigParserAsync(configPath);
                return parser != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load config file {ConfigPath}", configPath);
                return false;
            }
        }

        /// <summary>
        /// Saves a specific configuration file from memory to storage.
        /// </summary>
        public async Task<bool> SaveConfigFileAsync(string configPath)
        {
            try
            {
                if (_loadedConfigs.TryGetValue(configPath, out var parser))
                {
                    return await SaveConfigParserAsync(configPath, parser);
                }
                
                _logger.LogWarning("Configuration file {ConfigPath} not loaded in memory", configPath);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save config file {ConfigPath}", configPath);
                return false;
            }
        }

        /// <summary>
        /// Reloads all configuration files from storage.
        /// </summary>
        public async Task<bool> ReloadAllConfigsAsync()
        {
            try
            {
                var allSuccess = true;
                var configPaths = _loadedConfigs.Keys.ToList();
                
                _loadedConfigs.Clear();
                
                foreach (var configPath in configPaths)
                {
                    var success = await LoadConfigFileAsync(configPath);
                    if (!success)
                    {
                        allSuccess = false;
                    }
                }

                FireConfigurationChanged("*", "*", null, SettingScope.System, ConfigurationChangeType.Reloaded);
                return allSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload all configuration files");
                return false;
            }
        }        /// <summary>
        /// Validates the syntax and content of a configuration file.
        /// </summary>
        public async Task<bool> ValidateConfigAsync(string configPath)
        {
            try
            {
                if (await _fileSystem.ExistsAsync(configPath))
                {
                    var contentBytes = await _fileSystem.ReadFileAsync(configPath);
                    if (contentBytes != null)
                    {
                        var content = Encoding.UTF8.GetString(contentBytes);
                        return ConfigFileParser.ValidateSyntax(content);
                    }
                }
                return true; // Non-existent files are considered valid
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate config file {ConfigPath}", configPath);
                return false;
            }
        }        /// <summary>
        /// Gets the list of available configuration files for a specific scope.
        /// </summary>
        public async Task<IEnumerable<string>> GetConfigFilesAsync(SettingScope scope)
        {
            try
            {
                var basePath = scope switch
                {
                    SettingScope.System => "/etc/",
                    SettingScope.User => "~/.config/",
                    SettingScope.Application => "~/.config/applications/",
                    _ => throw new ArgumentException($"Unknown scope: {scope}")
                };

                var files = new List<string>();
                
                if (await _fileSystem.ExistsAsync(basePath))
                {
                    // Get all .conf files in the directory
                    var entries = await _fileSystem.ListDirectoryAsync(basePath);
                    files.AddRange(entries
                        .Where(e => !e.IsDirectory && e.Name.EndsWith(".conf", StringComparison.OrdinalIgnoreCase))
                        .Select(e => Path.Combine(basePath, e.Name).Replace('\\', '/')));
                }

                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get config files for scope {Scope}", scope);
                return Enumerable.Empty<string>();
            }
        }        /// <summary>
        /// Creates default configuration files if they don't exist.
        /// </summary>
        public async Task<bool> CreateDefaultConfigsAsync(SettingScope scope)
        {
            try
            {
                var configPath = GetConfigPath(scope);
                
                if (!await _fileSystem.ExistsAsync(configPath))
                {
                    var defaultContent = scope switch
                    {
                        SettingScope.System => GetDefaultSystemConfig(),
                        SettingScope.User => GetDefaultUserConfig(),
                        SettingScope.Application => GetDefaultApplicationConfig(),
                        _ => string.Empty
                    };

                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(configPath)?.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(directory) && !await _fileSystem.ExistsAsync(directory))
                    {
                        await _fileSystem.CreateDirectoryAsync(directory);
                    }

                    var contentBytes = Encoding.UTF8.GetBytes(defaultContent);
                    await _fileSystem.WriteFileAsync(configPath, contentBytes);
                    _logger.LogInformation("Created default configuration file: {ConfigPath}", configPath);
                    return true;
                }

                return true; // File already exists
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create default config for scope {Scope}", scope);
                return false;
            }
        }

        #region Private Methods

        private string GetConfigPath(SettingScope scope)
        {
            return _configPaths.TryGetValue(scope, out var path) ? path : throw new ArgumentException($"Unknown scope: {scope}");
        }        private async Task<ConfigFileParser?> LoadConfigParserAsync(string configPath)
        {
            // Check if already loaded
            if (_loadedConfigs.TryGetValue(configPath, out var cachedParser))
            {
                return cachedParser;
            }

            try
            {
                if (await _fileSystem.ExistsAsync(configPath))
                {
                    var contentBytes = await _fileSystem.ReadFileAsync(configPath);
                    if (contentBytes != null)
                    {
                        var content = Encoding.UTF8.GetString(contentBytes);
                        var parser = new ConfigFileParser();
                        
                        if (parser.ParseContent(content))
                        {
                            _loadedConfigs[configPath] = parser;
                            return parser;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration file {ConfigPath}", configPath);
            }

            return null;
        }        private async Task<bool> SaveConfigParserAsync(string configPath, ConfigFileParser parser)
        {
            try
            {
                var content = parser.ToIniContent();
                var contentBytes = Encoding.UTF8.GetBytes(content);
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(configPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(directory) && !await _fileSystem.ExistsAsync(directory))
                {
                    await _fileSystem.CreateDirectoryAsync(directory);
                }

                await _fileSystem.WriteFileAsync(configPath, contentBytes);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration file {ConfigPath}", configPath);
                return false;
            }
        }        private void OnFileSystemEvent(object? sender, FileSystemEvent e)
        {
            // Check if the changed file is a configuration file we're monitoring
            if (e.Path.EndsWith(".conf", StringComparison.OrdinalIgnoreCase) &&
                _loadedConfigs.ContainsKey(e.Path))
            {
                // Remove from cache to force reload on next access
                _loadedConfigs.Remove(e.Path);
                
                var scope = DetermineScope(e.Path);
                FireConfigurationChanged(e.Path, "*", null, scope, ConfigurationChangeType.Reloaded);
            }
        }

        private SettingScope DetermineScope(string configPath)
        {
            if (configPath.StartsWith("/etc/"))
                return SettingScope.System;
            else if (configPath.Contains("/.config/applications/"))
                return SettingScope.Application;
            else
                return SettingScope.User;
        }

        private void FireConfigurationChanged(string configPath, string section, string? key, SettingScope scope, ConfigurationChangeType changeType)
        {
            OnConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(configPath, section, key, scope, changeType));
        }

        private static string GetDefaultSystemConfig()
        {
            return @"# HackerOS System Configuration
# /etc/hackeros.conf

[System]
hostname=hackeros-sim
version=1.0.0
kernel_version=1.0.0-hackeros
default_shell=/bin/bash
timezone=UTC
locale=en_US.UTF-8

[Display]
resolution=1920x1080
color_depth=32
theme=dark-hacker
desktop_effects=true
terminal_transparency=0.8

[Security]
password_min_length=8
session_timeout=1800
auto_lock_delay=300
enable_firewall=true

[Network]
enable_networking=true
dns_servers=8.8.8.8,8.8.4.4
proxy_enabled=false
port_scan_detection=true

[Performance]
max_processes=100
memory_limit_mb=1024
cpu_throttle=false
background_tasks=10
";
        }

        private static string GetDefaultUserConfig()
        {
            return @"# HackerOS User Configuration
# ~/.config/hackeros/user.conf

[Preferences]
desktop_icons=true
show_hidden_files=false
terminal_transparency=0.8
auto_save_interval=30
confirm_deletions=true

[Applications]
default_editor=nano
default_browser=hacker-browser
default_terminal=bash
default_file_manager=files

[Theme]
window_theme=dark
icon_theme=hacker-icons
font_family=Courier New
font_size=12
syntax_highlighting=true

[Shortcuts]
copy=Ctrl+C
paste=Ctrl+V
new_terminal=Ctrl+Alt+T
quick_search=Ctrl+Space
";
        }

        private static string GetDefaultApplicationConfig()
        {
            return @"# HackerOS Application Configuration Template
# ~/.config/applications/app.conf

[Application]
name=DefaultApp
version=1.0.0
auto_start=false
permissions=read,write
sandbox_enabled=true

[Resources]
memory_limit_mb=128
cpu_priority=normal
network_access=limited
file_access=user_home
";        }

        #endregion

        #region Event Handlers and Watchers

        /// <summary>
        /// Initializes configuration file watchers
        /// </summary>
        private async Task InitializeWatchersAsync()
        {
            try
            {
                // Watch all configuration files
                foreach (var configPath in _configPaths.Values)
                {
                    if (!string.IsNullOrEmpty(configPath) && !configPath.EndsWith("/"))
                    {
                        await _configWatcher.WatchFileAsync(configPath);
                    }
                }
                
                _logger.LogInformation("Configuration file watchers initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize configuration watchers");
            }
        }

        /// <summary>
        /// Handles configuration file change events
        /// </summary>
        private void OnConfigurationFileChanged(object? sender, ConfigurationFileChangedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Configuration file changed: {FilePath}", e.FilePath);
                
                // Remove from cache to force reload
                if (_loadedConfigs.ContainsKey(e.FilePath))
                {
                    _loadedConfigs.Remove(e.FilePath);
                }
                
                // Determine scope from file path
                var scope = GetScopeFromPath(e.FilePath);
                  // Fire configuration changed event
                OnConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(
                    configPath: e.FilePath,
                    section: "Unknown", // We don't know which section changed from file events
                    key: null, // We don't know which key changed
                    scope: scope,
                    changeType: ConfigurationChangeType.Reloaded
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling configuration file change: {FilePath}", e.FilePath);
            }
        }

        /// <summary>
        /// Gets the setting scope from a file path
        /// </summary>
        private SettingScope GetScopeFromPath(string filePath)
        {
            foreach (var kvp in _configPaths)
            {
                if (filePath.Equals(kvp.Value, StringComparison.OrdinalIgnoreCase) ||
                    filePath.StartsWith(kvp.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }
            return SettingScope.User; // Default fallback
        }

        #endregion

        #region ISettingsProvider Implementation

        /// <summary>
        /// Checks if a setting value exists in the current scope
        /// </summary>
        public bool HasValue(string key, string? section = null)
        {
            // This will be overridden by specialized providers
            return false;
        }        /// <summary>
        /// Gets a setting value from the current scope
        /// </summary>
        public T GetValue<T>(string key, string? section = null, T defaultValue = default!)
        {
            // This will be overridden by specialized providers
            return defaultValue;
        }

        /// <summary>
        /// Gets all settings in a section from the current scope
        /// </summary>
        public Dictionary<string, object> GetAllSettings(string? section = null)
        {
            // This will be overridden by specialized providers
            return new Dictionary<string, object>();
        }

        #endregion

        #region Enhanced API Methods

        /// <summary>
        /// Gets information about where a setting value is coming from
        /// </summary>
        public SettingResolutionInfo GetSettingSource(string section, string key)
        {
            return _inheritanceManager.GetSettingSource(key, section, _scopeProviders);
        }

        /// <summary>
        /// Gets all effective settings by merging all scopes
        /// </summary>
        public Dictionary<string, object> GetEffectiveSettings(string? section = null)
        {
            return _inheritanceManager.GetEffectiveSettings(section, _scopeProviders);
        }        #endregion

        #region Service Initialization (Phase 2.1.3)

        /// <summary>        /// <summary>
        /// Initializes the settings service including configuration files and watchers.
        /// </summary>
        private async Task InitializeServiceAsync()
        {
            try
            {
                _logger.LogInformation("Initializing SettingsService...");

                // Initialize configuration directory structure and default files
                await _initializationService.InitializeConfigurationAsync(forceRecreate: false);

                // Initialize configuration file watchers
                await InitializeWatchersAsync();

                _logger.LogInformation("SettingsService initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize SettingsService");
            }
        }

        /// <summary>
        /// Gets the current configuration initialization status.
        /// </summary>
        public async Task<ConfigurationInitializationStatus> GetInitializationStatusAsync()
        {
            try
            {
                return await _initializationService.GetInitializationStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get configuration initialization status");
                return new ConfigurationInitializationStatus
                {
                    IsFullyInitialized = false,
                    SystemConfigExists = false,
                    UserTemplatesExist = false,
                    RequiredDirectoriesExist = false
                };
            }
        }

        /// <summary>
        /// Creates configuration for a new user.
        /// </summary>
        /// <param name="username">The username to create configuration for</param>
        public async Task<bool> InitializeUserConfigurationAsync(string username)
        {
            try
            {
                await _initializationService.InitializeUserConfigurationAsync(username);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize user configuration for: {Username}", username);
                return false;
            }
        }

        /// <summary>
        /// Enhanced setting setter with validation and backup functionality.
        /// </summary>
        public async Task<bool> SetSettingWithValidationAsync<T>(string section, string key, T value, SettingScope scope = SettingScope.User)
        {
            try
            {
                var configKey = $"{section}.{key}";
                
                // Validate the setting value
                if (!_validator.ValidateValue(configKey, value))
                {
                    _logger.LogWarning("Setting validation failed for {Key}: {Value}", configKey, value);
                    return false;
                }

                var configPath = GetConfigPath(scope);
                
                // Create backup before modification if file exists
                if (await _fileSystem.ExistsAsync(configPath))
                {
                    try
                    {
                        await _backupService.CreateBackupAsync(configPath);
                        _logger.LogDebug("Created backup for configuration file: {Path}", configPath);
                    }
                    catch (Exception backupEx)
                    {
                        _logger.LogWarning(backupEx, "Failed to create backup for {Path}, continuing with setting update", configPath);
                    }
                }

                // Set the value using the standard method
                return await SetSettingAsync(section, key, value, scope);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set validated setting {Section}.{Key} in scope {Scope}", section, key, scope);
                return false;
            }
        }

        /// <summary>
        /// Validates the current configuration and provides validation results.
        /// </summary>
        /// <param name="scope">The scope to validate</param>
        /// <returns>A list of validation errors</returns>
        public async Task<List<ConfigurationValidationError>> ValidateConfigurationAsync(SettingScope scope = SettingScope.System)
        {
            try
            {
                var configPath = GetConfigPath(scope);
                
                if (!await _fileSystem.ExistsAsync(configPath))
                {
                    return new List<ConfigurationValidationError>
                    {
                        new ConfigurationValidationError
                        {
                            Key = "file",
                            Message = $"Configuration file does not exist: {configPath}",
                            ErrorType = ConfigurationValidationErrorType.FileMissing
                        }
                    };
                }

                // Load and validate configuration
                var parser = await LoadConfigParserAsync(configPath);
                if (parser == null)
                {
                    return new List<ConfigurationValidationError>
                    {
                        new ConfigurationValidationError
                        {
                            Key = "file",
                            Message = $"Failed to parse configuration file: {configPath}",
                            ErrorType = ConfigurationValidationErrorType.ParseError
                        }
                    };
                }

                // Convert parser content to validation format
                var configurationData = new Dictionary<string, object?>();
                var sections = parser.GetAllSections();
                
                foreach (var section in sections)
                {
                    var sectionData = parser.GetSection(section);
                    foreach (var kvp in sectionData)
                    {
                        var key = $"{section}.{kvp.Key}";
                        configurationData[key] = kvp.Value;
                    }
                }

                return _validator.ValidateConfiguration(configurationData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate configuration for scope: {Scope}", scope);
                return new List<ConfigurationValidationError>
                {
                    new ConfigurationValidationError
                    {
                        Key = "validation",
                        Message = $"Configuration validation failed: {ex.Message}",
                        ErrorType = ConfigurationValidationErrorType.ValidationError
                    }
                };
            }
        }

        /// <summary>
        /// Restores a configuration file from backup.
        /// </summary>
        /// <param name="scope">The scope to restore</param>
        /// <param name="backupId">Optional specific backup ID to restore</param>        public async Task<bool> RestoreConfigurationAsync(SettingScope scope, string? backupId = null)
        {
            try
            {
                var configPath = GetConfigPath(scope);
                
                // Find the most recent backup if no specific backup ID is provided
                var backups = await _backupService.ListBackupsAsync(configPath);
                var backupToRestore = backups.OrderByDescending(b => b.BackupTime).FirstOrDefault();
                
                if (backupToRestore == null)
                {
                    _logger.LogWarning("No backup found for configuration: {ConfigPath}", configPath);
                    return false;
                }

                var restoredPath = await _backupService.RestoreBackupAsync(backupToRestore.BackupPath, configPath);
                var restored = !string.IsNullOrEmpty(restoredPath);
                
                if (restored)
                {
                    // Remove from cache to force reload
                    if (_loadedConfigs.ContainsKey(configPath))
                    {
                        _loadedConfigs.Remove(configPath);
                    }

                    // Fire configuration changed event
                    FireConfigurationChanged(configPath, "*", null, scope, ConfigurationChangeType.Restored);
                }

                return restored;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore configuration for scope: {Scope}", scope);
                return false;
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the settings service and releases resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                // Dispose configuration watcher
                _configWatcher?.Dispose();

                // Clear loaded configs
                _loadedConfigs?.Clear();

                _logger.LogDebug("SettingsService disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing SettingsService");
            }
        }

        #endregion
    }
}
