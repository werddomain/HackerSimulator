using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Administration;
using HackerOs.AppSdk.Apps;
using HackerOs.AppSdk.Settings;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.AppSdk;

/// <summary>
/// Provides identity, granted policy, and scoped gateway access for one app instance.
/// </summary>
/// <remarks>
/// This context deliberately does not expose the root service provider, raw repositories,
/// unrestricted <c>IJSRuntime</c>, or any other concrete platform implementation. Every gateway
/// property is a narrow, capability-checked surface bound to this instance only. App code never
/// constructs this context itself; only the trusted platform-owned factory does
/// (`P1-EXEC-007`).
/// </remarks>
public interface IAppExecutionContext
{
    /// <summary>Gets the validated manifest for the running application.</summary>
    AppManifest Manifest { get; }

    /// <summary>Gets the unique identifier for this running instance.</summary>
    Guid InstanceId { get; }

    /// <summary>Gets the authenticated user identifier that initiated the operation.</summary>
    string UserId { get; }

    /// <summary>
    /// Gets the acting user's canonical home directory (<c>/home/{login}</c>), used by
    /// <see cref="HackExecutionContext"/> as the default working directory and by
    /// <see cref="IO.HackPath.GetFullPath(string)"/> to expand <c>~</c>, and as the root of the user settings layer
    /// (<c>~/.config/&lt;appId&gt;/</c>, ADR 0044). Defaults to
    /// <c>/home/{UserId}</c> so existing implementations keep compiling; the trusted platform
    /// factory overrides it with the principal's real home path (ADR 0043).
    /// </summary>
    string HomeDirectory => $"/home/{UserId}";

    /// <summary>
    /// Gets whether this one launch runs as full root through <c>sudo</c> (ADR 0042). The effective
    /// user is then <c>root</c>, whose home is <c>/root</c>: per-user data written on its behalf,
    /// such as the trash (ADR 0052), goes there rather than under <see cref="HomeDirectory"/>.
    /// Defaults to <see langword="false"/> so existing implementations keep compiling; the trusted
    /// platform factory sets it from the launch.
    /// </summary>
    bool IsRootElevated => false;

    /// <summary>
    /// Gets the session user's login name, read by <see cref="HackUser"/> (ADR 0056, section 1).
    /// Defaults to the process environment's user name so existing implementations keep compiling;
    /// the trusted platform factory sets it from the authenticated principal.
    /// </summary>
    string UserLoginName => Environment.UserName;

    /// <summary>
    /// Gets the session user's display name, read by <see cref="HackUser"/> (ADR 0056, section 1).
    /// Defaults to <see cref="UserLoginName"/>; the trusted platform factory sets it from the
    /// authenticated principal.
    /// </summary>
    string UserDisplayName => UserLoginName;

    /// <summary>Gets the acting user's authority for this operation.</summary>
    AppAuthority UserAuthority { get; }

    /// <summary>Gets the capabilities granted to this app instance by OS policy.</summary>
    IReadOnlySet<string> GrantedCapabilities { get; }

    /// <summary>Gets the session this app instance belongs to.</summary>
    SessionId SessionId { get; }

    /// <summary>Gets the process record hosting this app instance.</summary>
    ProcessId ProcessId { get; }

    /// <summary>
    /// Gets the cancellation token owned by this app instance's process, cancelled on close, kill,
    /// logout, or shutdown.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>Gets a capability checker scoped to this app/user, exposing no other app's grants.</summary>
    ICapabilityChecker Capabilities { get; }

    /// <summary>Gets this instance's authorized, capability-checked filesystem gateway.</summary>
    IAppFileSystemGateway FileSystem { get; }

    /// <summary>Gets this instance's typed event bus gateway.</summary>
    IAppEventGateway Events { get; }

    /// <summary>Gets this instance's authorized notification-posting gateway.</summary>
    IAppNotificationGateway Notifications { get; }

    /// <summary>
    /// Gets this instance's structured diagnostic logging gateway. Entries land in
    /// <c>/var/log/apps/&lt;appId&gt;.log</c> (ADR 0045); reading logs back is plain file access.
    /// </summary>
    IAppLoggingGateway Logging { get; }

    /// <summary>Gets this instance's read-only deterministic simulation clock gateway.</summary>
    IAppClockGateway Clock { get; }

    /// <summary>Gets this instance's authorized process/job gateway.</summary>
    IAppProcessGateway Processes { get; }

