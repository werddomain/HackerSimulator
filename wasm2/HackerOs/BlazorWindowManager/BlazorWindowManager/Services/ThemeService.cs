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

    /// <summary>
    /// Gets all available themes as a list
    /// </summary>
    public IList<ITheme> GetAvailableThemes()
    {
        return _themes.Values.ToList();
    }

    /// <summary>
    /// Sets the active theme by ID
    /// </summary>
    public async Task SetThemeAsync(string themeId)
    {
        await ApplyThemeAsync(themeId);
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
        });        // Windows 98 Theme
        RegisterTheme(new ThemeDefinition
        {
            Id = "windows-98",
            Name = "Windows 98",
            Description = "Classic Windows 98 interface with raised buttons and nostalgic styling",
            CssClass = "bwm-theme-windows-98",
            CssFilePath = "./_content/BlazorWindowManager/css/themes/windows-98.css",
            Category = ThemeCategory.Windows,
            IsDarkTheme = false,
            CustomVariables = new Dictionary<string, string>
            {
                // Classic Windows 98 System Colors
                ["--win98-button-face"] = "#c0c0c0",
                ["--win98-button-highlight"] = "#ffffff",
                ["--win98-button-shadow"] = "#808080",
                ["--win98-button-dark-shadow"] = "#404040",
                ["--win98-button-text"] = "#000000",
                ["--win98-active-caption"] = "#0a246a",
                ["--win98-active-caption-text"] = "#ffffff",
                ["--win98-inactive-caption"] = "#808080",
                ["--win98-inactive-caption-text"] = "#c0c0c0",
                ["--win98-window-background"] = "#c0c0c0",
                ["--win98-window-text"] = "#000000",
                ["--win98-highlight"] = "#316ac5",
                ["--win98-highlight-text"] = "#ffffff",
                
                // Window System Overrides
                ["--bwm-window-background"] = "#c0c0c0",
                ["--bwm-window-border"] = "#404040",
                ["--bwm-window-border-radius"] = "0px",
                ["--bwm-titlebar-background"] = "linear-gradient(90deg, #0a246a 0%, #4570b5 100%)",
                ["--bwm-title-color"] = "#ffffff",
                ["--bwm-title-font-family"] = "'MS Sans Serif', sans-serif",
                ["--bwm-title-font-size"] = "11px",
                ["--bwm-content-background"] = "#c0c0c0",
                ["--bwm-content-color"] = "#000000",
                ["--bwm-content-font-family"] = "'MS Sans Serif', sans-serif",
                ["--bwm-taskbar-background"] = "#c0c0c0",
                ["--bwm-taskbar-height"] = "28px",                ["--bwm-desktop-background"] = "#008080"
            }
        });

        // Windows XP Theme
        RegisterTheme(new ThemeDefinition
        {
            Id = "windows-xp",
            Name = "Windows XP",
            Description = "Classic Windows XP Luna blue interface with rounded corners and glass-like effects",
            CssClass = "bwm-theme-windows-xp",
            CssFilePath = "./_content/BlazorWindowManager/css/themes/windows-xp.css",
            Category = ThemeCategory.Windows,
            IsDarkTheme = false,
            CustomVariables = new Dictionary<string, string>
            {
                // Classic Windows XP Luna Blue Colors
                ["--winxp-luna-blue"] = "#316ac5",
                ["--winxp-luna-blue-light"] = "#4d84d1",
                ["--winxp-luna-blue-dark"] = "#1e5aa0",
                ["--winxp-window-bg"] = "#ece9d8",
                ["--winxp-button-face"] = "#ece9d8",
                ["--winxp-button-highlight"] = "#ffffff",
                ["--winxp-button-shadow"] = "#aca899",
                ["--winxp-button-dark-shadow"] = "#716f64",
                ["--winxp-active-caption"] = "linear-gradient(180deg, #0997ff 0%, #0053ee 49%, #0050ee 50%, #06f 100%)",
                ["--winxp-inactive-caption"] = "linear-gradient(180deg, #7a96df 0%, #416ab5 49%, #4a6fbb 50%, #4a6eaa 100%)",
                ["--winxp-window-text"] = "#000000",
                ["--winxp-caption-text"] = "#ffffff",
                ["--winxp-highlight"] = "#316ac5",
                ["--winxp-highlight-text"] = "#ffffff",
                ["--winxp-selection-bg"] = "#316ac5",
                ["--winxp-selection-text"] = "#ffffff",
                
                // Window System Overrides for XP
                ["--bwm-window-background"] = "#ece9d8",
                ["--bwm-window-border"] = "#0054e3",
                ["--bwm-window-border-radius"] = "8px",
                ["--bwm-window-shadow"] = "0 4px 16px rgba(0, 0, 0, 0.25)",
                ["--bwm-titlebar-background"] = "linear-gradient(180deg, #0997ff 0%, #0053ee 49%, #0050ee 50%, #06f 100%)",
                ["--bwm-titlebar-border-radius"] = "8px 8px 0 0",
                ["--bwm-title-color"] = "#ffffff",
                ["--bwm-title-font-family"] = "'Tahoma', 'Segoe UI', sans-serif",
                ["--bwm-title-font-size"] = "11px",
                ["--bwm-title-font-weight"] = "normal",
                ["--bwm-content-background"] = "#ece9d8",
                ["--bwm-content-color"] = "#000000",
                ["--bwm-content-font-family"] = "'Tahoma', 'Segoe UI', sans-serif",
                ["--bwm-taskbar-background"] = "linear-gradient(180deg, #245edb 0%, #1941a5 49%, #1941a5 50%, #14368a 100%)",
                ["--bwm-taskbar-height"] = "30px",
                ["--bwm-taskbar-border-radius"] = "0",
                ["--bwm-desktop-background"] = "#5a7edc",
                ["--bwm-button-background"] = "linear-gradient(180deg, #f4f1ea 0%, #ece9d8 49%, #e6e2d8 50%, #ddd8c8 100%)",
                ["--bwm-button-border"] = "#0054e3",
                ["--bwm-button-border-radius"] = "3px",                ["--bwm-button-hover-background"] = "linear-gradient(180deg, #fff8f0 0%, #f4f1ea 49%, #ede9d9 50%, #e5dfcd 100%)",
                ["--bwm-button-active-background"] = "linear-gradient(180deg, #d8d5c8 0%, #ece9d8 49%, #f0eddc 50%, #f7f4e7 100%)"
            }
        });

        // Windows Vista Theme
        RegisterTheme(new ThemeDefinition
        {
            Id = "windows-vista",
            Name = "Windows Vista",
            Description = "Classic Windows Vista Aero interface with glass effects and transparency",
            CssClass = "bwm-theme-windows-vista",
            CssFilePath = "./_content/BlazorWindowManager/css/themes/windows-vista.css",
            Category = ThemeCategory.Windows,
            IsDarkTheme = false,
            CustomVariables = new Dictionary<string, string>
            {
                // Classic Windows Vista Aero Colors
                ["--vista-aero-blue"] = "#1f5f99",
                ["--vista-aero-light-blue"] = "#e6f3ff",
                ["--vista-aero-medium-blue"] = "#b8d4f1",
                ["--vista-aero-dark-blue"] = "#1a4480",
                ["--vista-glass-blue"] = "rgba(31, 95, 153, 0.8)",
                ["--vista-glass-light-blue"] = "rgba(230, 243, 255, 0.9)",
                ["--vista-window-background"] = "rgba(255, 255, 255, 0.95)",
                ["--vista-window-text"] = "#000000",
                ["--vista-button-face"] = "rgba(235, 235, 235, 0.9)",
                ["--vista-active-caption"] = "linear-gradient(180deg, rgba(185, 209, 234, 0.9) 0%, rgba(31, 95, 153, 0.85) 100%)",
                ["--vista-inactive-caption"] = "linear-gradient(180deg, rgba(235, 235, 235, 0.8) 0%, rgba(153, 153, 153, 0.75) 100%)",
                ["--vista-caption-text"] = "#ffffff",
                ["--vista-highlight"] = "rgba(31, 95, 153, 0.8)",
                ["--vista-highlight-text"] = "#ffffff",
                
                // Window System Overrides for Vista
                ["--bwm-window-background"] = "rgba(255, 255, 255, 0.95)",
                ["--bwm-window-border"] = "rgba(31, 95, 153, 0.6)",
                ["--bwm-window-border-radius"] = "8px",
                ["--bwm-window-shadow"] = "0 8px 32px rgba(0, 0, 0, 0.4), 0 2px 8px rgba(0, 0, 0, 0.2)",
                ["--bwm-titlebar-background"] = "linear-gradient(180deg, rgba(185, 209, 234, 0.9) 0%, rgba(31, 95, 153, 0.85) 100%)",
                ["--bwm-title-color"] = "#ffffff",
                ["--bwm-title-font-family"] = "'Segoe UI', 'Tahoma', sans-serif",
                ["--bwm-title-font-size"] = "12px",
                ["--bwm-title-font-weight"] = "normal",
                ["--bwm-content-background"] = "rgba(255, 255, 255, 0.95)",
                ["--bwm-content-color"] = "#000000",
                ["--bwm-content-font-family"] = "'Segoe UI', 'Tahoma', sans-serif",
                ["--bwm-taskbar-background"] = "linear-gradient(180deg, rgba(31, 95, 153, 0.85) 0%, rgba(26, 68, 128, 0.9) 50%, rgba(20, 45, 80, 0.95) 100%)",
                ["--bwm-taskbar-height"] = "40px",
                ["--bwm-taskbar-border-radius"] = "0",
                ["--bwm-desktop-background"] = "linear-gradient(135deg, #1F5F99 0%, #2F4F8F 25%, #483D8B 50%, #4169E1 75%, #1F5F99 100%)",
                ["--bwm-button-background"] = "linear-gradient(180deg, rgba(248, 248, 248, 0.9) 0%, rgba(235, 235, 235, 0.85) 50%, rgba(218, 218, 218, 0.8) 100%)",
                ["--bwm-button-border"] = "rgba(31, 95, 153, 0.6)",
                ["--bwm-button-border-radius"] = "4px",
                ["--bwm-button-hover-background"] = "linear-gradient(180deg, rgba(255, 255, 255, 0.95) 0%, rgba(245, 245, 245, 0.9) 50%, rgba(230, 230, 230, 0.85) 100%)",                ["--bwm-button-active-background"] = "linear-gradient(180deg, rgba(210, 210, 210, 0.8) 0%, rgba(235, 235, 235, 0.85) 50%, rgba(248, 248, 248, 0.9) 100%)"
            }
        });

        // Windows 7 Theme
        RegisterTheme(new ThemeDefinition
        {
            Id = "windows-7",
            Name = "Windows 7",
            Description = "Enhanced Windows 7 Aero interface with refined glass effects and improved contrast",
            CssClass = "bwm-theme-windows-7",
            CssFilePath = "./_content/BlazorWindowManager/css/themes/windows-7.css",
            Category = ThemeCategory.Windows,
            IsDarkTheme = false,
            CustomVariables = new Dictionary<string, string>
            {
                // Enhanced Windows 7 Aero Colors
                ["--win7-aero-blue"] = "#2e74b5",
                ["--win7-aero-light-blue"] = "#e8f4fd",
                ["--win7-aero-medium-blue"] = "#c1e0f7",
                ["--win7-aero-dark-blue"] = "#1f4d73",
                ["--win7-glass-blue"] = "rgba(46, 116, 181, 0.85)",
                ["--win7-glass-light-blue"] = "rgba(232, 244, 253, 0.92)",
                ["--win7-window-background"] = "rgba(255, 255, 255, 0.98)",
                ["--win7-window-text"] = "#000000",
                ["--win7-button-face"] = "rgba(240, 240, 240, 0.95)",
                ["--win7-active-caption"] = "linear-gradient(180deg, rgba(193, 224, 247, 0.95) 0%, rgba(46, 116, 181, 0.9) 50%, rgba(31, 77, 115, 0.88) 100%)",
                ["--win7-inactive-caption"] = "linear-gradient(180deg, rgba(245, 245, 245, 0.9) 0%, rgba(180, 180, 180, 0.85) 50%, rgba(140, 140, 140, 0.8) 100%)",
                ["--win7-caption-text"] = "#ffffff",
                ["--win7-highlight"] = "rgba(46, 116, 181, 0.85)",
                ["--win7-highlight-text"] = "#ffffff",
                ["--win7-desktop-blue"] = "#2e74b5",
                
                // Window System Overrides for Windows 7
                ["--bwm-window-background"] = "rgba(255, 255, 255, 0.98)",
                ["--bwm-window-border"] = "rgba(46, 116, 181, 0.7)",
                ["--bwm-window-border-radius"] = "8px",
                ["--bwm-window-shadow"] = "0 10px 40px rgba(0, 0, 0, 0.35), 0 3px 12px rgba(0, 0, 0, 0.25)",
                ["--bwm-titlebar-background"] = "linear-gradient(180deg, rgba(193, 224, 247, 0.95) 0%, rgba(46, 116, 181, 0.9) 50%, rgba(31, 77, 115, 0.88) 100%)",
                ["--bwm-titlebar-border-radius"] = "8px 8px 0 0",
                ["--bwm-title-color"] = "#ffffff",
                ["--bwm-title-font-family"] = "'Segoe UI', 'Tahoma', sans-serif",
                ["--bwm-title-font-size"] = "13px",
                ["--bwm-title-font-weight"] = "normal",
                ["--bwm-content-background"] = "rgba(255, 255, 255, 0.98)",
                ["--bwm-content-color"] = "#000000",
                ["--bwm-content-font-family"] = "'Segoe UI', 'Tahoma', sans-serif",
                ["--bwm-taskbar-background"] = "linear-gradient(180deg, rgba(46, 116, 181, 0.9) 0%, rgba(31, 77, 115, 0.95) 100%)",
                ["--bwm-taskbar-height"] = "48px",
                ["--bwm-taskbar-border-radius"] = "0",
                ["--bwm-desktop-background"] = "linear-gradient(135deg, #2e74b5 0%, rgba(46, 116, 181, 0.8) 100%)",
                ["--bwm-button-background"] = "linear-gradient(180deg, rgba(255, 255, 255, 0.2) 0%, rgba(255, 255, 255, 0.1) 50%, rgba(0, 0, 0, 0.1) 100%)",
                ["--bwm-button-border"] = "rgba(46, 116, 181, 0.6)",
                ["--bwm-button-border-radius"] = "4px",
                ["--bwm-button-hover-background"] = "linear-gradient(180deg, rgba(255, 255, 255, 0.3) 0%, rgba(255, 255, 255, 0.2) 50%, rgba(0, 0, 0, 0.05) 100%)",                ["--bwm-button-active-background"] = "linear-gradient(180deg, rgba(0, 0, 0, 0.1) 0%, rgba(255, 255, 255, 0.1) 50%, rgba(255, 255, 255, 0.2) 100%)"
            }
        });

        // Windows 10 Theme
        RegisterTheme(new ThemeDefinition
        {
            Id = "windows-10",
            Name = "Windows 10",
            Description = "Modern flat design with minimalism, accent colors, and subtle shadows",
            CssClass = "bwm-theme-windows-10",
            CssFilePath = "./_content/BlazorWindowManager/css/themes/windows-10.css",
            Category = ThemeCategory.Windows,
            IsDarkTheme = false,
            CustomVariables = new Dictionary<string, string>
            {
                // Windows 10 Modern Colors
                ["--win10-accent-blue"] = "#0078d4",
                ["--win10-accent-light-blue"] = "#429ce3",
                ["--win10-accent-dark-blue"] = "#005a9e",
                ["--win10-background-light"] = "#ffffff",
                ["--win10-background-medium"] = "#f3f3f3",
                ["--win10-background-dark"] = "#e1e1e1",
                ["--win10-text-primary"] = "#000000",
                ["--win10-text-secondary"] = "#424242",
                ["--win10-text-disabled"] = "#a6a6a6",
                ["--win10-border-light"] = "#e1e1e1",
                ["--win10-border-medium"] = "#d1d1d1",
                ["--win10-border-dark"] = "#bebebe",
                ["--win10-shadow-light"] = "rgba(0, 0, 0, 0.1)",
                ["--win10-shadow-medium"] = "rgba(0, 0, 0, 0.15)",
                ["--win10-taskbar-dark"] = "#2f3136",
                ["--win10-taskbar-darker"] = "#232428",
                
                // Window System Overrides for Windows 10
                ["--bwm-window-background"] = "#ffffff",
                ["--bwm-window-border"] = "#d1d1d1",
                ["--bwm-window-border-radius"] = "0px",
                ["--bwm-window-shadow"] = "0 8px 16px rgba(0, 0, 0, 0.1), 0 4px 8px rgba(0, 0, 0, 0.15)",
                ["--bwm-titlebar-background"] = "#ffffff",
                ["--bwm-titlebar-border-radius"] = "0px",
                ["--bwm-title-color"] = "#000000",
                ["--bwm-title-font-family"] = "'Segoe UI', system-ui, -apple-system, sans-serif",
                ["--bwm-title-font-size"] = "14px",
                ["--bwm-title-font-weight"] = "400",
                ["--bwm-content-background"] = "#ffffff",
                ["--bwm-content-color"] = "#000000",
                ["--bwm-content-font-family"] = "'Segoe UI', system-ui, -apple-system, sans-serif",
                ["--bwm-taskbar-background"] = "#2f3136",
                ["--bwm-taskbar-height"] = "48px",
                ["--bwm-taskbar-border-radius"] = "0px",
                ["--bwm-desktop-background"] = "linear-gradient(135deg, #0078d4 0%, #005a9e 100%)",
                ["--bwm-button-background"] = "transparent",
                ["--bwm-button-border"] = "#d1d1d1",
                ["--bwm-button-border-radius"] = "0px",
                ["--bwm-button-hover-background"] = "rgba(0, 0, 0, 0.05)",                ["--bwm-button-active-background"] = "rgba(0, 0, 0, 0.1)"
            }
        });

        // macOS Theme
        RegisterTheme(new ThemeDefinition
        {            Id = "macos",
            Name = "macOS",
            Description = "System-style buttons, refined typography, traffic light controls",
            CssClass = "bwm-theme-macos",
            CssFilePath = "./_content/BlazorWindowManager/css/themes/macos.css",
            Category = ThemeCategory.MacOS,
            IsDarkTheme = false,
            CustomVariables = new Dictionary<string, string>
            {
                // macOS System Colors
                ["--macos-background-primary"] = "#ffffff",
                ["--macos-background-secondary"] = "#f6f6f6",
                ["--macos-background-tertiary"] = "#ebebeb",
                ["--macos-text-primary"] = "#000000",
                ["--macos-text-secondary"] = "#3c3c3c",
                ["--macos-text-disabled"] = "#86868b",
                ["--macos-separator"] = "#d1d1d6",
                ["--macos-accent-blue"] = "#007aff",
                ["--macos-accent-blue-hover"] = "#0056cc",
                ["--macos-accent-blue-active"] = "#004299",
                
                // macOS Traffic Light Colors
                ["--macos-traffic-red"] = "#ff5f57",
                ["--macos-traffic-red-hover"] = "#ff3b30",
                ["--macos-traffic-yellow"] = "#ffbd2e",
                ["--macos-traffic-yellow-hover"] = "#ff9500",
                ["--macos-traffic-green"] = "#28ca42",
                ["--macos-traffic-green-hover"] = "#30d158",
                
                // Window System Overrides for macOS
                ["--bwm-window-background"] = "#ffffff",
                ["--bwm-window-border"] = "#d1d1d6",
                ["--bwm-window-border-radius"] = "8px",
                ["--bwm-window-shadow"] = "0 10px 25px rgba(0, 0, 0, 0.1), 0 5px 10px rgba(0, 0, 0, 0.15)",
                ["--bwm-titlebar-background"] = "#f6f6f6",
                ["--bwm-titlebar-border-radius"] = "8px 8px 0 0",
                ["--bwm-titlebar-height"] = "28px",
                ["--bwm-title-color"] = "#000000",
                ["--bwm-title-font-family"] = "-apple-system, BlinkMacSystemFont, 'SF Pro Display', 'Helvetica Neue', Arial, sans-serif",
                ["--bwm-title-font-size"] = "13px",
                ["--bwm-title-font-weight"] = "500",
                ["--bwm-title-text-align"] = "center",
                ["--bwm-content-background"] = "#ffffff",
                ["--bwm-content-color"] = "#000000",
                ["--bwm-content-font-family"] = "-apple-system, BlinkMacSystemFont, 'SF Pro Text', 'Helvetica Neue', Arial, sans-serif",
                ["--bwm-content-font-size"] = "14px",
                ["--bwm-taskbar-background"] = "rgba(245, 245, 245, 0.8)",
                ["--bwm-taskbar-height"] = "60px",
                ["--bwm-taskbar-border-radius"] = "16px",
                ["--bwm-desktop-background"] = "linear-gradient(135deg, #007aff 0%, #0056cc 100%)",
                ["--bwm-button-background"] = "transparent",
                ["--bwm-button-border"] = "#d1d1d6",
                ["--bwm-button-border-radius"] = "8px",
                ["--bwm-button-hover-background"] = "rgba(0, 0, 0, 0.1)",
                ["--bwm-button-active-background"] = "#007aff",
                
                // Traffic Light Controls
                ["--bwm-control-close-background"] = "#ff5f57",
                ["--bwm-control-minimize-background"] = "#ffbd2e",
                ["--bwm-control-maximize-background"] = "#28ca42",                ["--bwm-control-size"] = "12px",
                ["--bwm-control-border-radius"] = "50%"
            }
        });

        // Linux/GTK Theme
        RegisterTheme(new ThemeDefinition
        {            Id = "linux",
            Name = "Linux (GTK)",
            Description = "GTK-inspired, clean and functional design",
            CssClass = "bwm-theme-linux",
            CssFilePath = "./_content/BlazorWindowManager/css/themes/linux.css",
            Category = ThemeCategory.Linux,
            IsDarkTheme = false,
            CustomVariables = new Dictionary<string, string>
            {
                // GTK System Colors
                ["--gtk-background-primary"] = "#ffffff",
                ["--gtk-background-secondary"] = "#f6f5f4",
                ["--gtk-background-tertiary"] = "#edebe9",
                ["--gtk-text-primary"] = "#2e3436",
                ["--gtk-text-secondary"] = "#555753",
                ["--gtk-text-disabled"] = "#888a85",
                ["--gtk-border-color"] = "#d3d7cf",
                ["--gtk-border-dark"] = "#babdb6",
                ["--gtk-accent-blue"] = "#3584e4",
                ["--gtk-accent-blue-hover"] = "#1c71d8",
                ["--gtk-accent-blue-active"] = "#1a5fb4",
                
                // GTK Header Bar Colors
                ["--gtk-headerbar-background"] = "#e9e9e7",
                ["--gtk-headerbar-border"] = "#cdc7c2",
                ["--gtk-headerbar-text"] = "#2e3436",
                
                // GTK Panel Colors
                ["--gtk-panel-background"] = "#2e3436",
                ["--gtk-panel-text"] = "#eeeeec",
                ["--gtk-panel-border"] = "#1a1e20",
                
                // Window System Overrides for Linux
                ["--bwm-window-background"] = "#ffffff",
                ["--bwm-window-border"] = "#d3d7cf",
                ["--bwm-window-border-radius"] = "6px",
                ["--bwm-window-shadow"] = "0 8px 16px rgba(0, 0, 0, 0.1), 0 4px 8px rgba(0, 0, 0, 0.15), 0 1px 2px rgba(0, 0, 0, 0.3)",
                ["--bwm-titlebar-background"] = "#e9e9e7",
                ["--bwm-titlebar-border-radius"] = "6px 6px 0 0",
                ["--bwm-titlebar-height"] = "32px",
                ["--bwm-title-color"] = "#2e3436",
                ["--bwm-title-font-family"] = "'Inter', 'Ubuntu', 'Cantarell', system-ui, sans-serif",
                ["--bwm-title-font-size"] = "14px",
                ["--bwm-title-font-weight"] = "500",
                ["--bwm-title-text-align"] = "left",
                ["--bwm-content-background"] = "#ffffff",
                ["--bwm-content-color"] = "#2e3436",
                ["--bwm-content-font-family"] = "'Inter', 'Ubuntu', 'Liberation Sans', system-ui, sans-serif",
                ["--bwm-content-font-size"] = "14px",
                ["--bwm-taskbar-background"] = "#2e3436",
                ["--bwm-taskbar-height"] = "48px",
                ["--bwm-taskbar-border-radius"] = "0px",
                ["--bwm-desktop-background"] = "linear-gradient(135deg, #3584e4 0%, #1c71d8 100%)",
                ["--bwm-button-background"] = "#f6f5f4",
                ["--bwm-button-border"] = "#cdc7c2",
                ["--bwm-button-border-radius"] = "6px",
                ["--bwm-button-hover-background"] = "#f0efed",
                ["--bwm-button-active-background"] = "#3584e4",
                
                // GTK Window Controls
                ["--bwm-control-close-background"] = "#e01b24",
                ["--bwm-control-minimize-background"] = "#f9f06b",
                ["--bwm-control-maximize-background"] = "#26a269",
                ["--bwm-control-size"] = "20px",
                ["--bwm-control-border-radius"] = "4px"
            }
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
