using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Provides configuration validation services for HackerOS settings
    /// </summary>
    public class ConfigurationValidator
    {
        private readonly ILogger<ConfigurationValidator> _logger;
        private readonly Dictionary<string, ConfigurationValidationRule> _validationRules;

        public ConfigurationValidator(ILogger<ConfigurationValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validationRules = DefaultSystemConfiguration.GetValidationRules();
        }

        /// <summary>
        /// Validates a configuration value against its validation rule
        /// </summary>
        /// <param name="key">The configuration key</param>
        /// <param name="value">The value to validate</param>
        /// <returns>True if the value is valid; otherwise, false</returns>
        public bool ValidateValue(string key, object? value)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (!_validationRules.TryGetValue(key, out var rule))
            {
                // No validation rule defined - allow any value
                _logger.LogDebug("No validation rule found for key: {Key}", key);
                return true;
            }

            return ValidateValueAgainstRule(key, value, rule);
        }

        /// <summary>
        /// Validates an entire configuration dictionary
        /// </summary>
        /// <param name="configuration">The configuration to validate</param>
        /// <returns>A list of validation errors, empty if valid</returns>
        public List<ConfigurationValidationError> ValidateConfiguration(Dictionary<string, object?> configuration)
        {
            var errors = new List<ConfigurationValidationError>();

            // Check for required settings
            var requiredSettings = DefaultSystemConfiguration.GetRequiredSettings();
            foreach (var requiredKey in requiredSettings)
            {
                if (!configuration.ContainsKey(requiredKey) || configuration[requiredKey] == null)
                {
                    errors.Add(new ConfigurationValidationError
                    {
                        Key = requiredKey,
                        Message = $"Required configuration key '{requiredKey}' is missing or null",
                        ErrorType = ConfigurationValidationErrorType.Required
                    });
                }
            }

            // Validate each configuration value
            foreach (var kvp in configuration)
            {
                if (!ValidateValue(kvp.Key, kvp.Value))
                {
                    var rule = _validationRules.GetValueOrDefault(kvp.Key);
                    var errorMessage = rule?.ErrorMessage ?? $"Invalid value for configuration key '{kvp.Key}'";
                    
                    errors.Add(new ConfigurationValidationError
                    {
                        Key = kvp.Key,
                        Value = kvp.Value,
                        Message = errorMessage,
                        ErrorType = ConfigurationValidationErrorType.InvalidValue
                    });
                }
            }

            return errors;
        }

        /// <summary>
        /// Validates a configuration file content
        /// </summary>
        /// <param name="fileContent">The raw configuration file content</param>
        /// <param name="fileName">The name of the configuration file (for error reporting)</param>
        /// <returns>A list of validation errors, empty if valid</returns>
        public List<ConfigurationValidationError> ValidateConfigurationFile(string fileContent, string fileName = "config")
        {
            var errors = new List<ConfigurationValidationError>();

            try
            {
                var parser = new ConfigFileParser();
                var configuration = parser.ParseContent(fileContent);
                
                // Add file-specific errors
                errors.AddRange(ValidateConfiguration(configuration));
            }
            catch (Exception ex)
            {
                errors.Add(new ConfigurationValidationError
                {
                    Key = fileName,
                    Message = $"Failed to parse configuration file: {ex.Message}",
                    ErrorType = ConfigurationValidationErrorType.ParseError
                });
            }

            return errors;
        }

        /// <summary>
        /// Gets the default value for a configuration key
        /// </summary>
        /// <param name="key">The configuration key</param>
        /// <returns>The default value, or null if no default is defined</returns>
        public object? GetDefaultValue(string key)
        {
            var defaults = DefaultSystemConfiguration.GetDefaultValues();
            return defaults.GetValueOrDefault(key);
        }

        /// <summary>
        /// Applies default values to a configuration dictionary for any missing required keys
        /// </summary>
        /// <param name="configuration">The configuration to update</param>
        /// <returns>The configuration with default values applied</returns>
        public Dictionary<string, object?> ApplyDefaults(Dictionary<string, object?> configuration)
        {
            var result = new Dictionary<string, object?>(configuration);
            var defaults = DefaultSystemConfiguration.GetDefaultValues();
            var requiredSettings = DefaultSystemConfiguration.GetRequiredSettings();

            foreach (var requiredKey in requiredSettings)
            {
                if (!result.ContainsKey(requiredKey) && defaults.ContainsKey(requiredKey))
                {
                    result[requiredKey] = defaults[requiredKey];
                    _logger.LogDebug("Applied default value for required key: {Key}", requiredKey);
                }
            }

            return result;
        }        /// <summary>
        /// Validates configuration data asynchronously
        /// </summary>
        /// <param name="configData">Dictionary of configuration data</param>
        /// <param name="fileName">The name of the configuration file (for error reporting)</param>
        /// <returns>Configuration validation result</returns>
        public Task<ConfigurationValidationResult> ValidateConfigurationAsync(Dictionary<string, object> configData, string fileName = "config")
        {
            var errors = new List<ConfigurationValidationError>();

            // Convert to nullable dictionary for existing validation method
            var nullableConfigData = configData.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
            errors.AddRange(ValidateConfiguration(nullableConfigData));

            var result = new ConfigurationValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };

            return Task.FromResult(result);
        }

        private bool ValidateValueAgainstRule(string key, object? value, ConfigurationValidationRule rule)
        {
            // Check if required value is present
            if (rule.Required && value == null)
            {
                _logger.LogWarning("Required configuration value is null: {Key}", key);
                return false;
            }

            // Skip validation for null optional values
            if (value == null && !rule.Required)
                return true;

            // Type validation
            if (!IsCorrectType(value, rule.Type))
            {
                _logger.LogWarning("Configuration value has incorrect type for key: {Key}. Expected: {ExpectedType}, Actual: {ActualType}",
                    key, rule.Type.Name, value?.GetType().Name ?? "null");
                return false;
            }

            // Numeric range validation
            if (rule.MinValue != null || rule.MaxValue != null)
            {
                if (!ValidateNumericRange(value, rule.MinValue, rule.MaxValue))
                {
                    _logger.LogWarning("Configuration value out of range for key: {Key}. Value: {Value}, Min: {Min}, Max: {Max}",
                        key, value, rule.MinValue, rule.MaxValue);
                    return false;
                }
            }

            // String enumeration validation
            if (rule.AllowedValues != null && rule.AllowedValues.Length > 0)
            {
                if (!ValidateAllowedValues(value, rule.AllowedValues))
                {
                    _logger.LogWarning("Configuration value not in allowed list for key: {Key}. Value: {Value}, Allowed: [{AllowedValues}]",
                        key, value, string.Join(", ", rule.AllowedValues));
                    return false;
                }
            }

            // Pattern validation
            if (!string.IsNullOrEmpty(rule.Pattern))
            {
                if (!ValidatePattern(value, rule.Pattern))
                {
                    _logger.LogWarning("Configuration value does not match pattern for key: {Key}. Value: {Value}, Pattern: {Pattern}",
                        key, value, rule.Pattern);
                    return false;
                }
            }

            return true;
        }

        private bool IsCorrectType(object? value, Type expectedType)
        {
            if (value == null)
                return true; // Null check is handled separately

            var valueType = value.GetType();
            
            // Direct type match
            if (valueType == expectedType)
                return true;

            // Handle numeric conversions
            if (IsNumericType(expectedType) && IsNumericType(valueType))
                return true;

            // Handle string conversions
            if (expectedType == typeof(string))
                return true; // Any value can be converted to string

            // Handle boolean conversions
            if (expectedType == typeof(bool) && (valueType == typeof(string) || IsNumericType(valueType)))
                return true;

            return false;
        }

        private bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(float) || 
                   type == typeof(double) || type == typeof(decimal) || type == typeof(byte) ||
                   type == typeof(short) || type == typeof(uint) || type == typeof(ulong) ||
                   type == typeof(ushort) || type == typeof(sbyte);
        }

        private bool ValidateNumericRange(object? value, object? minValue, object? maxValue)
        {
            if (value == null)
                return true;

            try
            {
                var doubleValue = Convert.ToDouble(value);
                
                if (minValue != null)
                {
                    var minDouble = Convert.ToDouble(minValue);
                    if (doubleValue < minDouble)
                        return false;
                }

                if (maxValue != null)
                {
                    var maxDouble = Convert.ToDouble(maxValue);
                    if (doubleValue > maxDouble)
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ValidateAllowedValues(object? value, string[] allowedValues)
        {
            if (value == null)
                return true;

            var stringValue = value.ToString();
            return allowedValues.Contains(stringValue, StringComparer.OrdinalIgnoreCase);
        }

        private bool ValidatePattern(object? value, string pattern)
        {
            if (value == null)
                return true;

            try
            {
                var stringValue = value.ToString();
                var regex = new Regex(pattern);
                return regex.IsMatch(stringValue ?? string.Empty);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Represents the result of configuration validation
    /// </summary>
    public class ConfigurationValidationResult
    {
        /// <summary>
        /// Whether the configuration is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// List of validation errors
        /// </summary>
        public IEnumerable<ConfigurationValidationError> Errors { get; set; } = Array.Empty<ConfigurationValidationError>();

        /// <summary>
        /// Number of errors found
        /// </summary>
        public int ErrorCount => Errors?.Count() ?? 0;

        /// <summary>
        /// Configuration validation summary
        /// </summary>
        public string Summary => IsValid ? "Configuration is valid" : $"Configuration has {ErrorCount} error(s)";
    }    /// <summary>
    /// Represents a configuration validation error
    /// </summary>
    public class ConfigurationValidationError
    {
        /// <summary>
        /// The configuration section that failed validation
        /// </summary>
        public string Section { get; set; } = string.Empty;

        /// <summary>
        /// The configuration key that failed validation
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// The value that failed validation
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// A description of the validation error
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The type of validation error
        /// </summary>
        public ConfigurationValidationErrorType ErrorType { get; set; }

        /// <summary>
        /// Returns a string representation of the validation error
        /// </summary>
        public override string ToString()
        {
            return $"[{ErrorType}] {Key}: {Message}";
        }
    }    /// <summary>
    /// Types of configuration validation errors
    /// </summary>
    public enum ConfigurationValidationErrorType
    {
        /// <summary>
        /// A required configuration key is missing
        /// </summary>
        Required,

        /// <summary>
        /// A configuration value is invalid
        /// </summary>
        InvalidValue,

        /// <summary>
        /// Failed to parse the configuration file
        /// </summary>
        ParseError,

        /// <summary>
        /// Configuration schema error
        /// </summary>
        SchemaError,

        /// <summary>
        /// Configuration file is missing
        /// </summary>
        FileMissing,

        /// <summary>
        /// General validation error
        /// </summary>
        ValidationError
    }
}
