using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Parses Linux-style INI configuration files with support for sections, comments, and type conversion.
    /// Follows standard INI file conventions with # and ; comment support.
    /// </summary>
    public class ConfigFileParser
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections;

        /// <summary>
        /// Initializes a new instance of the ConfigFileParser class.
        /// </summary>
        public ConfigFileParser()
        {
            _sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets all section names in the configuration.
        /// </summary>
        public IEnumerable<string> Sections => _sections.Keys;

        /// <summary>
        /// Parses the content of an INI configuration file.
        /// </summary>
        /// <param name="content">The text content of the configuration file</param>
        /// <returns>True if parsing was successful</returns>
        public bool ParseContent(string content)
        {
            try
            {
                _sections.Clear();
                
                var lines = content.Split('\n', StringSplitOptions.None);
                string currentSection = string.Empty;

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();

                    // Skip empty lines and comments
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith(";"))
                        continue;

                    // Section header
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        if (!_sections.ContainsKey(currentSection))
                        {
                            _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        }
                        continue;
                    }

                    // Key-value pair
                    var equalIndex = line.IndexOf('=');
                    if (equalIndex > 0)
                    {
                        var key = line.Substring(0, equalIndex).Trim();
                        var value = line.Substring(equalIndex + 1).Trim();

                        // Remove quotes from value if present
                        if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                            (value.StartsWith("'") && value.EndsWith("'")))
                        {
                            value = value.Substring(1, value.Length - 2);
                        }

                        // Use global section if no section is currently active
                        if (string.IsNullOrEmpty(currentSection))
                        {
                            currentSection = "Global";
                            if (!_sections.ContainsKey(currentSection))
                            {
                                _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            }
                        }

                        _sections[currentSection][key] = value;
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a string value from the configuration.
        /// </summary>
        /// <param name="section">The section name</param>
        /// <param name="key">The key name</param>
        /// <param name="defaultValue">The default value if not found</param>
        /// <returns>The string value or default</returns>
        public string GetString(string section, string key, string defaultValue = "")
        {
            if (_sections.TryGetValue(section, out var sectionData) &&
                sectionData.TryGetValue(key, out var value))
            {
                return value;
            }
            return defaultValue;
        }

        /// <summary>
        /// Gets an integer value from the configuration.
        /// </summary>
        /// <param name="section">The section name</param>
        /// <param name="key">The key name</param>
        /// <param name="defaultValue">The default value if not found or invalid</param>
        /// <returns>The integer value or default</returns>
        public int GetInt(string section, string key, int defaultValue = 0)
        {
            var stringValue = GetString(section, key);
            if (int.TryParse(stringValue, out var result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// Gets a boolean value from the configuration.
        /// </summary>
        /// <param name="section">The section name</param>
        /// <param name="key">The key name</param>
        /// <param name="defaultValue">The default value if not found or invalid</param>
        /// <returns>The boolean value or default</returns>
        public bool GetBool(string section, string key, bool defaultValue = false)
        {
            var stringValue = GetString(section, key).ToLowerInvariant();
            if (stringValue == "true" || stringValue == "yes" || stringValue == "1" || stringValue == "on")
                return true;
            if (stringValue == "false" || stringValue == "no" || stringValue == "0" || stringValue == "off")
                return false;
            return defaultValue;
        }

        /// <summary>
        /// Gets a double value from the configuration.
        /// </summary>
        /// <param name="section">The section name</param>
        /// <param name="key">The key name</param>
        /// <param name="defaultValue">The default value if not found or invalid</param>
        /// <returns>The double value or default</returns>
        public double GetDouble(string section, string key, double defaultValue = 0.0)
        {
            var stringValue = GetString(section, key);
            if (double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// Gets an array of strings from the configuration (comma-separated values).
        /// </summary>
        /// <param name="section">The section name</param>
        /// <param name="key">The key name</param>
        /// <param name="defaultValue">The default array if not found</param>
        /// <returns>The string array or default</returns>
        public string[] GetStringArray(string section, string key, string[]? defaultValue = null)
        {
            var stringValue = GetString(section, key);
            if (!string.IsNullOrWhiteSpace(stringValue))
            {
                return stringValue.Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
            }
            return defaultValue ?? Array.Empty<string>();
        }        /// <summary>
        /// Gets a generic typed value from the configuration.
        /// </summary>
        /// <typeparam name="T">The type to convert to</typeparam>
        /// <param name="section">The section name</param>
        /// <param name="key">The key name</param>
        /// <param name="defaultValue">The default value if not found or conversion fails</param>
        /// <returns>The typed value or default</returns>
        public T? GetValue<T>(string section, string key, T? defaultValue = default)
        {            var stringValue = GetString(section, key);
            if (string.IsNullOrWhiteSpace(stringValue))
                return defaultValue;;

            try
            {
                var type = typeof(T);
                var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

                if (underlyingType == typeof(string))
                    return (T)(object)stringValue;
                else if (underlyingType == typeof(int))
                    return (T)(object)GetInt(section, key, Convert.ToInt32(defaultValue));
                else if (underlyingType == typeof(bool))
                    return (T)(object)GetBool(section, key, Convert.ToBoolean(defaultValue));
                else if (underlyingType == typeof(double))
                    return (T)(object)GetDouble(section, key, Convert.ToDouble(defaultValue));
                else if (underlyingType == typeof(float))
                    return (T)(object)(float)GetDouble(section, key, Convert.ToDouble(defaultValue));
                else if (underlyingType == typeof(long))
                    return (T)(object)(long)GetInt(section, key, Convert.ToInt32(defaultValue));
                else if (underlyingType.IsEnum)
                    return (T)Enum.Parse(underlyingType, stringValue, true);
                else
                {
                    // Try JSON deserialization for complex types
                    return JsonSerializer.Deserialize<T>(stringValue) ?? defaultValue;
                }
            }            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Sets a value in the configuration.
        /// </summary>
        /// <param name="section">The section name</param>
        /// <param name="key">The key name</param>
        /// <param name="value">The value to set</param>
        public void SetValue(string section, string key, object value)
        {
            if (!_sections.ContainsKey(section))
            {
                _sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            string stringValue;
            if (value is string str)
                stringValue = str;
            else if (value is bool boolVal)
                stringValue = boolVal ? "true" : "false";
            else if (value is IEnumerable<string> arrayVal)
                stringValue = string.Join(", ", arrayVal);
            else
                stringValue = value?.ToString() ?? string.Empty;

            _sections[section][key] = stringValue;
        }

        /// <summary>
        /// Gets all keys and values in a specific section.
        /// </summary>
        /// <param name="section">The section name</param>
        /// <returns>Dictionary of key-value pairs</returns>
        public Dictionary<string, string> GetSection(string section)
        {
            if (_sections.TryGetValue(section, out var sectionData))
            {
                return new Dictionary<string, string>(sectionData, StringComparer.OrdinalIgnoreCase);
            }
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Removes a section from the configuration.
        /// </summary>
        /// <param name="section">The section name to remove</param>
        /// <returns>True if the section was removed</returns>
        public bool RemoveSection(string section)
        {
            return _sections.Remove(section);
        }

        /// <summary>
        /// Removes a key from a section.
        /// </summary>
        /// <param name="section">The section name</param>
        /// <param name="key">The key name to remove</param>
        /// <returns>True if the key was removed</returns>
        public bool RemoveKey(string section, string key)
        {
            if (_sections.TryGetValue(section, out var sectionData))
            {
                return sectionData.Remove(key);
            }
            return false;
        }

        /// <summary>
        /// Serializes the configuration back to INI format.
        /// </summary>
        /// <returns>The INI formatted configuration content</returns>
        public string ToIniContent()
        {
            var builder = new StringBuilder();

            foreach (var section in _sections)
            {
                if (section.Key != "Global")
                {
                    builder.AppendLine($"[{section.Key}]");
                }

                foreach (var kvp in section.Value)
                {
                    var value = kvp.Value;
                    // Quote values that contain spaces or special characters
                    if (value.Contains(' ') || value.Contains('\t') || value.Contains('#') || value.Contains(';'))
                    {
                        value = $"\"{value}\"";
                    }
                    builder.AppendLine($"{kvp.Key}={value}");
                }

                builder.AppendLine(); // Empty line between sections
            }

            return builder.ToString();
        }

        /// <summary>
        /// Validates the syntax of an INI configuration file content.
        /// </summary>
        /// <param name="content">The configuration file content</param>
        /// <returns>True if the syntax is valid</returns>
        public static bool ValidateSyntax(string content)
        {
            try
            {
                var parser = new ConfigFileParser();
                return parser.ParseContent(content);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets all section names in the configuration.
        /// </summary>
        /// <returns>Collection of all section names</returns>
        public IEnumerable<string> GetAllSections()
        {
            return _sections.Keys;
        }
    }
}
