using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Settings;

namespace HackerOs.Platform.Core.Shell;

/// <summary>
/// Defines the protected UI-platform-preference document exposed at
/// <c>/etc/hackeros/ui-platform-preference.json</c>, holding whether the shell follows automatic
/// platform detection or an explicit Desktop/Mobile choice (see
/// docs/mobile-interface-platform-plan.md §6). Registered under <see cref="AppAuthority.User"/>
/// like other routine session preferences (compare <c>AppearanceSettingsDocuments</c>), but marked
/// <see langword="false"/> for <see cref="SettingsDocumentDefinition.SyncEligible"/> — the doc
/// requires this choice stay device-local and never roam between a user's devices.
/// </summary>
public static class UiPlatformPreferenceSettingsDocuments
{
    /// <summary>Gets the canonical OS-administrator repository identity.</summary>
    public static SettingsDocumentKey Key { get; } = SettingsDocumentKey.ForOsAdmin("ui-platform-preference");

    /// <summary>Gets the registered virtual path of the protected UI-platform-preference document.</summary>
    public static VirtualPath Path { get; } = VirtualPath.Parse("/etc/hackeros/ui-platform-preference.json");

    /// <summary>Gets the clean-profile default document content: automatic detection, no explicit choice.</summary>
    public const string EmptyDocumentContent = "{\"schemaVersion\":1,\"selectionSource\":\"auto\",\"explicitPlatformId\":null}";

    /// <summary>Creates the clean-profile document definition for a settings service registration.</summary>
    public static SettingsDocumentDefinition CreateDefinition() => new(
        Path,
        Key,
        EmptyDocumentContent,
        "application/json",
        AppCapabilities.SettingsSystemRead,
        AppCapabilities.SettingsSystemWrite,
        AppAuthority.User,
        AppAuthority.User,
        new UiPlatformPreferenceValidator(),
        SyncEligible: false);
}
