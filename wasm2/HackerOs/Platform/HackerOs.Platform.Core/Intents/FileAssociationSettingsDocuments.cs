using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Settings;

namespace HackerOs.Platform.Core.Intents;

/// <summary>
/// Defines the protected file-association document exposed at
/// <c>/etc/hackeros/file-associations.json</c>, per `P1-APP-008A`.
/// </summary>
/// <remarks>
/// Per ADR 0011, this single registered path is the sole approved strict-JSON exception to the
/// otherwise Linux-like <c>.config</c> settings format; it is never duplicated as an independent
/// ordinary file. Each entry stores an explicit, administrator-configured default handler for one
/// extension or media type. Apps that are not named here are still discoverable candidates
/// through their own manifest's <see cref="FileHandlerManifest"/> declarations
/// (see <see cref="FileAssociationIndex"/> and <see cref="FileAssociationResolver"/>).
/// </remarks>
public static class FileAssociationSettingsDocuments
{
    /// <summary>Gets the canonical OS-administrator repository identity.</summary>
    public static SettingsDocumentKey Key { get; } = SettingsDocumentKey.ForOsAdmin("file-associations");

    /// <summary>Gets the registered virtual path of the protected file-association document.</summary>
    public static VirtualPath Path { get; } = VirtualPath.Parse("/etc/hackeros/file-associations.json");

    /// <summary>Gets the clean-profile empty document content.</summary>
    public const string EmptyDocumentContent = "{\"schemaVersion\":1,\"associations\":[]}";

    /// <summary>Creates the clean-profile document definition for a settings service registration.</summary>
    /// <returns>
    /// A definition any authenticated user may read (resolving file associations is a routine
    /// user operation), but that requires Administrator write authority to change, per ADR 0011.
    /// </returns>
    public static SettingsDocumentDefinition CreateDefinition() => new(
        Path,
        Key,
        EmptyDocumentContent,
        "application/json",
        AppCapabilities.FileAssociationsRead,
        AppCapabilities.FileAssociationsWrite,
        AppAuthority.User,
        AppAuthority.Administrator,
        new FileAssociationSettingsValidator());
}
