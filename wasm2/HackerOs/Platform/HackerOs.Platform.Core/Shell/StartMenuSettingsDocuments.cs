using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Settings;

namespace HackerOs.Platform.Core.Shell;

/// <summary>
/// Defines the OS-owned, per-user start-menu preference document exposed at
/// <c>/etc/hackeros/start-menu.json</c>. Profiles share one boot-time registration while remaining
/// isolated by opaque local-user ID inside the document.
/// </summary>
public static class StartMenuSettingsDocuments
{
    /// <summary>The maximum number of ordered quick-launch pins stored for one local user.</summary>
    public const int MaximumPinnedAppCount = 64;

    /// <summary>Gets the canonical OS-administrator repository identity.</summary>
    public static SettingsDocumentKey Key { get; } = SettingsDocumentKey.ForOsAdmin("start-menu");

    /// <summary>Gets the registered virtual path of the protected start-menu document.</summary>
    public static VirtualPath Path { get; } = VirtualPath.Parse("/etc/hackeros/start-menu.json");

    /// <summary>Gets the clean-profile document containing no per-user pins.</summary>
    public const string EmptyDocumentContent = "{\"schemaVersion\":1,\"profiles\":{}}";

    /// <summary>Creates the protected, device-local document definition for settings registration.</summary>
    /// <returns>
    /// A system-authority-only definition. Shell code mediates access for the active principal so
    /// one user cannot edit another user's profile through the aggregated document.
    /// </returns>
    public static SettingsDocumentDefinition CreateDefinition() => new(
        Path,
        Key,
        EmptyDocumentContent,
        "application/json",
        AppCapabilities.SettingsSystemRead,
        AppCapabilities.SettingsSystemWrite,
        AppAuthority.System,
        AppAuthority.System,
        new StartMenuSettingsValidator(),
        SyncEligible: false);
}