    /// <summary>
    /// Gets this instance's authorized ability to launch another installed application.
    /// Defaults to an unsupported gateway so existing <see cref="IAppExecutionContext"/>
    /// implementations that predate this member keep compiling; the trusted platform factory
    /// overrides it with a real implementation.
    /// </summary>
    IAppIntentGateway Intents => new UnsupportedAppIntentGateway();

    /// <summary>
    /// Gets this instance's authorized directory-change watch gateway. Defaults to an unsupported
    /// gateway so existing <see cref="IAppExecutionContext"/> implementations that predate this member
    /// keep compiling; the trusted platform factory overrides it with a real implementation.
    /// </summary>
    IAppFileSystemWatchGateway Watch => new UnsupportedAppFileSystemWatchGateway();

    /// <summary>
    /// Gets this instance's authorized ability to start, stop, or reconfigure other
    /// <see cref="AppKind.Service"/> apps. Defaults to an unsupported gateway so existing
    /// <see cref="IAppExecutionContext"/> implementations that predate this member keep compiling;
    /// the trusted platform factory overrides it with a real implementation.
    /// </summary>
    IAppServiceControlGateway Services => new UnsupportedAppServiceControlGateway();

    /// <summary>
    /// Gets this instance's permission-error notification gateway. Defaults to a no-op gateway so
    /// existing <see cref="IAppExecutionContext"/> implementations that predate this member keep
    /// compiling; the trusted platform factory overrides it with a real implementation.
    /// </summary>
    IAppPermissionErrorGateway PermissionErrors => new NoOpAppPermissionErrorGateway();

    /// <summary>
    /// Gets this instance's live one-shot elevation ticket set (`ADR 0042`). An app grants into it
    /// only after the platform's admin-approval dialog verifies an enabled Administrator; a ticket
    /// unblocks exactly the next matching (capability, path) filesystem denial, then is consumed.
    /// Defaults to a fresh, always-empty instance so existing <see cref="IAppExecutionContext"/>
    /// implementations that predate this member keep compiling; the trusted platform factory
    /// overrides it with the same live instance <see cref="HackerOs.App.Abstractions.AppOperationContext"/> reads.
    /// </summary>
    OneShotElevationGrantSet OneShotElevation => new();

    /// <summary>
    /// Gets this instance's own process environment: arguments, working directory, environment
    /// variables, user and machine names (ADR 0050, section 6), read by <c>HackEnvironment</c>.
    /// Defaults to a detached, read-only view (home directory, <c>HOME</c> only) so existing
    /// <see cref="IAppExecutionContext"/> implementations that predate this member keep compiling;
    /// the trusted platform factory overrides it with the live process entry.
    /// </summary>
    IAppEnvironmentGateway Environment => new DetachedAppEnvironmentGateway(this);

    /// <summary>
    /// Gets this instance's read access to the app catalog, behind <see cref="HackApps"/> (ADR 0056,
    /// section 2). Defaults to an unsupported gateway so existing implementations keep compiling; the
    /// trusted platform factory overrides it with a real implementation.
    /// </summary>
    IAppCatalogGateway Catalog => new UnsupportedAppCatalogGateway();

    /// <summary>
    /// Gets this instance's uninstall/reinstall gateway, behind <see cref="HackAppManagement"/>
    /// (ADR 0056, section 3; capability <c>apps.manage</c>). Defaults to an unsupported gateway so
    /// existing implementations keep compiling; the trusted platform factory overrides it.
    /// </summary>
    IAppManagementGateway AppManagement => new UnsupportedAppManagementGateway();

    /// <summary>
    /// Gets this instance's access to the OS's own settings documents, behind
    /// <see cref="HackSettings.OpenSystemAsync(string, CancellationToken)"/> (ADR 0056, section 8; no
    /// capability, file permissions decide). Defaults to an unsupported gateway so existing
    /// implementations keep compiling; the trusted platform factory overrides it.
    /// </summary>
    IAppSystemSettingsGateway SystemSettings => UnsupportedAdministrationGateways.SystemSettings;

    /// <summary>
    /// Gets this instance's dangerous-capability administration gateway, behind
    /// <see cref="HackPermissionAdministration"/> (ADR 0056, section 9; capability
    /// <c>permissions.manage</c>). Defaults to an unsupported gateway; the trusted platform factory
    /// overrides it.
    /// </summary>
    IAppPermissionAdministrationGateway PermissionAdministration => UnsupportedAdministrationGateways.Permissions;

    /// <summary>
    /// Gets this instance's optional-server connection gateway, behind
    /// <see cref="HackServerConnection"/> (ADR 0056, section 10; capability
    /// <c>server.connection.manage</c>). Defaults to an unsupported gateway; the trusted platform
    /// factory overrides it.
    /// </summary>
    IAppServerConnectionGateway ServerConnection => UnsupportedAdministrationGateways.ServerConnection;

