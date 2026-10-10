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

    /// <summary>Launch another enabled application through the intent service.</summary>
    public const string AppsLaunch = "apps.launch";

    /// <summary>
    /// Install, uninstall, or reinstall applications. Reserved for the public app-management API of
    /// phase 4 (ADR 0048 section 4); classified dangerous by ADR 0049.
    /// </summary>
    public const string AppsManage = "apps.manage";

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

    /// <summary>
    /// Read from the simulated network: perform DNS lookups, host enumeration,
    /// port scans, and navigate to simulated websites. Never makes real network calls.
    /// </summary>
    public const string NetworkSimulatedRead = "network.simulated.read";

    /// <summary>
    /// Send simulated HTTP POST requests and modify simulated network state
    /// (e.g., submit forms to simulated websites).
    /// Never makes real network calls.
    /// </summary>
    public const string NetworkSimulatedWrite = "network.simulated.write";

    /// <summary>
    /// Access the Game Domain engine, missions, hardware upgrades, and simulated security state.
    /// </summary>
    public const string GameplayDomainAccess = "gameplay.domain.access";

    /// <summary>
    /// Reach real hosts outside the simulated network, via the optional server's proxy (ADR 0028),
    /// when a device is connected. Distinct from <see cref="NetworkSimulatedRead"/> so real network
    /// reach is independently auditable/grantable.
    /// </summary>
    public const string NetworkRealAccess = "network.real.access";

    /// <summary>
    /// Exact classification of every catalog capability (ADR 0049 section 1). The catalog is the
    /// key set of this table, so a capability cannot be added without choosing its risk.
    /// </summary>
    private static readonly FrozenDictionary<string, CapabilityDescription> Descriptions = new CapabilityDescription[]
    {
        Normal(FileSystemPrivateRead, "Lire ses données privées"),
        Normal(FileSystemPrivateWrite, "Écrire ses données privées"),
        Normal(FileSystemUserSelectedRead, "Lire les fichiers choisis par l'utilisateur"),
        Normal(FileSystemUserSelectedWrite, "Écrire les fichiers choisis par l'utilisateur"),
        Normal(FileSystemUserHomeRead, "Lire le dossier personnel"),
        Normal(FileSystemUserHomeWrite, "Écrire dans le dossier personnel"),
        Dangerous(
            FileSystemSystemRead,
            "Lire les fichiers du système",
            "Lire des fichiers hors du dossier personnel, dont la configuration du système."),
        Dangerous(
            FileSystemSystemWrite,
            "Modifier les fichiers du système",
            "Modifier des fichiers du système, dont /etc et les paramètres de toutes les apps."),
        Normal(AppsLaunch, "Lancer d'autres applications"),
        Dangerous(
            AppsManage,
            "Gérer les applications",
            "Installer, désinstaller ou réinstaller des applications."),
        Normal(DialogFileOpen, "Afficher la boîte « Ouvrir un fichier »"),
        Normal(DialogFileSave, "Afficher la boîte « Enregistrer sous »"),
        Normal(DialogFolderSelect, "Afficher la boîte « Choisir un dossier »"),
        Normal(FileAssociationsRead, "Lire les associations de fichiers"),
        Dangerous(
            FileAssociationsWrite,
            "Modifier les associations de fichiers",
            "Choisir quelle app ouvre chaque type de fichier pour toute la machine."),
        Normal(ProcessList, "Lister les processus"),
        Dangerous(
            ProcessManage,
            "Gérer les processus",
            "Arrêter les processus d'autres applications."),
        Dangerous(
            ServicesManage,
            "Gérer les services",
            "Démarrer, arrêter et configurer les services du système."),
        Dangerous(
            WindowsManage,
            "Gérer les fenêtres",
            "Lire, déplacer ou fermer les fenêtres d'autres applications."),
        Normal(NotificationsPost, "Afficher des notifications"),
        Normal(ClipboardRead, "Lire le presse-papiers"),
        Normal(ClipboardWrite, "Écrire dans le presse-papiers"),
        Normal(NetworkSimulatedRead, "Consulter le réseau simulé"),
        Normal(NetworkSimulatedWrite, "Modifier le réseau simulé"),
        Dangerous(
            NetworkRealAccess,
            "Accéder au vrai réseau",
            "Joindre de vrais serveurs sur Internet par le serveur relais."),
        Normal(GameplayDomainAccess, "Accéder au moteur de jeu")
    }.ToFrozenDictionary(description => description.Capability, StringComparer.Ordinal);

    private static readonly FrozenSet<string> KnownIdentifiers = Descriptions.Keys.ToFrozenSet(StringComparer.Ordinal);

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

    /// <summary>Gets the exact risk class of one catalog capability (ADR 0049 section 1).</summary>
    /// <param name="capability">Exact catalog capability identifier.</param>
    /// <returns>The capability's risk class.</returns>
    /// <exception cref="ArgumentException">The identifier is not in the catalog.</exception>
    public static CapabilityRisk GetRisk(string capability) => Describe(capability).Risk;

    /// <summary>
    /// Determines whether a capability is dangerous: a non-system app receives it only with a
    /// recorded administrator approval (ADR 0049 section 2).
    /// </summary>
    /// <param name="capability">Capability identifier to inspect.</param>
    /// <returns>
    /// <see langword="true"/> only for a catalog capability classified
    /// <see cref="CapabilityRisk.Dangerous"/>. Identifiers outside the catalog, such as the bus
    /// topic permissions a manifest may also list (ADR 0040), are never dangerous.
    /// </returns>
    public static bool IsDangerous(string capability) =>
        capability is not null
        && Descriptions.TryGetValue(capability, out CapabilityDescription? description)
        && description.Risk == CapabilityRisk.Dangerous;

    /// <summary>Gets the user-facing title and risk of one catalog capability, for the approval UI.</summary>
    /// <param name="capability">Exact catalog capability identifier.</param>
    /// <returns>The capability's description.</returns>
    /// <exception cref="ArgumentException">The identifier is not in the catalog.</exception>
    public static CapabilityDescription Describe(string capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        return Descriptions.TryGetValue(capability, out CapabilityDescription? description)
            ? description
            : throw new ArgumentException($"Unknown capability '{capability}'.", nameof(capability));
    }

    private static CapabilityDescription Normal(string capability, string title) =>
        new(capability, title, CapabilityRisk.Normal, RiskDescription: null);

    private static CapabilityDescription Dangerous(string capability, string title, string riskDescription) =>
        new(capability, title, CapabilityRisk.Dangerous, riskDescription);
}

/// <summary>Risk class of a catalog capability (ADR 0049 section 1).</summary>
public enum CapabilityRisk
{
    /// <summary>Granted by default to any app that declares it.</summary>
    Normal = 0,

    /// <summary>Granted to a non-system app only after an administrator approval.</summary>
    Dangerous = 1
}

/// <summary>User-facing description of one catalog capability.</summary>
/// <param name="Capability">Exact capability identifier.</param>
/// <param name="Title">Short title shown in the approval window and the Settings app.</param>
/// <param name="Risk">Risk class of the capability.</param>
/// <param name="RiskDescription">
/// Sentence explaining the risk to an administrator; <see langword="null"/> for a normal capability.
/// </param>
public sealed record CapabilityDescription(
    string Capability,
    string Title,
    CapabilityRisk Risk,
    string? RiskDescription);

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