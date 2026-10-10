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

    /// <summary>Gets the declared schema; theme and accent ids are closed sets from <see cref="ThemeCatalog"/>.</summary>
    public static SettingsSchema Schema { get; } = new(
        schemaVersion: 2,
        fields:
        [
            new SettingFieldDeclaration(
                DesktopThemeKey,
                SettingValueType.Enum,
                ThemePreferences.Default.DesktopThemeId,
                SettingSensitivity.Public,
                [.. ThemeCatalog.DesktopThemes.Select(static theme => theme.Id)]),
            new SettingFieldDeclaration(
                MobileThemeKey,
                SettingValueType.Enum,
                ThemePreferences.Default.MobileThemeId,
                SettingSensitivity.Public,
                [.. ThemeCatalog.MobileThemes.Select(static theme => theme.Id)]),
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
