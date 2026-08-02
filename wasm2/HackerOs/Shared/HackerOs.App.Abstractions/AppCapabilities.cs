using System.Collections.Frozen;

namespace HackerOs.App.Abstractions;

/// <summary>
/// Defines capability identifiers understood by the current App SDK.
/// </summary>
/// <remarks>
/// Capability grants use exact, case-sensitive matching. Resource restrictions
/// are represented by policy constraints rather than wildcard identifiers.
/// </remarks>
public static class AppCapabilities
{
    /// <summary>Read files in the app's private data scope.</summary>
    public const string FileSystemPrivateRead = "filesystem.private.read";

    /// <summary>Write files in the app's private data scope.</summary>
    public const string FileSystemPrivateWrite = "filesystem.private.write";

    /// <summary>Read files explicitly selected by the user.</summary>
    public const string FileSystemUserSelectedRead = "filesystem.user-selected.read";

    /// <summary>Write files explicitly selected by the user.</summary>
    public const string FileSystemUserSelectedWrite = "filesystem.user-selected.write";

    /// <summary>Read broadly from the current user's home directory.</summary>
    public const string FileSystemUserHomeRead = "filesystem.user-home.read";

    /// <summary>Write broadly to the current user's home directory.</summary>
    public const string FileSystemUserHomeWrite = "filesystem.user-home.write";

    /// <summary>Read protected system filesystem paths.</summary>
    public const string FileSystemSystemRead = "filesystem.system.read";

    /// <summary>Write protected system filesystem paths.</summary>
    public const string FileSystemSystemWrite = "filesystem.system.write";

    /// <summary>Read settings in scopes declared by the app manifest.</summary>
    public const string SettingsRead = "settings.read";

    /// <summary>Write settings in scopes declared by the app manifest.</summary>
    public const string SettingsWrite = "settings.write";

    /// <summary>Read protected OS settings and file associations.</summary>
    public const string SettingsSystemRead = "settings.system.read";

    /// <summary>Write protected OS settings and file associations.</summary>
    public const string SettingsSystemWrite = "settings.system.write";

    /// <summary>Launch another enabled application through the intent service.</summary>
    public const string AppsLaunch = "apps.launch";

    /// <summary>Display the ecosystem-owned file-open dialog.</summary>
    public const string DialogFileOpen = "dialogs.file-open";

    /// <summary>Display the ecosystem-owned file-save dialog.</summary>
    public const string DialogFileSave = "dialogs.file-save";

    /// <summary>Display the ecosystem-owned folder-selection dialog.</summary>
    public const string DialogFolderSelect = "dialogs.folder-select";

    /// <summary>Read the effective file-association policy.</summary>
    public const string FileAssociationsRead = "file-associations.read";

    /// <summary>Modify protected file-association policy.</summary>
    public const string FileAssociationsWrite = "file-associations.write";

    /// <summary>Enumerate processes owned by other applications.</summary>
    public const string ProcessList = "process.list";

    /// <summary>Start, stop, or kill processes owned by other applications.</summary>
    public const string ProcessManage = "process.manage";

    /// <summary>Post a notification visible to the user.</summary>
    public const string NotificationsPost = "notifications.post";

    /// <summary>List, focus, minimize, or close windows owned by other applications.</summary>
    public const string WindowsManage = "windows.manage";

    /// <summary>Read the current shared clipboard contents.</summary>
    public const string ClipboardRead = "clipboard.read";

    /// <summary>Replace the shared clipboard contents.</summary>
    public const string ClipboardWrite = "clipboard.write";

    /// <summary>Start, stop, or query session-scoped service apps other than the caller.</summary>
    public const string ServicesManage = "services.manage";

    private static readonly FrozenSet<string> KnownIdentifiers = new[]
    {
        FileSystemPrivateRead,
        FileSystemPrivateWrite,
        FileSystemUserSelectedRead,
        FileSystemUserSelectedWrite,
        FileSystemUserHomeRead,
        FileSystemUserHomeWrite,
        FileSystemSystemRead,
        FileSystemSystemWrite,
        SettingsRead,
        SettingsWrite,
        SettingsSystemRead,
        SettingsSystemWrite,
        AppsLaunch,
        DialogFileOpen,
        DialogFileSave,
        DialogFolderSelect,
        FileAssociationsRead,
        FileAssociationsWrite,
        ProcessList,
        ProcessManage,
        NotificationsPost,
        WindowsManage,
        ClipboardRead,
        ClipboardWrite,
        ServicesManage
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Gets the capabilities that require a window-hosting app because they render modal UI.</summary>
    private static readonly FrozenSet<string> WindowOnlyIdentifiers = new[]
    {
        DialogFileOpen,
        DialogFileSave,
        DialogFolderSelect
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Determines whether a capability requires a window-hosting app because it renders modal UI.</summary>
    /// <param name="capability">Capability identifier to inspect.</param>
    /// <returns><see langword="true"/> only for capabilities that require <see cref="AppKind.Window"/>.</returns>
    public static bool RequiresWindowKind(string capability) => WindowOnlyIdentifiers.Contains(capability);

    /// <summary>Gets every capability recognized by this App SDK version.</summary>
    public static IReadOnlySet<string> All { get; } = KnownIdentifiers;

    /// <summary>Determines whether an identifier is recognized by this App SDK version.</summary>
    /// <param name="capability">Capability identifier to inspect.</param>
    /// <returns><see langword="true"/> only for an exact known identifier.</returns>
    public static bool IsKnown(string capability) => KnownIdentifiers.Contains(capability);
}

/// <summary>
/// Evaluates capability grants using the mandatory exact-match semantics.
/// </summary>
public static class AppCapabilityPolicy
{
    /// <summary>Determines whether the requested capability was granted exactly.</summary>
    /// <param name="grantedCapabilities">Capabilities granted by trusted OS policy.</param>
    /// <param name="requestedCapability">Capability required by an operation.</param>
    /// <returns><see langword="true"/> when an exact case-sensitive grant exists.</returns>
    public static bool IsGranted(
        IEnumerable<string> grantedCapabilities,
        string requestedCapability)
    {
        ArgumentNullException.ThrowIfNull(grantedCapabilities);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedCapability);

        return grantedCapabilities.Contains(requestedCapability, StringComparer.Ordinal);
    }
}