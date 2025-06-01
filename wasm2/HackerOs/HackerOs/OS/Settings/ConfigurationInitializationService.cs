using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Service responsible for initializing the configuration directory structure
    /// and creating default configuration files in the VFS.
    /// </summary>
    public class ConfigurationInitializationService
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ConfigurationValidator _validator;
        private readonly ILogger<ConfigurationInitializationService> _logger;

        /// <summary>
        /// Initializes a new instance of the ConfigurationInitializationService.
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="validator">Configuration validator service</param>
        /// <param name="logger">Logger instance</param>
        public ConfigurationInitializationService(
            IVirtualFileSystem fileSystem,
            ConfigurationValidator validator,
            ILogger<ConfigurationInitializationService> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes the complete configuration directory structure and default files.
        /// </summary>
        /// <param name="forceRecreate">If true, recreates configuration files even if they exist</param>
        public async Task InitializeConfigurationAsync(bool forceRecreate = false)
        {
            try
            {
                _logger.LogInformation("Starting configuration initialization...");

                // Create directory structure
                await CreateDirectoryStructureAsync();

                // Initialize system configuration
                await InitializeSystemConfigurationAsync(forceRecreate);

                // Initialize user configuration template (for when users are created)
                await InitializeUserConfigurationTemplatesAsync(forceRecreate);

                _logger.LogInformation("Configuration initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize configuration");
                throw;
            }
        }

        /// <summary>
        /// Creates the required directory structure for configuration files.
        /// </summary>
        private async Task CreateDirectoryStructureAsync()
        {
            var directories = new[]
            {
                "/etc",                                    // System configuration directory
                "/etc/default",                           // Default system configurations
                "/home",                                   // User home directories base
                "/var",                                    // Variable data directory
                "/var/log",                               // Log files directory
                "/var/backups",                           // Configuration backups
                "/var/backups/config"                     // Configuration backup storage
            };

            foreach (var dir in directories)
            {
                try
                {
                    if (!await _fileSystem.ExistsAsync(dir))
                    {
                        await _fileSystem.CreateDirectoryAsync(dir);
                        _logger.LogDebug("Created directory: {Directory}", dir);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create directory: {Directory}", dir);
                }
            }
        }

        /// <summary>
        /// Initializes the system-wide configuration file (/etc/hackeros.conf).
        /// </summary>
        /// <param name="forceRecreate">If true, recreates the file even if it exists</param>
        private async Task InitializeSystemConfigurationAsync(bool forceRecreate)
        {
            const string systemConfigPath = "/etc/hackeros.conf";

            try
            {
                // Check if file exists and if we should skip creation
                if (!forceRecreate && await _fileSystem.ExistsAsync(systemConfigPath))
                {
                    _logger.LogDebug("System configuration file already exists: {Path}", systemConfigPath);
                    
                    // Validate existing configuration
                    await ValidateExistingConfigurationAsync(systemConfigPath);
                    return;
                }

                // Create the system configuration file with template content
                var configContent = DefaultSystemConfiguration.GetSystemConfigTemplate();
                  // Validate the template content before writing
                var validationResult = await _validator.ValidateConfigurationContentAsync(configContent);
                if (validationResult.Count > 0)
                {
                    _logger.LogWarning("System configuration template failed validation: {Errors}", 
                        string.Join(", ", validationResult.Select(e => e.Message)));
                }

                // Write the configuration file
                await _fileSystem.WriteTextAsync(systemConfigPath, configContent);
                _logger.LogInformation("Created system configuration file: {Path}", systemConfigPath);

                // Apply default values for any missing settings
                await ApplyDefaultValuesAsync(systemConfigPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize system configuration: {Path}", systemConfigPath);
                throw;
            }
        }

        /// <summary>
        /// Initializes user configuration templates that will be used when creating new users.
        /// </summary>
        /// <param name="forceRecreate">If true, recreates templates even if they exist</param>
        private async Task InitializeUserConfigurationTemplatesAsync(bool forceRecreate)
        {
            const string templateDir = "/etc/default/user-config";
            
            try
            {
                // Create template directory
                if (!await _fileSystem.ExistsAsync(templateDir))
                {
                    await _fileSystem.CreateDirectoryAsync(templateDir);
                }

                // Configuration templates to create
                var templates = new Dictionary<string, Func<string>>
                {
                    ["user.conf"] = DefaultSystemConfiguration.GetUserConfigTemplate,
                    ["desktop.conf"] = DefaultSystemConfiguration.GetDesktopConfigTemplate,
                    ["theme.conf"] = DefaultSystemConfiguration.GetThemeConfigTemplate
                };

                foreach (var template in templates)
                {
                    var templatePath = $"{templateDir}/{template.Key}";
                    
                    if (!forceRecreate && await _fileSystem.ExistsAsync(templatePath))
                    {
                        _logger.LogDebug("User configuration template already exists: {Path}", templatePath);
                        continue;
                    }

                    var content = template.Value();
                    await _fileSystem.WriteTextAsync(templatePath, content);
                    _logger.LogInformation("Created user configuration template: {Path}", templatePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize user configuration templates");
                throw;
            }
        }

        /// <summary>
        /// Initializes configuration for a specific user by copying templates.
        /// </summary>
        /// <param name="username">The username to initialize configuration for</param>
        public async Task InitializeUserConfigurationAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty", nameof(username));

            try
            {
                var userConfigDir = $"/home/{username}/.config/hackeros";
                var templateDir = "/etc/default/user-config";

                // Create user's configuration directory
                if (!await _fileSystem.ExistsAsync(userConfigDir))
                {
                    await _fileSystem.CreateDirectoryAsync(userConfigDir);
                }

                // Copy configuration templates
                var configFiles = new[] { "user.conf", "desktop.conf", "theme.conf" };

                foreach (var configFile in configFiles)
                {
                    var templatePath = $"{templateDir}/{configFile}";
                    var userConfigPath = $"{userConfigDir}/{configFile}";

                    // Skip if user config already exists
                    if (await _fileSystem.ExistsAsync(userConfigPath))
                    {
                        _logger.LogDebug("User configuration already exists: {Path}", userConfigPath);
                        continue;
                    }

                    // Copy template if it exists
                    if (await _fileSystem.ExistsAsync(templatePath))
                    {
                        var templateContent = await _fileSystem.ReadTextAsync(templatePath);
                        await _fileSystem.WriteTextAsync(userConfigPath, templateContent);
                        _logger.LogInformation("Created user configuration: {Path}", userConfigPath);
                    }
                    else
                    {
                        _logger.LogWarning("Template not found for user configuration: {Template}", templatePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize configuration for user: {Username}", username);
                throw;
            }
        }

        /// <summary>
        /// Validates an existing configuration file and applies missing default values.
        /// </summary>
        /// <param name="configPath">Path to the configuration file</param>
        private async Task ValidateExistingConfigurationAsync(string configPath)
        {
            try
            {
                var content = await _fileSystem.ReadTextAsync(configPath);
                var validationResult = await _validator.ValidateConfigurationContentAsync(content);                if (validationResult.Count > 0)
                {
                    _logger.LogWarning("Configuration file has validation errors: {Path}, Errors: {Errors}",
                        configPath, string.Join(", ", validationResult.Select(e => e.Message)));
                }

                // Apply any missing default values
                await ApplyDefaultValuesAsync(configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate existing configuration: {Path}", configPath);
            }
        }

        /// <summary>
        /// Applies default values for any missing required settings in a configuration file.
        /// </summary>
        /// <param name="configPath">Path to the configuration file</param>
        private async Task ApplyDefaultValuesAsync(string configPath)
        {
            try
            {
                var parser = new ConfigFileParser();
                
                // Load existing configuration
                if (await _fileSystem.ExistsAsync(configPath))
                {
                    var content = await _fileSystem.ReadTextAsync(configPath);
                    parser.ParseContent(content);
                }

                // Get default values and required settings
                var defaultValues = DefaultSystemConfiguration.GetDefaultValues();
                var requiredSettings = DefaultSystemConfiguration.GetRequiredSettings();
                bool configModified = false;

                // Check each required setting
                foreach (var requiredKey in requiredSettings)
                {
                    // Parse the key to get section and setting name
                    var keyParts = requiredKey.Split('.');
                    if (keyParts.Length != 2) continue;
                    
                    var section = keyParts[0];
                    var key = keyParts[1];

                    // Check if setting exists
                    try
                    {
                        var existingValue = parser.GetValue<string>(section, key, null);
                        if (existingValue == null && defaultValues.ContainsKey(requiredKey))
                        {
                            // Apply default value
                            parser.SetValue(section, key, defaultValues[requiredKey].ToString() ?? "");
                            configModified = true;
                            _logger.LogDebug("Applied default value for {Section}.{Key}: {Value}", 
                                section, key, defaultValues[requiredKey]);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to check/set default value for {Section}.{Key}", section, key);
                    }
                }

                // Save configuration if modified
                if (configModified)
                {
                    var updatedContent = parser.ToConfigString();
                    await _fileSystem.WriteTextAsync(configPath, updatedContent);
                    _logger.LogInformation("Applied missing default values to configuration: {Path}", configPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply default values to configuration: {Path}", configPath);
            }
        }

        /// <summary>
        /// Gets the current status of configuration initialization.
        /// </summary>
        /// <returns>A summary of the configuration initialization status</returns>
        public async Task<ConfigurationInitializationStatus> GetInitializationStatusAsync()
        {
            var status = new ConfigurationInitializationStatus();

            try
            {
                // Check system configuration
                status.SystemConfigExists = await _fileSystem.ExistsAsync("/etc/hackeros.conf");
                
                // Check template directory
                status.UserTemplatesExist = await _fileSystem.ExistsAsync("/etc/default/user-config");
                
                // Check required directories
                var requiredDirs = new[] { "/etc", "/var/log", "/var/backups/config" };
                status.RequiredDirectoriesExist = true;
                
                foreach (var dir in requiredDirs)
                {
                    if (!await _fileSystem.ExistsAsync(dir))
                    {
                        status.RequiredDirectoriesExist = false;
                        break;
                    }
                }

                status.IsFullyInitialized = status.SystemConfigExists && 
                                          status.UserTemplatesExist && 
                                          status.RequiredDirectoriesExist;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get configuration initialization status");
                status.IsFullyInitialized = false;
            }

            return status;
        }
    }

    /// <summary>
    /// Represents the status of configuration initialization.
    /// </summary>
    public class ConfigurationInitializationStatus
    {
        /// <summary>
        /// Gets or sets whether the system configuration file exists.
        /// </summary>
        public bool SystemConfigExists { get; set; }

        /// <summary>
        /// Gets or sets whether user configuration templates exist.
        /// </summary>
        public bool UserTemplatesExist { get; set; }

        /// <summary>
        /// Gets or sets whether all required directories exist.
        /// </summary>
        public bool RequiredDirectoriesExist { get; set; }

        /// <summary>
        /// Gets or sets whether the configuration is fully initialized.
        /// </summary>
        public bool IsFullyInitialized { get; set; }
    }
}