    /// <summary>
    /// Gets this instance's reboot and factory-reset gateway, behind <see cref="HackSystem"/>
    /// (ADR 0056, section 11; capability <c>system.power</c> plus an administrator). Named
    /// <c>SystemPower</c> rather than <c>System</c> so it never hides the <c>System</c> namespace in
    /// implementing classes. Defaults to an unsupported gateway; the trusted platform factory
    /// overrides it.
    /// </summary>
    IAppSystemPowerGateway SystemPower => UnsupportedAdministrationGateways.SystemPower;

    /// <summary>
    /// Gets this instance's raw storage inspection gateway, behind
    /// <see cref="HackStorageDiagnostics"/> (ADR 0056, section 11; capability
    /// <c>system.storage.inspect</c>). Defaults to an unsupported gateway; the trusted platform
    /// factory overrides it.
    /// </summary>
    IAppStorageDiagnosticsGateway StorageDiagnostics => UnsupportedAdministrationGateways.StorageDiagnostics;
}

/// <summary>
/// Defaults of the administration gateways (ADR 0056, sections 8 to 11) for contexts that don't wire
/// them up: every call throws <see cref="NotSupportedException"/>.
/// </summary>
public static class UnsupportedAdministrationGateways
{
    private const string Message = "This IAppExecutionContext implementation does not provide this administration gateway.";

    /// <summary>Unsupported <see cref="IAppSystemSettingsGateway"/>.</summary>
    public static IAppSystemSettingsGateway SystemSettings { get; } = new UnsupportedSystemSettings();

    /// <summary>Unsupported <see cref="IAppPermissionAdministrationGateway"/>.</summary>
    public static IAppPermissionAdministrationGateway Permissions { get; } = new UnsupportedPermissions();

    /// <summary>Unsupported <see cref="IAppServerConnectionGateway"/>.</summary>
    public static IAppServerConnectionGateway ServerConnection { get; } = new UnsupportedServerConnection();

    /// <summary>Unsupported <see cref="IAppSystemPowerGateway"/>.</summary>
    public static IAppSystemPowerGateway SystemPower { get; } = new UnsupportedSystemPower();

    /// <summary>Unsupported <see cref="IAppStorageDiagnosticsGateway"/>.</summary>
    public static IAppStorageDiagnosticsGateway StorageDiagnostics { get; } = new UnsupportedStorageDiagnostics();

    private sealed class UnsupportedSystemSettings : IAppSystemSettingsGateway
    {
        public AppSystemSettingsDocumentInfo Describe(string documentId) => throw new NotSupportedException(Message);

        public ValueTask<HackerOs.Simulation.Abstractions.Settings.LayeredConfigSnapshot> ReadAsync(
            string documentId, CancellationToken cancellationToken = default) => throw new NotSupportedException(Message);

        public ValueTask SetAsync(
            string documentId,
            IReadOnlyList<KeyValuePair<string, string>> values,
            HackerOs.Simulation.Abstractions.Settings.SettingLayer target,
            CancellationToken cancellationToken = default) => throw new NotSupportedException(Message);

        public ValueTask ResetAsync(
            string documentId,
            string qualifiedKey,
            HackerOs.Simulation.Abstractions.Settings.SettingLayer target,
            CancellationToken cancellationToken = default) => throw new NotSupportedException(Message);

        public ValueTask<IAsyncDisposable> WatchAsync(
            string documentId, Func<ValueTask> onChanged, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);
    }

    private sealed class UnsupportedPermissions : IAppPermissionAdministrationGateway
    {
        public ValueTask<HackDangerousCapabilityPolicy> GetPolicyAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);

        public ValueTask<IReadOnlyList<HackAppPermissionInfo>> GetOverviewAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);

        public ValueTask SetPolicyAsync(HackDangerousCapabilityPolicy policy, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);

        public ValueTask SetMachineDecisionAsync(
            string appId, string capability, HackPermissionDecision decision, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);

        public ValueTask RemoveOwnDenialAsync(string appId, string capability, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);
    }

    private sealed class UnsupportedServerConnection : IAppServerConnectionGateway
    {
        public ValueTask<HackServerConnectionState> GetStateAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);

        public ValueTask<HackServerConnectionState> ConnectAsync(
            Uri serverUrl, string login, string password, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);

        public ValueTask<HackServerConnectionState> CreateAccountAsync(
            Uri serverUrl, string login, string password, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException(Message);

        public ValueTask SyncAsync(HackSyncDomain domain, HackSyncDirection direction, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);

        public ValueTask<IReadOnlyList<HackSyncConflict>> GetConflictsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);
    }

    private sealed class UnsupportedSystemPower : IAppSystemPowerGateway
    {
        public ValueTask RebootAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException(Message);

        public ValueTask FactoryResetAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException(Message);
    }

    private sealed class UnsupportedStorageDiagnostics : IAppStorageDiagnosticsGateway
    {
        public ValueTask<HackStorageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);
    }
}

