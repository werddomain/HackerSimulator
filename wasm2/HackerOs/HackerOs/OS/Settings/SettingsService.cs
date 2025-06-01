using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HackerOs.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Settings
{
    public class SettingsService : ISettingsService, ISettingsProvider, IDisposable
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILogger<SettingsService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly Dictionary<string, ConfigFileParser> _loadedConfigs;
        private readonly Dictionary<SettingScope, string> _configPaths;
        private readonly ConfigurationWatcher _configWatcher;
        private readonly SettingsInheritanceManager _inheritanceManager;
        private readonly Dictionary<SettingScope, ISettingsProvider> _scopeProviders;
        
        // Phase 2.1.3 services
        private readonly ConfigurationValidator _validator;
        private readonly ConfigurationBackupService _backupService;
        private readonly ConfigurationInitializationService _initializationService;
        
        private bool _disposed = false;

        public event EventHandler<ConfigurationChangedEventArgs>? OnConfigurationChanged;

        public SettingsService(IVirtualFileSystem fileSystem, ILogger<SettingsService> logger, ILoggerFactory loggerFactory)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            
            _loadedConfigs = new Dictionary<string, ConfigFileParser>();
            _configPaths = new Dictionary<SettingScope, string>
            {
                [SettingScope.System] = "/etc/hackeros.conf",
                [SettingScope.User] = "~/.config/hackeros/user.conf",
                [SettingScope.Application] = "~/.config/applications/"
            };

            // Initialize Phase 2.1.3 services
            _validator = new ConfigurationValidator(_loggerFactory.CreateLogger<ConfigurationValidator>());
            _backupService = new ConfigurationBackupService(_fileSystem, _loggerFactory.CreateLogger<ConfigurationBackupService>());
            _initializationService = new ConfigurationInitializationService(_fileSystem, _validator, _loggerFactory.CreateLogger<ConfigurationInitializationService>());

            // Initialize other services
            _configWatcher = new ConfigurationWatcher(_fileSystem, _loggerFactory.CreateLogger<ConfigurationWatcher>());
            _inheritanceManager = new SettingsInheritanceManager(_loggerFactory.CreateLogger<SettingsInheritanceManager>());
            
            _scopeProviders = new Dictionary<SettingScope, ISettingsProvider>
            {
                [SettingScope.System] = this,
                [SettingScope.User] = this,
                [SettingScope.Application] = this
            };

            _configWatcher.ConfigurationFileChanged += OnConfigurationFileChanged;
            _ = Task.Run(InitializeServiceAsync);
        }

        public async Task<T?> GetSettingAsync<T>(string section, string key, T? defaultValue = default, SettingScope scope = SettingScope.User)
        {
            try
            {
                var configPath = GetConfigPath(scope);
                var parser = await LoadConfigParserAsync(configPath);
                return parser?.GetValue(section, key, defaultValue) ?? defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get setting {Section}.{Key} from scope {Scope}", section, key, scope);
                return defaultValue;
            }
        }        public async Task<bool> SetSettingAsync<T>(string section, string key, T value, SettingScope scope = SettingScope.User)
        {
            try
            {
                var configPath = GetConfigPath(scope);
                var parser = await LoadConfigParserAsync(configPath) ?? new ConfigFileParser();
                
                parser.SetValue(section, key, value!);
                await SaveConfigurationAsync(configPath, parser);
                _loadedConfigs[configPath] = parser;
                
                OnConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(
                    configPath, section, key, scope, ConfigurationChangeType.Modified));
                    
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set setting {Section}.{Key} in scope {Scope}", section, key, scope);
                return false;
            }
        }

        public async Task<T?> GetInheritedSettingAsync<T>(string section, string key, T? defaultValue = default)
        {
            return await _inheritanceManager.GetInheritedSettingAsync(section, key, _scopeProviders, defaultValue);
        }

        public async Task<Dictionary<string, string>?> GetSectionAsync(string section, SettingScope scope = SettingScope.User)
        {
            try
            {
                var configPath = GetConfigPath(scope);
                var parser = await LoadConfigParserAsync(configPath);
                return parser?.GetSection(section);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get section {Section} from scope {Scope}", section, scope);
                return null;
            }
        }

        public async Task<IEnumerable<string>?> GetSectionNamesAsync(SettingScope scope = SettingScope.User)
        {
            try
            {
                var configPath = GetConfigPath(scope);
                var parser = await LoadConfigParserAsync(configPath);
                return parser?.GetAllSections();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get section names from scope {Scope}", scope);
                return null;
            }
        }

        public async Task<bool> RemoveSettingAsync(string section, string key, SettingScope scope = SettingScope.User)
        {
            try
            {
                var configPath = GetConfigPath(scope);
                var parser = await LoadConfigParserAsync(configPath);
                
                if (parser != null && parser.RemoveValue(section, key))
                {
                    await SaveConfigurationAsync(configPath, parser);
                    OnConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(
                        configPath, ConfigurationChangeType.Deleted, section, key, null));
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove setting {Section}.{Key} from scope {Scope}", section, key, scope);
                return false;
            }
        }

        public async Task<bool> RemoveSectionAsync(string section, SettingScope scope = SettingScope.User)
        {
            try
            {
                var configPath = GetConfigPath(scope);
                var parser = await LoadConfigParserAsync(configPath);
                
                if (parser != null && parser.RemoveSection(section))
                {
                    await SaveConfigurationAsync(configPath, parser);
                    OnConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(
                        configPath, ConfigurationChangeType.Deleted, section, null, null));
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove section {Section} from scope {Scope}", section, scope);
                return false;
            }
        }

        public async Task<bool> CreateBackupAsync(string backupName)
        {
            try
            {
                await _backupService.CreateBackupAsync(backupName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create backup {BackupName}", backupName);
                return false;
            }
        }

        public async Task<bool> RestoreBackupAsync(string backupName)
        {
            try
            {
                await _backupService.RestoreBackupAsync(backupName);
                _loadedConfigs.Clear();
                OnConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(
                    "system", ConfigurationChangeType.Restored, "backup", "name", backupName));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore backup {BackupName}", backupName);
                return false;
            }
        }

        public async Task<ConfigurationValidationResult> ValidateConfigurationAsync(SettingScope scope = SettingScope.User)
        {
            try
            {
                var configPath = GetConfigPath(scope);
                var parser = await LoadConfigParserAsync(configPath);
                
                if (parser != null)
                {
                    return await _validator.ValidateConfigurationAsync(parser.GetAllData(), configPath);
                }

                return new ConfigurationValidationResult 
                { 
                    IsValid = false, 
                    Errors = new[] { new ConfigurationValidationError
                    {
                        ErrorType = ConfigurationValidationErrorType.FileMissing,
                        Message = $"Configuration file not found: {configPath}",
                        Section = "",
                        Key = "",
                        Value = ""
                    }}
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate configuration for scope {Scope}", scope);
                return new ConfigurationValidationResult 
                { 
                    IsValid = false, 
                    Errors = new[] { new ConfigurationValidationError
                    {
                        ErrorType = ConfigurationValidationErrorType.ValidationError,
                        Message = ex.Message,
                        Section = "",
                        Key = "",
                        Value = ""
                    }}
                };
            }
        }

        private async Task InitializeServiceAsync()
        {
            try
            {
                _logger.LogInformation("Initializing SettingsService...");
                await _initializationService.InitializeConfigurationStructureAsync();
                
                foreach (var configPath in _configPaths.Values)
                {
                    await _configWatcher.StartWatchingAsync(configPath);
                }

                _logger.LogInformation("SettingsService initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize SettingsService");
            }
        }

        private string GetConfigPath(SettingScope scope)
        {
            return _configPaths.TryGetValue(scope, out var path) ? path : _configPaths[SettingScope.User];
        }

        private async Task<ConfigFileParser?> LoadConfigParserAsync(string configPath)
        {
            try
            {
                if (_loadedConfigs.TryGetValue(configPath, out var cachedParser))
                {
                    return cachedParser;
                }

                if (await _fileSystem.FileExistsAsync(configPath))
                {
                    var content = await _fileSystem.ReadAllTextAsync(configPath);
                    var parser = new ConfigFileParser();
                    parser.LoadFromString(content);
                    _loadedConfigs[configPath] = parser;
                    return parser;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration parser for {ConfigPath}", configPath);
                return null;
            }
        }

        private async Task SaveConfigurationAsync(string configPath, ConfigFileParser parser)
        {
            try
            {
                var content = parser.SaveToString();
                var directory = Path.GetDirectoryName(configPath);
                
                if (!string.IsNullOrEmpty(directory) && !await _fileSystem.DirectoryExistsAsync(directory))
                {
                    await _fileSystem.CreateDirectoryAsync(directory);
                }

                await _fileSystem.WriteAllTextAsync(configPath, content);
                _logger.LogDebug("Saved configuration file: {ConfigPath}", configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration file: {ConfigPath}", configPath);
                throw;
            }
        }

        private void OnConfigurationFileChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            try
            {
                if (_loadedConfigs.ContainsKey(e.FilePath))
                {
                    _loadedConfigs.Remove(e.FilePath);
                }

                OnConfigurationChanged?.Invoke(this, e);
                _logger.LogDebug("Configuration file changed: {FilePath}", e.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling configuration file change: {FilePath}", e.FilePath);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    _configWatcher?.Dispose();
                    _backupService?.Dispose();
                    _loadedConfigs?.Clear();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing SettingsService");
                }

                _disposed = true;
            }
        }
    }
}
