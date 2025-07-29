using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace HackerOs.OS.Core.Settings
{
    /// <summary>
    /// Default implementation of ISettingsService using browser local storage
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly ILogger<SettingsService> _logger;
        private readonly IJSRuntime _jsRuntime;
        private readonly Dictionary<string, object> _settings;
        private readonly JsonSerializerOptions _jsonOptions;
        
        private const string StorageKey = "hackeros_settings";

        public event EventHandler<SettingChangedEventArgs>? SettingChanged;

        public SettingsService(ILogger<SettingsService> logger, IJSRuntime jsRuntime)
        {
            _logger = logger;
            _jsRuntime = jsRuntime;
            _settings = new Dictionary<string, object>();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        public async Task<T> GetSettingAsync<T>(string key, T defaultValue = default!)
        {
            try
            {
                if (_settings.TryGetValue(key, out var value))
                {
                    if (value is JsonElement jsonElement)
                    {
                        return jsonElement.Deserialize<T>(_jsonOptions) ?? defaultValue;
                    }
                    
                    if (value is T directValue)
                    {
                        return directValue;
                    }
                    
                    // Try to convert
                    if (value != null)
                    {
                        var json = JsonSerializer.Serialize(value, _jsonOptions);
                        return JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? defaultValue;
                    }
                }
                
                return defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting setting: {Key}", key);
                return defaultValue;
            }
        }

        public async Task SetSettingAsync<T>(string key, T value)
        {
            try
            {
                var oldValue = _settings.TryGetValue(key, out var existing) ? existing : null;
                _settings[key] = value!;
                
                _logger.LogDebug("Setting changed: {Key} = {Value}", key, value);
                
                // Raise event
                SettingChanged?.Invoke(this, new SettingChangedEventArgs(key, oldValue, value));
                
                // Auto-save to storage
                await SaveAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value: {Key}", key);
                throw;
            }
        }

        public async Task RemoveSettingAsync(string key)
        {
            try
            {
                if (_settings.TryGetValue(key, out var oldValue))
                {
                    _settings.Remove(key);
                    
                    _logger.LogDebug("Setting removed: {Key}", key);
                    
                    // Raise event
                    SettingChanged?.Invoke(this, new SettingChangedEventArgs(key, oldValue, null));
                    
                    // Auto-save to storage
                    await SaveAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing setting: {Key}", key);
                throw;
            }
        }

        public async Task<bool> HasSettingAsync(string key)
        {
            return _settings.ContainsKey(key);
        }

        public async Task<Dictionary<string, object>> GetCategoryAsync(string category)
        {
            var result = new Dictionary<string, object>();
            var prefix = category + ".";
            
            foreach (var kvp in _settings)
            {
                if (kvp.Key.StartsWith(prefix))
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            
            return result;
        }

        public async Task ClearCategoryAsync(string category)
        {
            try
            {
                var prefix = category + ".";
                var keysToRemove = new List<string>();
                
                foreach (var key in _settings.Keys)
                {
                    if (key.StartsWith(prefix))
                    {
                        keysToRemove.Add(key);
                    }
                }
                
                foreach (var key in keysToRemove)
                {
                    await RemoveSettingAsync(key);
                }
                
                _logger.LogInformation("Cleared settings category: {Category}", category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing category: {Category}", category);
                throw;
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, _jsonOptions);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
                _logger.LogDebug("Settings saved to storage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings to storage");
                throw;
            }
        }

        public async Task LoadAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
                
                if (!string.IsNullOrEmpty(json))
                {
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, _jsonOptions);
                    
                    if (loaded != null)
                    {
                        _settings.Clear();
                        
                        foreach (var kvp in loaded)
                        {
                            _settings[kvp.Key] = kvp.Value;
                        }
                        
                        _logger.LogInformation("Loaded {Count} settings from storage", _settings.Count);
                    }
                }
                else
                {
                    await LoadDefaultSettingsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings from storage, loading defaults");
                await LoadDefaultSettingsAsync();
            }
        }

        public async Task ResetToDefaultsAsync()
        {
            try
            {
                _settings.Clear();
                await LoadDefaultSettingsAsync();
                await SaveAsync();
                
                _logger.LogInformation("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting settings to defaults");
                throw;
            }
        }

        private async Task LoadDefaultSettingsAsync()
        {
            // Load default system settings
            _settings["system.theme"] = "Classic Hacker";
            _settings["system.language"] = "en-US";
            _settings["system.startup_sound"] = true;
            
            // Desktop settings
            _settings["desktop.background_image"] = "";
            _settings["desktop.icon_grid_size"] = 64;
            _settings["desktop.show_desktop_icons"] = true;
            _settings["desktop.wallpaper_mode"] = "stretch";
            
            // Terminal settings
            _settings["terminal.font_size"] = 14;
            _settings["terminal.font_family"] = "Consolas, monospace";
            _settings["terminal.cursor_blink"] = true;
            _settings["terminal.scroll_buffer"] = 1000;
            
            // Window settings
            _settings["window.default_width"] = 800;
            _settings["window.default_height"] = 600;
            _settings["window.transparency"] = false;
            _settings["window.animations"] = true;
            
            // Security settings
            _settings["security.require_password"] = false;
            _settings["security.auto_lock_timeout"] = 0;
            _settings["security.show_password_hints"] = true;
            
            _logger.LogInformation("Loaded default settings");
        }
    }
}