/// <summary>Default <see cref="IAppExecutionContext.Catalog"/> for contexts that don't wire one up.</summary>
file sealed class UnsupportedAppCatalogGateway : IAppCatalogGateway
{
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public ValueTask<IReadOnlyList<HackAppInfo>> GetInstalledAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide a Catalog gateway.");

    public ValueTask<IReadOnlyList<HackAppInfo>> GetHandlersAsync(
        VirtualPath path, string? mediaType = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide a Catalog gateway.");
}

/// <summary>Default <see cref="IAppExecutionContext.AppManagement"/> for contexts that don't wire one up.</summary>
file sealed class UnsupportedAppManagementGateway : IAppManagementGateway
{
    public ValueTask<IReadOnlyList<HackAppInstallInfo>> GetAllAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide an AppManagement gateway.");

    public ValueTask<IReadOnlyList<string>> UninstallAsync(
        string appId, HackInstallScope scope, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide an AppManagement gateway.");

    public ValueTask<IReadOnlyList<string>> ReinstallAsync(
        string appId, HackInstallScope scope, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide an AppManagement gateway.");
}

/// <summary>Default <see cref="IAppExecutionContext.Environment"/> for contexts with no live process environment.</summary>
file sealed class DetachedAppEnvironmentGateway(IAppExecutionContext context) : IAppEnvironmentGateway
{
    public string CurrentDirectory => context.HomeDirectory;

    public string UserName => context.UserId;

    public string MachineName => "hackeros";

    public IReadOnlyList<string> GetCommandLineArgs() => [context.Manifest.Id];

    public string? GetVariable(string name) => name == "HOME" ? context.HomeDirectory : null;

    public IReadOnlyDictionary<string, string> GetVariables() =>
        new Dictionary<string, string>(StringComparer.Ordinal) { ["HOME"] = context.HomeDirectory };

    public void SetCurrentDirectory(VirtualPath directory) =>
        throw new NotSupportedException("This IAppExecutionContext implementation has no live process environment.");

    public void SetVariable(string name, string? value) =>
        throw new NotSupportedException("This IAppExecutionContext implementation has no live process environment.");
}

/// <summary>Default <see cref="IAppExecutionContext.PermissionErrors"/> for contexts that don't wire one up.</summary>
file sealed class NoOpAppPermissionErrorGateway : IAppPermissionErrorGateway
{
    public event EventHandler<AppPermissionErrorEventArgs>? PermissionDenied
    {
        add { }
        remove { }
    }
}

/// <summary>Default <see cref="IAppExecutionContext.Intents"/> for contexts that don't wire one up.</summary>
file sealed class UnsupportedAppIntentGateway : IAppIntentGateway
{
    public ValueTask<AppIntentLaunchResult> LaunchAsync(
        string appId, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide an Intents gateway.");

    public ValueTask<AppIntentOpenFileResult> OpenFileAsync(
        VirtualPath path, string? mediaType = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide an Intents gateway.");
}

/// <summary>Default <see cref="IAppExecutionContext.Watch"/> for contexts that don't wire one up.</summary>
file sealed class UnsupportedAppFileSystemWatchGateway : IAppFileSystemWatchGateway
{
    public ValueTask<ITopicChannelSubscription<FileSystemChangeEvent>> WatchAsync(
        VirtualPath path, FileSystemWatchScope scope, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide a Watch gateway.");
}

/// <summary>Default <see cref="IAppExecutionContext.Services"/> for contexts that don't wire one up.</summary>
file sealed class UnsupportedAppServiceControlGateway : IAppServiceControlGateway
{
    public ValueTask<ServiceControlResult> StartAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide a Services gateway.");

    public ValueTask<ServiceControlResult> StopAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide a Services gateway.");

    public ValueTask<ServiceStartMode> GetStartModeAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide a Services gateway.");

    public ValueTask<ServiceControlResult> SetStartModeAsync(
        string serviceAppId, ServiceStartMode mode, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide a Services gateway.");
}