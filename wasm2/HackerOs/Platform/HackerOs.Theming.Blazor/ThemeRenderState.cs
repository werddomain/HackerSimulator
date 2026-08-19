using HackerOs.Theming.Abstractions;

namespace HackerOs.Theming.Blazor;

/// <summary>
/// Holds the bounded strings emitted as data attributes by theme-rendering components. Keeping
/// this validation in one place prevents previews and the live shell from drifting apart.
/// </summary>
internal sealed record ThemeRenderState(
    ThemeDefinition Theme,
    string FormFactorId,
    string AccentId,
    string MotionId)
{
    /// <summary>Validates a theme request and converts it to safe, catalog-backed attribute values.</summary>
    internal static ThemeRenderState Create(
        string? themeId,
        ThemePlatform? requiredPlatform,
        string? accentId,
        bool animationsEnabled)
    {
        if (!ThemeCatalog.TryGet(themeId, out ThemeDefinition? theme))
        {
            throw new ArgumentException($"Unknown theme ID '{themeId}'.", nameof(themeId));
        }

        ThemeDefinition resolvedTheme = theme;

        if (requiredPlatform is { } platform && resolvedTheme.Platform != platform)
        {
            throw new ArgumentException(
                $"Theme '{resolvedTheme.Id}' targets {resolvedTheme.Platform}, not the requested {platform} form factor.",
                nameof(themeId));
        }

        if (!ThemeCatalog.IsKnownAccent(accentId))
        {
            throw new ArgumentException($"Unknown accent ID '{accentId}'.", nameof(accentId));
        }

        return new ThemeRenderState(
            resolvedTheme,
            resolvedTheme.Platform == ThemePlatform.Desktop ? "desktop" : "mobile",
            accentId!,
            animationsEnabled ? "enabled" : "reduced");
    }
}
