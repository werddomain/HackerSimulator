namespace HackerOs.App.Abstractions;

/// <summary>
/// Who may provide a component contract (ADR 0148 §3.3). Most contracts are open to any app, like
/// <c>hackeros.terminal</c> (ADR 0063); the controls and dialogs the OS shows everywhere are
/// reserved, so a third-party app can never stand in for the file view or the context menu.
/// </summary>
public enum AppComponentProviderScope
{
    /// <summary>Any installed window app may register a component for the contract.</summary>
    AnyApp,

    /// <summary>
    /// Only a system app of the host (<c>IsSystemApp</c> on its <c>ProjectReference</c>, ADR 0048)
    /// may register it; another app's registration is ignored by the OS and fails the host build.
    /// </summary>
    SystemApps,

    /// <summary>The platform provides the contract itself, without a manifest; no app may register it.</summary>
    Platform
}

/// <summary>
/// The component contracts the manifest validator and the OS registry know by name (ADR 0148), with
/// who may provide them. The SDK repeats these names in <c>HackComponentContracts</c>, next to the
/// contracts' controllers.
/// </summary>
public static class AppComponentContracts
{
    /// <summary>The shared file view (<c>IHackFileView</c>), provided by the File Explorer only.</summary>
    public const string FileView = "hackeros.file-view";

    /// <summary>The shared file dialogs (Open with, Properties, name conflicts, file pickers), provided by the File Explorer only.</summary>
    public const string FileDialogs = "hackeros.file-dialogs";

    /// <summary>The OS context menu (<c>IHackContextMenu</c>, ADR 0150), provided by the platform itself.</summary>
    public const string ContextMenu = "hackeros.context-menu";

    /// <summary>Gets who may provide <paramref name="contract"/> (ordinal comparison).</summary>
    /// <param name="contract">Contract name.</param>
    /// <returns><see cref="AppComponentProviderScope.AnyApp"/> for every contract not reserved by ADR 0148.</returns>
    public static AppComponentProviderScope GetProviderScope(string contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return contract switch
        {
            FileView or FileDialogs => AppComponentProviderScope.SystemApps,
            ContextMenu => AppComponentProviderScope.Platform,
            _ => AppComponentProviderScope.AnyApp
        };
    }
}
