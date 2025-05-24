using MudBlazor;

namespace HackerSimulator.Wasm.Core
{
    /// <summary>
    /// Provides the MudBlazor <see cref="MudTheme"/> used throughout the application.
    /// </summary>
    public class ThemeService
    {
        /// <summary>
        /// Gets the dark hacker style theme instance.
        /// </summary>
        public MudTheme CurrentTheme { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeService"/> class.
        /// </summary>
        public ThemeService()
        {
            CurrentTheme = new MudTheme()
            {
                PaletteDark = new PaletteDark
                {
                    Primary = Colors.Green.Accent4,
                    Background = "#111111",
                    Surface = "#222222",
                    TextPrimary = "#ffffff",
                    AppbarBackground = "#1b1b1b",
                    DrawerBackground = "#1b1b1b",
                }
            };
        }
    }
}
