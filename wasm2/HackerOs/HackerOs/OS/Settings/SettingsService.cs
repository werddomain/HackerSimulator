using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Settings
{
    public class SettingsService : ISettingsService, ISettingsProvider, IDisposable
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILogger<SettingsService> _logger;
        private readonly Dictionary<string, ConfigFileParser> _loadedConfigs;
        private readonly Dictionary<SettingScope, string> _configPaths;
        
        private bool _disposed = false;

        public event EventHandler<ConfigurationChangedEventArgs>? OnConfigurationChanged;

        public SettingsService(IVirtualFileSystem fileSystem, ILogger<SettingsService> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _loadedConfigs = new Dictionary<string, ConfigFileParser>();
            _configPaths = new Dictionary<SettingScope, string>
            {
                [SettingScope.System] = "/etc/hackeros.conf",
                [SettingScope.User] = "~/.config/hackeros/user.conf",
                [SettingScope.Application] = "~/.config/applications/"
            };

            // Initialize the service
            _ = Task.Run(InitializeServiceAsync);
        }

        private async Task InitializeServiceAsync()
        {
            try
            {
                // Create default config directories if they don't exist
                await CreateDefaultConfigsAsync(SettingScope.System);
                await CreateDefaultConfigsAsync(SettingScope.User);
                await CreateDefaultConfigsAsync(SettingScope.Application);
                
                _logger.LogInformation("Settings service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize settings service");
            }
        }        public async Task<T?> GetSettingAsync<T>(string section, string key, T? defaultValue = default, SettingScope scope = SettingScope.User)
        {
            try
            {
                var configPath = GetConfigPath(scope);                var parser = await LoadConfigParserAsync(configPath);
                if (parser == null)
                    return defaultValue;
                
                return parser.GetValue(section, key, defaultValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get setting {Section}.{Key} from scope {Scope}", section, key, scope);
                return defaultValue;
            }
        }

        public async Task<bool> SetSettingAsync<T>(string section, string key, T value, SettingScope scope = SettingScope.User)
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

        public async Task<Dictionary<string, object>> GetSectionAsync(string section, SettingScope scope = SettingScope.User)
        {
            try
            {
                var configPath = GetConfigPath(scope);
                var parser = await LoadConfigParserAsync(configPath);
                var sectionData = parser?.GetSection(section);
                
                var result = new Dictionary<string, object>();
                if (sectionData != null)
                {
                    foreach (var kvp in sectionData)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get section {Section} from scope {Scope}", section, scope);
                return new Dictionary<string, object>();
            }
        }

        public async Task<T?> GetEffectiveSettingAsync<T>(string section, string key, SettingScope maxScope = SettingScope.User, T? defaultValue = default)
        {
            try
            {
                // Check scopes in order: User -> System
                var scopes = new[] { SettingScope.User, SettingScope.System };
                
                foreach (var scope in scopes.Where(s => s <= maxScope))
                {
                    var result = await GetSettingAsync(section, key, defaultValue, scope);
                    if (result != null && !result.Equals(defaultValue))
                    {
                        return result;
                    }
                }
                
                return defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get effective setting {Section}.{Key}", section, key);
                return defaultValue;
            }
        }

        public async Task<bool> LoadConfigFileAsync(string configPath)
        {            try
            {
                if (string.IsNullOrWhiteSpace(configPath))
                    return false;

                var currentUser = new HackerOs.OS.User.User { Username = "user", UserId = 1000 };
                
                // Check if file exists
                if (!await _fileSystem.FileExistsAsync(configPath, currentUser))
                {
                    _logger.LogWarning("Config file does not exist: {ConfigPath}", configPath);
                    return false;
                }                // Read file content
                var content = await _fileSystem.ReadFileAsync(configPath, currentUser);
                if (content == null)
                {
                    _logger.LogError("Failed to read content from config file: {ConfigPath}", configPath);
                    return false;
                }
                
                var parser = new ConfigFileParser();
                
                if (parser.ParseContent(content))
                {
                    _loadedConfigs[configPath] = parser;
                    _logger.LogDebug("Successfully loaded config file: {ConfigPath}", configPath);
                    return true;
                }
                
                _logger.LogError("Failed to parse config file: {ConfigPath}", configPath);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading config file: {ConfigPath}", configPath);
                return false;
            }
        }

        public async Task<bool> SaveConfigFileAsync(string configPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(configPath) || !_loadedConfigs.ContainsKey(configPath))
                    return false;                var parser = _loadedConfigs[configPath];
                var content = parser.ToIniContent();
                
                var currentUser = new HackerOs.OS.User.User { Username = "user", UserId = 1000 };
                await _fileSystem.WriteFileAsync(configPath, content, currentUser);
                
                _logger.LogDebug("Successfully saved config file: {ConfigPath}", configPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving config file: {ConfigPath}", configPath);
                return false;
            }
        }

        public async Task<bool> ReloadAllConfigsAsync()
        {
            try
            {
                var loadTasks = _loadedConfigs.Keys.Select(LoadConfigFileAsync).ToArray();
                var results = await Task.WhenAll(loadTasks);
                
                var successCount = results.Count(r => r);
                _logger.LogInformation("Reloaded {SuccessCount}/{TotalCount} config files", successCount, results.Length);
                
                return successCount == results.Length;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading config files");
                return false;
            }
        }

        public async Task<bool> ValidateConfigAsync(string configPath)
        {
            try
            {                if (string.IsNullOrWhiteSpace(configPath))
                    return false;

                var currentUser = new HackerOs.OS.User.User { Username = "user", UserId = 1000 };
                  if (!await _fileSystem.FileExistsAsync(configPath, currentUser))
                    return false;

                var content = await _fileSystem.ReadFileAsync(configPath, currentUser);
                return content != null && ConfigFileParser.ValidateSyntax(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating config file: {ConfigPath}", configPath);
                return false;
            }
        }

        public async Task<IEnumerable<string>> GetConfigFilesAsync(SettingScope scope)
        {            try
            {
                var basePath = GetConfigPath(scope);
                var currentUser = new HackerOs.OS.User.User { Username = "user", UserId = 1000 };
                
                if (scope == SettingScope.Application)
                {
                    // For application scope, return all .conf files in the applications directory
                    if (!await _fileSystem.DirectoryExistsAsync(basePath, currentUser))
                        return Enumerable.Empty<string>();

                    var entries = await _fileSystem.ListDirectoryAsync(basePath, currentUser);
                    return entries
                        .Where(e => !e.IsDirectory && e.Name.EndsWith(".conf", StringComparison.OrdinalIgnoreCase))
                        .Select(e => Path.Combine(basePath, e.Name));
                }
                else
                {
                    // For system and user scopes, return the single config file if it exists
                    if (await _fileSystem.FileExistsAsync(basePath, currentUser))
                        return new[] { basePath };
                    return Enumerable.Empty<string>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting config files for scope: {Scope}", scope);
                return Enumerable.Empty<string>();
            }
        }

        public async Task<bool> CreateDefaultConfigsAsync(SettingScope scope)
        {            try
            {
                var configPath = GetConfigPath(scope);
                var currentUser = new HackerOs.OS.User.User { Username = "user", UserId = 1000 };
                
                if (scope == SettingScope.Application)
                {
                    // Create applications directory
                    if (!await _fileSystem.DirectoryExistsAsync(configPath, currentUser))
                    {
                        await _fileSystem.CreateDirectoryAsync(configPath, currentUser);
                        _logger.LogInformation("Created applications config directory: {ConfigPath}", configPath);
                    }
                }
                else
                {
                    // Create config file if it doesn't exist
                    if (!await _fileSystem.FileExistsAsync(configPath, currentUser))
                    {
                        // Ensure directory exists
                        var directory = Path.GetDirectoryName(configPath);
                        if (!string.IsNullOrEmpty(directory) && !await _fileSystem.DirectoryExistsAsync(directory, currentUser))
                        {
                            await _fileSystem.CreateDirectoryAsync(directory, currentUser);
                        }

                        // Create default config content
                        var defaultContent = scope == SettingScope.System
                            ? CreateDefaultSystemConfig()
                            : CreateDefaultUserConfig();
                        
                        await _fileSystem.WriteFileAsync(configPath, defaultContent, currentUser);
                        _logger.LogInformation("Created default config file: {ConfigPath}", configPath);
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating default configs for scope: {Scope}", scope);
                return false;
            }
        }

        // ISettingsProvider implementation
        public bool HasValue(string section, string? key = null)
        {
            try
            {
                // For simplicity, check user scope first
                var configPath = GetConfigPath(SettingScope.User);
                if (_loadedConfigs.TryGetValue(configPath, out var parser))
                {
                    if (key == null)
                    {
                        return parser.GetAllSections().Contains(section);
                    }
                    else
                    {
                        var sectionData = parser.GetSection(section);
                        return sectionData.ContainsKey(key);
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }        public T GetValue<T>(string section, string? key, T defaultValue = default!)
        {
            try
            {
                // For simplicity, check user scope first
                var configPath = GetConfigPath(SettingScope.User);
                if (_loadedConfigs.TryGetValue(configPath, out var parser) && key != null)
                {
                    var result = parser.GetValue(section, key, defaultValue);
                    return result != null ? result : defaultValue;
                }
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public Dictionary<string, object> GetAllSettings(string? section = null)
        {
            try
            {
                var result = new Dictionary<string, object>();
                var configPath = GetConfigPath(SettingScope.User);
                
                if (_loadedConfigs.TryGetValue(configPath, out var parser))
                {
                    if (section == null)
                    {
                        // Return all settings from all sections
                        foreach (var sectionName in parser.GetAllSections())
                        {
                            var sectionData = parser.GetSection(sectionName);
                            foreach (var kvp in sectionData)
                            {
                                result[$"{sectionName}.{kvp.Key}"] = kvp.Value;
                            }
                        }
                    }
                    else
                    {
                        // Return settings from specific section
                        var sectionData = parser.GetSection(section);
                        foreach (var kvp in sectionData)
                        {
                            result[kvp.Key] = kvp.Value;
                        }
                    }
                }
                
                return result;
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private string GetConfigPath(SettingScope scope)
        {
            return _configPaths[scope];
        }

        private async Task<ConfigFileParser?> LoadConfigParserAsync(string configPath)
        {
            try
            {                if (_loadedConfigs.TryGetValue(configPath, out var cachedParser))
                {
                    return cachedParser;
                }

                var currentUser = new HackerOs.OS.User.User { Username = "user", UserId = 1000 };
                  if (!await _fileSystem.FileExistsAsync(configPath, currentUser))
                {
                    return null;
                }

                var content = await _fileSystem.ReadFileAsync(configPath, currentUser);
                if (content == null)
                {
                    return null;
                }
                
                var parser = new ConfigFileParser();
                
                if (parser.ParseContent(content))
                {
                    _loadedConfigs[configPath] = parser;
                    return parser;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading config parser for: {ConfigPath}", configPath);
                return null;
            }
        }

        private async Task SaveConfigurationAsync(string configPath, ConfigFileParser parser)
        {            try
            {
                var content = parser.ToIniContent();
                var currentUser = new HackerOs.OS.User.User { Username = "user", UserId = 1000 };
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(directory) && !await _fileSystem.DirectoryExistsAsync(directory, currentUser))
                {
                    await _fileSystem.CreateDirectoryAsync(directory, currentUser);
                }

                await _fileSystem.WriteFileAsync(configPath, content, currentUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving configuration to: {ConfigPath}", configPath);
                throw;
            }
        }

        private static string CreateDefaultSystemConfig()
        {
            return @"# HackerOS System Configuration
# This file contains system-wide settings

[System]
# Default system locale
Locale=en_US.UTF-8

# Default timezone
Timezone=UTC

# System logging level
LogLevel=Information

[Security]
# Password policy settings
MinPasswordLength=8
RequireSpecialChars=true

[Network]
# Default network settings
DefaultDNS=8.8.8.8,8.8.4.4
";
        }

        private static string CreateDefaultUserConfig()
        {
            return @"# HackerOS User Configuration
# This file contains user-specific settings

[Appearance]
# UI theme (Dark, Light, Auto)
Theme=Dark

# Accent color
AccentColor=#0078d7

# Font size
FontSize=14

[Desktop]
# Desktop background
Background=default

# Show desktop icons
ShowDesktopIcons=true

[Terminal]
# Default shell
Shell=/bin/bash

# Terminal font family
FontFamily=Consolas

# Terminal font size
FontSize=12
";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _loadedConfigs.Clear();
                _disposed = true;
            }
        }
    }
}
