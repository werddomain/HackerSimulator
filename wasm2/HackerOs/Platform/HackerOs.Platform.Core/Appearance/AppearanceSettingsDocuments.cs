using HackerOs.Platform.Core.Settings;
using HackerOs.Simulation.Abstractions.Settings;
using HackerOs.Theming.Abstractions;

namespace HackerOs.Platform.Core.Appearance;

/// <summary>
/// Defines the layered appearance document (ADR 0044 section 8):
/// <c>/etc/hackeros/appearance.config</c> holds the machine default, and each user overrides it in
/// <c>~/.config/hackeros/appearance.config</c>. Desktop and mobile themes are kept separately so
/// changing form factor does not discard either selection.
/// </summary>
public static class AppearanceSettingsDocuments
{
    /// <summary>Key of the desktop theme id.</summary>
    public const string DesktopThemeKey = "desktopThemeId";

    /// <summary>Key of the mobile theme id.</summary>
    public const string MobileThemeKey = "mobileThemeId";

    /// <summary>Key of the accent id.</summary>
    public const string AccentKey = "accent";

    /// <summary>Key of the animations switch.</summary>
    public const string AnimationsKey = "animationsEnabled";

    /// <summary>Gets the declared schema; theme ids are open (built-in or user themes, ADR 0103), accent ids a closed set from <see cref="ThemeCatalog"/>.</summary>
    public static SettingsSchema Schema { get; } = new(
        schemaVersion: 2,
        fields:
        [
            // Theme ids are open strings since ADR 0103: a user theme of /etc/hackeros/themes or
            // ~/.config/hackeros/themes may be selected by its id. The shell resolves an id that
            // names no theme (or a theme of the other form factor) to the default theme
            // (ResolvedTheme.Resolve), as a desktop does with a deleted theme.
            new SettingFieldDeclaration(
                DesktopThemeKey,
                SettingValueType.String,
                ThemePreferences.Default.DesktopThemeId,
                SettingSensitivity.Public),
            new SettingFieldDeclaration(
                MobileThemeKey,
                SettingValueType.String,
                ThemePreferences.Default.MobileThemeId,
                SettingSensitivity.Public),
            new SettingFieldDeclaration(
                AccentKey,
                SettingValueType.Enum,
                ThemePreferences.Default.AccentId,
                SettingSensitivity.Public,
                [.. ThemeCatalog.AccentIds]),
            new SettingFieldDeclaration(
                AnimationsKey,
                SettingValueType.Boolean,
                ThemePreferences.Default.AnimationsEnabled ? "true" : "false",
                SettingSensitivity.Public)
        ]);

    /// <summary>Gets the layered document.</summary>
    public static OsSettingsDocument Document { get; } = new("appearance", Schema);

    /// <summary>Builds preferences from a merged snapshot (always valid: invalid lines fell back to the next layer).</summary>
    /// <param name="snapshot">Snapshot of <see cref="Document"/>.</param>
    public static ThemePreferences ToPreferences(LayeredConfigSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new ThemePreferences(
            snapshot.Values[DesktopThemeKey],
            snapshot.Values[MobileThemeKey],
            snapshot.Values[AccentKey],
            snapshot.Values[AnimationsKey] == "true");
    }
}
