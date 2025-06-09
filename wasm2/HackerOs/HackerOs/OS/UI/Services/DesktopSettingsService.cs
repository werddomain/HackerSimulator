using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.UI.Models;
using HackerOs.OS.Core.Settings;
using System.Text.Json;

namespace HackerOs.OS.UI.Services
{
    /// <summary>
    /// Service responsible for managing desktop settings and persistence
    /// </summary>
    public class DesktopSettingsService
    {
        private readonly ISettingsService _settingsService;
        private const string DESKTOP_ICONS_KEY = "desktop.icons";
        private const string DESKTOP_SETTINGS_KEY = "desktop.settings";

        public DesktopSettingsService(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        /// <summary>
        /// Gets the desktop background image path
        /// </summary>
        public async Task<string> GetBackgroundImagePathAsync()
        {
            var settings = await GetDesktopSettingsAsync();
            return settings.TryGetValue("BackgroundImage", out var path) ? path : "/images/desktop/default-background.jpg";
        }

        /// <summary>
        /// Sets the desktop background image path
        /// </summary>
        public async Task SetBackgroundImagePathAsync(string path)
        {
            var settings = await GetDesktopSettingsAsync();
            settings["BackgroundImage"] = path;
            await SaveDesktopSettingsAsync(settings);
        }

        /// <summary>
        /// Gets the desktop icons
        /// </summary>
        public async Task<List<DesktopIcon>> GetDesktopIconsAsync()
        {
            var json = await _settingsService.GetSettingAsync<string>(DESKTOP_ICONS_KEY, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return CreateDefaultDesktopIcons();
            }

            try
            {
                return JsonSerializer.Deserialize<List<DesktopIcon>>(json) ?? CreateDefaultDesktopIcons();
            }
            catch (JsonException)
            {
                // If deserialization fails, return default icons
                return CreateDefaultDesktopIcons();
            }
        }

        /// <summary>
        /// Saves the desktop icons
        /// </summary>
        public async Task SaveDesktopIconsAsync(List<DesktopIcon> icons)
        {
            var json = JsonSerializer.Serialize(icons);
            await _settingsService.SetSettingAsync(DESKTOP_ICONS_KEY, json);
        }

        /// <summary>
        /// Gets the grid cell size for desktop icons
        /// </summary>
        public async Task<(int Width, int Height)> GetIconGridCellSizeAsync()
        {
            var settings = await GetDesktopSettingsAsync();
            
            if (settings.TryGetValue("IconGridCellWidth", out var widthStr) && 
                settings.TryGetValue("IconGridCellHeight", out var heightStr) &&
                int.TryParse(widthStr, out var width) &&
                int.TryParse(heightStr, out var height))
            {
                return (width, height);
            }
            
            return (80, 90); // Default grid cell size
        }

        /// <summary>
        /// Sets the grid cell size for desktop icons
        /// </summary>
        public async Task SetIconGridCellSizeAsync(int width, int height)
        {
            var settings = await GetDesktopSettingsAsync();
            settings["IconGridCellWidth"] = width.ToString();
            settings["IconGridCellHeight"] = height.ToString();
            await SaveDesktopSettingsAsync(settings);
        }

        /// <summary>
        /// Gets a dictionary of desktop settings
        /// </summary>
        private async Task<Dictionary<string, string>> GetDesktopSettingsAsync()
        {
            var json = await _settingsService.GetSettingAsync<string>(DESKTOP_SETTINGS_KEY, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return CreateDefaultDesktopSettings();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? CreateDefaultDesktopSettings();
            }
            catch (JsonException)
            {
                // If deserialization fails, return default settings
                return CreateDefaultDesktopSettings();
            }
        }

        /// <summary>
        /// Saves desktop settings
        /// </summary>
        private async Task SaveDesktopSettingsAsync(Dictionary<string, string> settings)
        {
            var json = JsonSerializer.Serialize(settings);
            await _settingsService.SetSettingAsync(DESKTOP_SETTINGS_KEY, json);
        }

        /// <summary>
        /// Creates default desktop icons
        /// </summary>
        private List<DesktopIcon> CreateDefaultDesktopIcons()
        {
            return new List<DesktopIcon>
            {
                DesktopIcon.CreateApplicationIcon(
                    "fileExplorer", 
                    "File Explorer", 
                    "/images/icons/file-explorer.png",
                    0, 0),
                DesktopIcon.CreateApplicationIcon(
                    "terminal", 
                    "Terminal", 
                    "/images/icons/terminal.png",
                    1, 0),
                DesktopIcon.CreateApplicationIcon(
                    "settings", 
                    "Settings", 
                    "/images/icons/settings.png",
                    0, 1),
                DesktopIcon.CreateApplicationIcon(
                    "textEditor", 
                    "Text Editor", 
                    "/images/icons/text-editor.png",
                    1, 1)
            };
        }

        /// <summary>
        /// Creates default desktop settings
        /// </summary>
        private Dictionary<string, string> CreateDefaultDesktopSettings()
        {
            return new Dictionary<string, string>
            {
                ["BackgroundImage"] = "/images/desktop/default-background.jpg",
                ["IconGridCellWidth"] = "80",
                ["IconGridCellHeight"] = "90",
                ["IconAlignment"] = "TopLeft",
                ["ShowDesktopIcons"] = "true"
            };
        }
    }
}
