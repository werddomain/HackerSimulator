using HackerOs.App.Abstractions;

namespace HackerOs.AppSdk.Apps;

/// <summary>
/// Read access to the app catalog for one app instance (ADR 0056, section 2), behind
/// <see cref="HackApps"/>. Needs no capability: the list of installed apps is public, like
/// <c>/usr/share/applications</c>.
/// </summary>
public interface IAppCatalogGateway
{
    /// <summary>
    /// Raised after an app was installed, uninstalled or reinstalled for the session user (an
    /// <c>apps.config</c> change, ADR 0048). Handlers run inside the subscribing app's ambient scope.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>Gets every app installed for the session user, sorted by name. Uninstalled apps are hidden (ADR 0048).</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    ValueTask<IReadOnlyList<HackAppInfo>> GetInstalledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the installed window apps that declare they can open <paramref name="path"/> ("Open
    /// with"): the default configured in <c>/etc/hackeros/file-associations.json</c> first, then the
    /// others by name.
    /// </summary>
    /// <param name="path">File to open.</param>
    /// <param name="mediaType">MIME type when known, matched besides the extension.</param>
    /// <param name="cancellationToken">Cancels the read of the associations file.</param>
    ValueTask<IReadOnlyList<HackAppInfo>> GetHandlersAsync(
        VirtualPath path, string? mediaType = null, CancellationToken cancellationToken = default);
}

/// <summary>Installation state of an app for the session user (ADR 0048, section 4).</summary>
public enum HackAppInstallState
{
    /// <summary>Installed, or a system app.</summary>
    Installed = 0,

    /// <summary>Uninstalled in the user's own <c>~/.config/hackeros/apps.config</c>, or by a dependency there.</summary>
    UninstalledForUser = 1,

    /// <summary>Uninstalled in <c>/etc/hackeros/apps.config</c> for every user: only an administrator reinstalls it.</summary>
    UninstalledForMachine = 2
}

/// <summary>Which <c>apps.config</c> file an uninstall or reinstall writes (ADR 0048, section 3).</summary>
public enum HackInstallScope
{
    /// <summary><c>~/.config/hackeros/apps.config</c>: the session user only.</summary>
    User = 0,

    /// <summary><c>/etc/hackeros/apps.config</c>: every user; an administrator must authenticate (ADR 0055, section 5).</summary>
    Machine = 1
}

/// <summary>One catalog app with its installation state (ADR 0056, section 3).</summary>
/// <param name="App">Public description of the app.</param>
/// <param name="State">Installation state for the session user.</param>
/// <param name="UninstallClosure">
/// Every app an uninstall would remove: the apps that need it through non-optional dependencies,
/// dependents first, then the app itself. A confirmation dialog lists them.
/// </param>
public sealed record HackAppInstallInfo(HackAppInfo App, HackAppInstallState State, IReadOnlyList<string> UninstallClosure);

/// <summary>
/// Uninstall and reinstall for one app instance (ADR 0056, section 3), behind
/// <see cref="HackAppManagement"/>. Every member requires the dangerous capability
/// <c>apps.manage</c>; the machine scope also requires an administrator through the OS
/// authentication agent, unless the process already runs as root.
/// </summary>
public interface IAppManagementGateway
{
    /// <summary>Gets every catalog app (installed or not) with its state, sorted by name.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    ValueTask<IReadOnlyList<HackAppInstallInfo>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Uninstalls an app and its dependents; returns every app ID written, in order.</summary>
    /// <param name="appId">Exact catalog app ID.</param>
    /// <param name="scope">File to write.</param>
    /// <param name="cancellationToken">Cancels the authentication and the write.</param>
    ValueTask<IReadOnlyList<string>> UninstallAsync(
        string appId, HackInstallScope scope, CancellationToken cancellationToken = default);

    /// <summary>Reinstalls an app and its uninstalled dependencies; returns every app ID written, in order.</summary>
    /// <param name="appId">Exact catalog app ID.</param>
    /// <param name="scope">File to write.</param>
    /// <param name="cancellationToken">Cancels the authentication and the write.</param>
    ValueTask<IReadOnlyList<string>> ReinstallAsync(
        string appId, HackInstallScope scope, CancellationToken cancellationToken = default);
}
