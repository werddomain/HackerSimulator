using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Settings;

namespace HackerOs.Platform.Core.Appearance;

/// <summary>
/// Defines the protected appearance document exposed at <c>/etc/hackeros/appearance.json</c>.
/// The version-2 document keeps separate desktop and mobile theme choices plus the shared accent
/// and animation preferences so changing form factor does not discard either selection.
/// </summary>
public static class AppearanceSettingsDocuments
{
    /// <summary>Gets the canonical OS-administrator repository identity.</summary>
    public static SettingsDocumentKey Key { get; } = SettingsDocumentKey.ForOsAdmin("appearance");

    /// <summary>Gets the registered virtual path of the protected appearance document.</summary>
    public static VirtualPath Path { get; } = VirtualPath.Parse("/etc/hackeros/appearance.json");

    /// <summary>Gets the clean-profile default document content.</summary>
    public const string EmptyDocumentContent =
        "{\"schemaVersion\":2,\"desktopThemeId\":\"hackeros\",\"mobileThemeId\":\"android\",\"accent\":\"green\",\"animationsEnabled\":true}";

    /// <summary>Creates the clean-profile document definition for a settings service registration.</summary>
    /// <returns>
    /// A definition any authenticated user may read or write (desktop appearance is a routine
    /// per-session preference, not an administrator-only policy).
    /// </returns>
    public static SettingsDocumentDefinition CreateDefinition() => new(
        Path,
        Key,
        EmptyDocumentContent,
        "application/json",
        AppCapabilities.SettingsSystemRead,
        AppCapabilities.SettingsSystemWrite,
        AppAuthority.User,
        AppAuthority.User,
        new AppearanceSettingsValidator(),
        SyncEligible: true);
}
