using BlazorWindowManager.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace BlazorWindowManager.Services;

/// <summary>
/// Service for managing themes in the Blazor Window Manager system.
/// Handles theme registration, switching, and CSS injection.
/// </summary>
public class ThemeService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly Dictionary<string, ITheme> _themes = new();
    private ITheme? _currentTheme;
    private ThemeSettings _settings = new();
    private IJSObjectReference? _jsModule;

    #region Events

    /// <summary>
    /// Raised when the active theme changes
    /// </summary>
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    /// Raised when a new theme is registered
    /// </summary>
    public event EventHandler<ITheme>? ThemeRegistered;

    #endregion

    #region Constructor

    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
        RegisterBuiltInThemes();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the currently active theme
    /// </summary>
    public ITheme? CurrentTheme => _currentTheme;

    /// <summary>
    /// Gets all registered themes
    /// </summary>
    public IReadOnlyDictionary<string, ITheme> RegisteredThemes => _themes;

    /// <summary>
    /// Gets current theme settings
    /// </summary>
    public ThemeSettings Settings => _settings;

    #endregion

    #region Public Methods

    /// <summary>
    /// Initializes the theme service and loads the default theme
    /// </summary>
    public async Task InitializeAsync()
    {
        _jsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorWindowManager/js/theme-manager.js");

        // Load persisted settings
        await LoadSettingsAsync();

        // Apply the default or previously selected theme
        var defaultTheme = GetTheme(_settings.ActiveThemeId) ?? GetTheme("hacker-matrix");
        if (defaultTheme != null)
        {
            await ApplyThemeAsync(defaultTheme, skipPersistence: true);
        }
    }

    /// <summary>
    /// Registers a new theme
    /// </summary>
    public void RegisterTheme(ITheme theme)
    {
        if (string.IsNullOrWhiteSpace(theme.Id))
            throw new ArgumentException("Theme ID cannot be empty", nameof(theme));

        _themes[theme.Id] = theme;
        ThemeRegistered?.Invoke(this, theme);
    }

    /// <summary>
    /// Gets a theme by ID
    /// </summary>
    public ITheme? GetTheme(string themeId)
    {
        return _themes.TryGetValue(themeId, out var theme) ? theme : null;
    }

    /// <summary>
    /// Gets all themes in a specific category
    /// </summary>
    public IEnumerable<ITheme> GetThemesByCategory(ThemeCategory category)
    {
        return _themes.Values.Where(t => t.Category == category);
    }

    /// <summary>
    /// Applies a theme by ID
    /// </summary>
    public async Task<bool> ApplyThemeAsync(string themeId)
    {
        var theme = GetTheme(themeId);
        if (theme == null) return false;

        await ApplyThemeAsync(theme);
        return true;
    }

    /// <summary>
    /// Applies a specific theme
    /// </summary>
    public async Task ApplyThemeAsync(ITheme theme, bool skipPersistence = false)
    {
        if (_jsModule == null)
        {
            throw new InvalidOperationException("ThemeService not initialized. Call InitializeAsync first.");
        }

        var previousTheme = _currentTheme;
        
        try
        {
            // Remove previous theme CSS class
            if (previousTheme != null && !string.IsNullOrEmpty(previousTheme.CssClass))
            {
                await _jsModule.InvokeVoidAsync("removeThemeClass", previousTheme.CssClass);
            }

            // Apply new theme CSS class
            if (!string.IsNullOrEmpty(theme.CssClass))
            {
                await _jsModule.InvokeVoidAsync("addThemeClass", theme.CssClass);
            }

            // Inject theme CSS if it has a file path
            if (!string.IsNullOrEmpty(theme.CssFilePath))
            {
                await _jsModule.InvokeVoidAsync("loadThemeCSS", theme.Id, theme.CssFilePath);
            }

            // Apply custom CSS variables
            if (theme.CustomVariables.Any())
            {
                await _jsModule.InvokeVoidAsync("applyThemeVariables", theme.CustomVariables);
            }

            // Apply user custom variables
            if (_settings.UserCustomVariables.Any())
            {
                await _jsModule.InvokeVoidAsync("applyThemeVariables", _settings.UserCustomVariables);
            }

            _currentTheme = theme;

            // Update settings and persist if needed
            if (!skipPersistence)
            {
                _settings.ActiveThemeId = theme.Id;
                await SaveSettingsAsync();
            }

            // Raise event
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs
            {
                PreviousTheme = previousTheme,
                NewTheme = theme
            });
        }
        catch (Exception ex)
        {
            // Log error and potentially rollback
            Console.WriteLine($"Error applying theme '{theme.Name}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Updates theme settings
    /// </summary>
    public async Task UpdateSettingsAsync(ThemeSettings newSettings)
    {
        _settings = newSettings;
        await SaveSettingsAsync();
    }

    /// <summary>
    /// Adds or updates a user custom CSS variable
    /// </summary>
    public async Task SetCustomVariableAsync(string variableName, string value)
    {
        _settings.UserCustomVariables[variableName] = value;
        
        if (_jsModule != null)
        {
            await _jsModule.InvokeVoidAsync("setCustomVariable", variableName, value);
        }
        
        await SaveSettingsAsync();
    }

    /// <summary>
    /// Removes a user custom CSS variable
    /// </summary>
    public async Task RemoveCustomVariableAsync(string variableName)
    {
        _settings.UserCustomVariables.Remove(variableName);
        
        if (_jsModule != null)
        {
            await _jsModule.InvokeVoidAsync("removeCustomVariable", variableName);
        }
        
        await SaveSettingsAsync();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Registers all built-in themes
    /// </summary>
    private void RegisterBuiltInThemes()
    {
        // Modern Theme
        RegisterTheme(new ThemeDefinition
        {
            Id = "modern",
            Name = "Modern",
            Description = "Clean, contemporary design with subtle gradients and smooth animations",
            CssClass = "bwm-theme-modern",
            CssFilePath = "./_content/BlazorWindowManager/css/themes/modern.css",
            Category = ThemeCategory.Modern,
            IsDarkTheme = false,
            CustomVariables = new Dictionary<string, string>
            {
                ["--bwm-window-background"] = "#ffffff",
                ["--bwm-window-border"] = "#e1e1e1",
                ["--bwm-window-shadow"] = "0 8px 32px rgba(0, 0, 0, 0.12)",
                ["--bwm-titlebar-background"] = "linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%)",
                ["--bwm-title-color"] = "#495057",
                ["--bwm-content-background"] = "#ffffff",
                ["--bwm-content-color"] = "#212529"
            }
        });

        // Hacker/Matrix Theme (Default)
        RegisterTheme(new ThemeDefinition
        {
            Id = "hacker-matrix",
            Name = "Hacker Matrix",
            Description = "Retro CRT-style interface with green on black, perfect for hacker aesthetics",
            CssClass = "bwm-theme-hacker-matrix",
            CssFilePath = "./_content/BlazorWindowManager/css/themes/hacker-matrix.css",
            Category = ThemeCategory.Gaming,
            IsDarkTheme = true,
            CustomVariables = new Dictionary<string, string>
            {
                ["--bwm-window-background"] = "#0a0a0a",
                ["--bwm-window-border"] = "#00ff00",
                ["--bwm-window-shadow"] = "0 0 20px rgba(0, 255, 0, 0.5), inset 0 0 20px rgba(0, 255, 0, 0.1)",
                ["--bwm-titlebar-background"] = "linear-gradient(135deg, #001100 0%, #000800 100%)",
                ["--bwm-title-color"] = "#00ff00",
                ["--bwm-content-background"] = "#000000",
                ["--bwm-content-color"] = "#00ff00",
                ["--bwm-content-font-family"] = "'Courier New', 'Liberation Mono', monospace"
            }
        });

        // Windows 98 Theme (placeholder for future implementation)
        RegisterTheme(new ThemeDefinition
        {
            Id = "windows-98",
            Name = "Windows 98",
            Description = "Classic Windows 98 interface with raised buttons and nostalgic styling",
            CssClass = "bwm-theme-windows-98",
            CssFilePath = "./_content/BlazorWindowManager/css/themes/windows-98.css",
            Category = ThemeCategory.Windows,
            IsDarkTheme = false,
            CustomVariables = new Dictionary<string, string>()
        });
    }

    /// <summary>
    /// Loads theme settings from local storage
    /// </summary>
    private async Task LoadSettingsAsync()
    {
        try
        {
            if (_jsModule != null)
            {
                var settingsJson = await _jsModule.InvokeAsync<string>("loadSettings");
                if (!string.IsNullOrEmpty(settingsJson))
                {
                    var settings = JsonSerializer.Deserialize<ThemeSettings>(settingsJson);
                    if (settings != null)
                    {
                        _settings = settings;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading theme settings: {ex.Message}");
            // Use defaults if loading fails
        }
    }

    /// <summary>
    /// Saves theme settings to local storage
    /// </summary>
    private async Task SaveSettingsAsync()
    {
        try
        {
            if (_jsModule != null && _settings.PersistThemeSelection)
            {
                var settingsJson = JsonSerializer.Serialize(_settings);
                await _jsModule.InvokeVoidAsync("saveSettings", settingsJson);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving theme settings: {ex.Message}");
        }
    }

    #endregion

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if (_jsModule != null)
        {
            await _jsModule.DisposeAsync();
        }
    }

    #endregion
}
