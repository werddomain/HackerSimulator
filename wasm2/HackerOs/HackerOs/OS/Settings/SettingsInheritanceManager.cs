using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Manages hierarchical settings inheritance and resolution
    /// </summary>
    public class SettingsInheritanceManager
    {
        private readonly ILogger<SettingsInheritanceManager> _logger;
        private readonly Dictionary<SettingScope, int> _scopePriority;

        public SettingsInheritanceManager(ILogger<SettingsInheritanceManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Define scope priority (higher number = higher priority)
            _scopePriority = new Dictionary<SettingScope, int>
            {
                { SettingScope.System, 1 },     // Lowest priority (system defaults)
                { SettingScope.User, 2 },       // Medium priority (user overrides)
                { SettingScope.Application, 3 } // Highest priority (app-specific)
            };
        }

        /// <summary>
        /// Resolves a setting value by checking all scopes in priority order
        /// </summary>
        /// <typeparam name="T">Type of the setting value</typeparam>
        /// <param name="key">Setting key</param>
        /// <param name="section">Configuration section (optional)</param>
        /// <param name="settingProviders">Dictionary of scope to setting providers</param>
        /// <param name="defaultValue">Default value if setting not found</param>
        /// <returns>Resolved setting value</returns>
        public T ResolveValue<T>(
            string key,
            string? section,
            Dictionary<SettingScope, ISettingsProvider> settingProviders,
            T defaultValue = default!)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Setting key cannot be null or empty", nameof(key));

            if (settingProviders == null)
                throw new ArgumentNullException(nameof(settingProviders));

            // Order scopes by priority (highest first)
            var orderedScopes = _scopePriority
                .Where(kvp => settingProviders.ContainsKey(kvp.Key))
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => kvp.Key);

            foreach (var scope in orderedScopes)
            {
                try
                {
                    var provider = settingProviders[scope];
                    if (provider.HasValue(key, section))
                    {
                        var value = provider.GetValue<T>(key, section, defaultValue);
                        _logger.LogDebug("Setting resolved from {Scope}: {Key} = {Value}", scope, key, value);
                        return value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading setting {Key} from {Scope}", key, scope);
                }
            }

            _logger.LogDebug("Setting not found in any scope, using default: {Key} = {DefaultValue}", key, defaultValue);
            return defaultValue;
        }

        /// <summary>
        /// Gets all effective settings by merging all scopes
        /// </summary>
        /// <param name="section">Configuration section (optional)</param>
        /// <param name="settingProviders">Dictionary of scope to setting providers</param>
        /// <returns>Dictionary of effective setting keys and values</returns>
        public Dictionary<string, object> GetEffectiveSettings(
            string? section,
            Dictionary<SettingScope, ISettingsProvider> settingProviders)
        {
            if (settingProviders == null)
                throw new ArgumentNullException(nameof(settingProviders));

            var effectiveSettings = new Dictionary<string, object>();

            // Order scopes by priority (lowest first, so higher priority overwrites)
            var orderedScopes = _scopePriority
                .Where(kvp => settingProviders.ContainsKey(kvp.Key))
                .OrderBy(kvp => kvp.Value)
                .Select(kvp => kvp.Key);

            foreach (var scope in orderedScopes)
            {
                try
                {
                    var provider = settingProviders[scope];
                    var scopeSettings = provider.GetAllSettings(section);

                    foreach (var setting in scopeSettings)
                    {
                        effectiveSettings[setting.Key] = setting.Value;
                        _logger.LogTrace("Setting {Key} = {Value} from {Scope}", setting.Key, setting.Value, scope);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading settings from {Scope}", scope);
                }
            }

            _logger.LogDebug("Resolved {Count} effective settings for section {Section}", effectiveSettings.Count, section ?? "root");
            return effectiveSettings;
        }

        /// <summary>
        /// Gets information about where a setting value is coming from
        /// </summary>
        /// <param name="key">Setting key</param>
        /// <param name="section">Configuration section (optional)</param>
        /// <param name="settingProviders">Dictionary of scope to setting providers</param>
        /// <returns>Setting resolution information</returns>
        public SettingResolutionInfo GetSettingSource(
            string key,
            string? section,
            Dictionary<SettingScope, ISettingsProvider> settingProviders)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Setting key cannot be null or empty", nameof(key));

            if (settingProviders == null)
                throw new ArgumentNullException(nameof(settingProviders));

            var sources = new List<SettingSourceInfo>();

            // Check all scopes for the setting
            foreach (var scope in Enum.GetValues<SettingScope>())
            {
                if (!settingProviders.TryGetValue(scope, out var provider))
                    continue;

                try
                {
                    if (provider.HasValue(key, section))
                    {
                        var value = provider.GetValue<object>(key, section);
                        sources.Add(new SettingSourceInfo
                        {
                            Scope = scope,
                            Value = value,
                            Priority = _scopePriority[scope]
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking setting {Key} in {Scope}", key, scope);
                }
            }

            // Find the effective source (highest priority)
            var effectiveSource = sources
                .OrderByDescending(s => s.Priority)
                .FirstOrDefault();

            return new SettingResolutionInfo
            {
                Key = key,
                Section = section,
                EffectiveSource = effectiveSource,
                AllSources = sources.AsReadOnly()
            };
        }

        /// <summary>
        /// Validates that setting overrides follow inheritance rules
        /// </summary>
        /// <param name="scope">Target scope for the setting</param>
        /// <param name="key">Setting key</param>
        /// <param name="section">Configuration section (optional)</param>
        /// <param name="settingProviders">Dictionary of scope to setting providers</param>
        /// <returns>True if the override is valid</returns>
        public bool ValidateOverride(
            SettingScope scope,
            string key,
            string? section,
            Dictionary<SettingScope, ISettingsProvider> settingProviders)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Setting key cannot be null or empty", nameof(key));

            if (settingProviders == null)
                throw new ArgumentNullException(nameof(settingProviders));

            // Application scope can override anything
            if (scope == SettingScope.Application)
                return true;

            // User scope can override system settings
            if (scope == SettingScope.User)
                return true;

            // System scope is the base - can't override anything above it
            if (scope == SettingScope.System)
            {
                // Check if higher priority scopes already have this setting
                var higherScopes = _scopePriority
                    .Where(kvp => kvp.Value > _scopePriority[scope])
                    .Select(kvp => kvp.Key);

                foreach (var higherScope in higherScopes)
                {
                    if (settingProviders.TryGetValue(higherScope, out var provider) &&
                        provider.HasValue(key, section))
                    {
                        _logger.LogWarning("Cannot set system setting {Key} - already overridden in {Scope}", key, higherScope);
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the priority order of setting scopes
        /// </summary>
        /// <returns>List of scopes ordered by priority (highest first)</returns>
        public IReadOnlyList<SettingScope> GetScopePriorityOrder()
        {
            return _scopePriority
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => kvp.Key)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Interface for objects that can provide settings values
    /// </summary>
    public interface ISettingsProvider
    {
        /// <summary>
        /// Checks if a setting value exists
        /// </summary>
        bool HasValue(string key, string? section = null);

        /// <summary>
        /// Gets a setting value
        /// </summary>
        T GetValue<T>(string key, string? section = null, T defaultValue = default!);

        /// <summary>
        /// Gets all settings in a section
        /// </summary>
        Dictionary<string, object> GetAllSettings(string? section = null);
    }

    /// <summary>
    /// Information about setting resolution
    /// </summary>
    public class SettingResolutionInfo
    {
        /// <summary>
        /// Setting key
        /// </summary>
        public required string Key { get; init; }

        /// <summary>
        /// Configuration section
        /// </summary>
        public string? Section { get; init; }

        /// <summary>
        /// The effective source (highest priority source that has this setting)
        /// </summary>
        public SettingSourceInfo? EffectiveSource { get; init; }

        /// <summary>
        /// All sources that have this setting
        /// </summary>
        public required IReadOnlyList<SettingSourceInfo> AllSources { get; init; }

        /// <summary>
        /// Gets the effective value
        /// </summary>
        public object? EffectiveValue => EffectiveSource?.Value;

        /// <summary>
        /// Gets the effective scope
        /// </summary>
        public SettingScope? EffectiveScope => EffectiveSource?.Scope;
    }

    /// <summary>
    /// Information about a setting source
    /// </summary>
    public class SettingSourceInfo
    {
        /// <summary>
        /// Setting scope
        /// </summary>
        public required SettingScope Scope { get; init; }

        /// <summary>
        /// Setting value
        /// </summary>
        public object? Value { get; init; }

        /// <summary>
        /// Priority level (higher = more important)
        /// </summary>
        public int Priority { get; init; }
    }
}
