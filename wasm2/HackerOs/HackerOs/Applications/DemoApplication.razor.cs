using BlazorWindowManager.Components;
using BlazorWindowManager.Models;
using HackerOs.OS.Applications.Attributes;

namespace HackerOs.Applications
{
    [App("Demo Application", "builtin.demo_application", Type = OS.Applications.ApplicationType.WindowedApplication)]
    public partial class DemoApplication: WindowBase
    {
        private ITheme? _currentTheme;
        private List<ITheme> _availableThemes = new();
        private List<ThemeCategory> _themeCategories = new();
        private bool _isLoading = false;

        protected override async Task OnInitializedAsync()
        {
            // Get current theme
            _currentTheme = ThemeService.CurrentTheme;

            // Get all available themes
            _availableThemes = ThemeService.GetAvailableThemes().ToList();

            // Get unique categories
            _themeCategories = _availableThemes.Select(t => t.Category).Distinct().OrderBy(c => c).ToList();

            // Subscribe to theme changes
            ThemeService.ThemeChanged += OnThemeChanged;

            await base.OnInitializedAsync();
        }

        private async Task SelectTheme(ITheme theme)
        {
            if (_isLoading || theme.Id == _currentTheme?.Id)
                return;

            try
            {
                _isLoading = true;
                StateHasChanged();

                await ThemeService.SetThemeAsync(theme.Id);
            }
            catch (Exception ex)
            {
                // Handle error - could add toast notification here
                Console.WriteLine($"Error switching theme: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
                StateHasChanged();
            }
        }

        private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
        {
            _currentTheme = e.NewTheme;
            InvokeAsync(StateHasChanged);
        }

        private string GetCategoryDisplayName(ThemeCategory category)
        {
            return category switch
            {
                ThemeCategory.Modern => "Modern",
                ThemeCategory.Windows => "Windows",
                ThemeCategory.MacOS => "macOS",
                ThemeCategory.Linux => "Linux",
                ThemeCategory.Retro => "Retro / Hacker",
                ThemeCategory.Gaming => "Gaming",
                ThemeCategory.Custom => "Custom",
                _ => category.ToString()
            };
        }

        public void Dispose()
        {
            ThemeService.ThemeChanged -= OnThemeChanged;
        }
    }
}
