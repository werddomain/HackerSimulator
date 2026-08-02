using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Settings;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Settings;

namespace HackerOs.Platform.Core.Policy;

/// <summary>
/// Defines the protected OS/admin policy document exposed at
/// <c>/etc/hackeros/policy.config</c>.
/// </summary>
/// <remarks>
/// Capability grant and settings-scope policy changes are recorded as an ordinary
/// canonical settings document so they share the same authorization, validation,
/// revision, and audit path as every other protected settings write. Only an
/// Administrator or explicit audited System operation holding
/// <see cref="AppCapabilities.SettingsSystemWrite"/> may commit a change.
/// </remarks>
public static class PolicySettingsDocuments
{
    /// <summary>Canonical document ID for the protected OS/admin policy document.</summary>
    public const string DocumentId = "policy";

    /// <summary>Gets the canonical OS-administrator repository identity.</summary>
    public static SettingsDocumentKey Key { get; } = SettingsDocumentKey.ForOsAdmin(DocumentId);

    /// <summary>Gets the projected virtual path of the protected policy document.</summary>
    public static VirtualPath Path { get; } = SettingsDocumentPathFactory.GetProjectionPath(
        Key);

    /// <summary>Gets the declared schema for the protected policy document.</summary>
    public static SettingsSchema Schema { get; } = new(
        schemaVersion: 1,
        fields:
        [
            new SettingFieldDeclaration(
                "capabilityGrantsLocked",
                SettingValueType.Boolean,
                DefaultValue: "false",
                SettingSensitivity.Public),
            new SettingFieldDeclaration(
                "allowRuntimePackageInstall",
                SettingValueType.Boolean,
                DefaultValue: "false",
                SettingSensitivity.Public)
        ]);

    /// <summary>Creates the clean-profile document definition for a settings service registration.</summary>
    /// <returns>A definition requiring Administrator write authority and the system-settings capability.</returns>
    public static SettingsDocumentDefinition CreateDefinition() => new(
        Path,
        Key,
        ConfigDocumentFormat.Serialize(Schema, new Dictionary<string, string>(StringComparer.Ordinal)),
        "text/x-hackeros-config",
        AppCapabilities.SettingsSystemRead,
        AppCapabilities.SettingsSystemWrite,
        AppAuthority.Administrator,
        AppAuthority.Administrator,
        new SchemaConfigSettingsDocumentValidator(Schema));
}
