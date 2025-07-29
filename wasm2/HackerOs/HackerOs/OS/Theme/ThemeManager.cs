using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Theme
{
    /// <summary>
    /// Default implementation of IThemeManager
    /// </summary>
    public class ThemeManager : IThemeManager
    {
        private readonly ILogger<ThemeManager> _logger;
        private readonly Dictionary<string, ITheme> _themes;
        private ITheme _currentTheme;

        public event EventHandler? ThemeChanged;

        public ITheme CurrentTheme => _currentTheme;

        public IEnumerable<ITheme> AvailableThemes => _themes.Values;

        public ThemeManager(ILogger<ThemeManager> logger)
        {
            _logger = logger;
            _themes = new Dictionary<string, ITheme>();
            
            // Load default themes
            LoadDefaultThemes();
            
            // Set default theme
            _currentTheme = _themes.Values.FirstOrDefault() ?? CreateDefaultTheme();
        }

        public async Task<bool> ApplyThemeAsync(string themeName)
        {
            try
            {
                if (_themes.TryGetValue(themeName, out var theme))
                {
                    var previousTheme = _currentTheme;
                    _currentTheme = theme;
                    
                    _logger.LogInformation("Applied theme: {ThemeName}", themeName);
                    
                    // Notify theme change
                    ThemeChanged?.Invoke(this, EventArgs.Empty);
                    
                    return true;
                }
                
                _logger.LogWarning("Theme not found: {ThemeName}", themeName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying theme: {ThemeName}", themeName);
                return false;
            }
        }

        public async Task<ITheme> LoadThemeAsync(string themeData)
        {
            try
            {
                // TODO: Implement theme loading from JSON/XML data
                // For now, return a basic theme
                return CreateDefaultTheme();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading theme from data");
                throw;
            }
        }

        public Dictionary<string, string> GetThemeCssVariables()
        {
            var variables = new Dictionary<string, string>();
            
            if (_currentTheme?.CssVariables != null)
            {
                foreach (var kvp in _currentTheme.CssVariables)
                {
                    variables[kvp.Key] = kvp.Value;
                }
            }
            
            // Add color scheme variables
            if (_currentTheme?.ColorScheme != null)
            {
                var colors = _currentTheme.ColorScheme;
                variables["--color-primary"] = colors.Primary;
                variables["--color-secondary"] = colors.Secondary;
                variables["--color-background"] = colors.Background;
                variables["--color-surface"] = colors.Surface;
                variables["--color-text"] = colors.Text;
                variables["--color-text-secondary"] = colors.TextSecondary;
                variables["--color-border"] = colors.Border;
                variables["--color-accent"] = colors.Accent;
                variables["--color-warning"] = colors.Warning;
                variables["--color-error"] = colors.Error;
                variables["--color-success"] = colors.Success;
            }
            
            return variables;
        }

        public void RegisterTheme(ITheme theme)
        {
            _themes[theme.Name] = theme;
            _logger.LogInformation("Registered theme: {ThemeName}", theme.Name);
        }

        private void LoadDefaultThemes()
        {
            // Classic Hacker theme
            RegisterTheme(new HackerTheme());
            
            // Matrix theme
            RegisterTheme(new MatrixTheme());
            
            // Cyberpunk theme
            RegisterTheme(new CyberpunkTheme());
        }

        private ITheme CreateDefaultTheme()
        {
            return new HackerTheme();
        }
    }
}
