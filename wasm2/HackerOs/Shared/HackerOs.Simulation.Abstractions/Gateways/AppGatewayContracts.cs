using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Notifications;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Simulation.Abstractions.Gateways;

/// <summary>
/// Thrown when a gateway denies an operation because trusted policy did not grant the
/// required capability or authority for the calling app instance.
/// </summary>
public sealed class AppGatewayAccessDeniedException : Exception
{
    /// <summary>Initializes a denial exception carrying the stable policy evaluation.</summary>
    public AppGatewayAccessDeniedException(string capability, CapabilityPolicyEvaluation evaluation)
        : base($"Capability '{capability}' was denied ({evaluation.Reason}).")
    {
        Capability = capability;
        Evaluation = evaluation;
    }

    /// <summary>Gets the exact capability that was evaluated.</summary>
    public string Capability { get; }

    /// <summary>Gets the stable deny-by-default policy evaluation.</summary>
    public CapabilityPolicyEvaluation Evaluation { get; }
}

/// <summary>
/// Evaluates capability policy for one bound app/user/authority, without exposing the
/// underlying grant repository or any other app's grants.
/// </summary>
public interface ICapabilityChecker
{
    /// <summary>Evaluates a deny-by-default decision for one capability and optional resource.</summary>
    /// <param name="capability">Exact capability identifier required by the operation.</param>
    /// <param name="requiredAuthority">Minimum authority policy requires, independent of the grant.</param>
    /// <param name="resourceCandidate">Optional concrete resource checked against structured grant constraints.</param>
    CapabilityPolicyEvaluation Evaluate(
        string capability,
        AppAuthority requiredAuthority = AppAuthority.User,
        CapabilityResourceCandidate? resourceCandidate = null);

    /// <summary>Evaluates a capability and throws <see cref="AppGatewayAccessDeniedException"/> when denied.</summary>
    /// <param name="capability">Exact capability identifier required by the operation.</param>
    /// <param name="requiredAuthority">Minimum authority policy requires, independent of the grant.</param>
    /// <param name="resourceCandidate">Optional concrete resource checked against structured grant constraints.</param>
    void Require(
        string capability,
        AppAuthority requiredAuthority = AppAuthority.User,
        CapabilityResourceCandidate? resourceCandidate = null);
}

/// <summary>
/// Carries one permission-class filesystem denial (missing capability, missing authority, or a
/// denied selected-handle/mode check) raised through <see cref="IAppPermissionErrorGateway"/>.
/// </summary>
public sealed class AppPermissionErrorEventArgs(FileSystemError error) : EventArgs
{
    /// <summary>Gets the permission-class failure that triggered this notification.</summary>
    public FileSystemError Error { get; } = error ?? throw new ArgumentNullException(nameof(error));

    /// <summary>
    /// Gets or sets whether a subscriber already surfaced this error to the user. Left
    /// <see langword="false"/>, the window host shows a default error notification after every
    /// subscriber has run.
    /// </summary>
    public bool ErrorHandled { get; set; }
}

/// <summary>
/// Notifies subscribers when any gateway bound to this app instance denies an operation for a
/// permission-class reason, so the window host can surface a default error notification when no
/// app-level handler marks the error as already handled.
/// </summary>
public interface IAppPermissionErrorGateway
{
    /// <summary>Raised when a permission-class error occurs on a gateway bound to this app instance.</summary>
    event EventHandler<AppPermissionErrorEventArgs>? PermissionDenied;
}

/// <summary>
/// Provides one app instance's authorized filesystem access. Every call is evaluated against
/// trusted OS policy for the bound app/user; the gateway never exposes the raw repository.
/// </summary>
public interface IAppFileSystemGateway
{
    /// <summary>Opens streamed regular-file content.</summary>
    ValueTask<FileSystemResult<FileSystemContentReadHandle>> ReadAsync(
        FileSystemReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Enumerates immediate directory children in ordinal name order.</summary>
    ValueTask<FileSystemResult<FileSystemDirectorySnapshot>> EnumerateAsync(
        FileSystemEnumerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Creates an empty file, directory, or symbolic link atomically.</summary>
    ValueTask<FileSystemMutationResult> CreateAsync(
        FileSystemCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Streams and atomically replaces regular-file content.</summary>
    ValueTask<FileSystemMutationResult> WriteAsync(
        FileSystemWriteRequest request, IFileSystemContentSource content, CancellationToken cancellationToken = default);

    /// <summary>Moves or renames an entry atomically.</summary>
    ValueTask<FileSystemMutationResult> MoveAsync(
        FileSystemMoveRequest request, CancellationToken cancellationToken = default);

    /// <summary>Copies an entry or subtree atomically.</summary>
    ValueTask<FileSystemMutationResult> CopyAsync(
        FileSystemCopyRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes an entry or subtree atomically.</summary>
    ValueTask<FileSystemMutationResult> DeleteAsync(
        FileSystemDeleteRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads one immutable entry snapshot.</summary>
    ValueTask<FileSystemResult<FileSystemEntrySnapshot>> StatAsync(
        FileSystemStatRequest request, CancellationToken cancellationToken = default);

    /// <summary>Atomically replaces owner/group/other permission bits.</summary>
    ValueTask<FileSystemMutationResult> SetPermissionsAsync(
        FileSystemSetPermissionsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a trusted, short-lived selected-resource handle (e.g. from a file-open dialog) to
    /// every operation issued by the returned scoped gateway, in place of a broad capability.
    /// </summary>
    /// <param name="handle">Handle previously issued to this exact app/user.</param>
    IAppFileSystemGateway WithSelectedHandle(FileSystemSelectedResourceHandle handle);
}

/// <summary>Provides one app instance's authorized canonical settings access.</summary>
public interface IAppSettingsGateway
{
    /// <summary>Reads the current revision of one settings document.</summary>
    ValueTask<SettingsReadResult> ReadAsync(VirtualPath path, CancellationToken cancellationToken = default);

    /// <summary>Atomically validates and replaces one settings document.</summary>
    ValueTask<SettingsWriteResult> WriteAsync(SettingsWriteRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides one app instance's typed event bus access. Subscriptions are attributed to the
/// app instance so platform code can trace and, if necessary, force-dispose leaked subscriptions.
/// </summary>
/// <remarks>
/// Per <c>docs/adr/0038-emitter-authorized-topic-messaging.md</c>, this gateway has two lanes.
/// <see cref="Subscribe{TEvent}"/>/<see cref="Publish{TEvent}"/> are the kernel lane: any app may still
/// subscribe to a trusted platform lifecycle event type (read access was never the security problem),
/// but <see cref="Publish{TEvent}"/> no longer forwards to the underlying bus for app-facing callers —
/// no CLR event type is currently allow-listed for app-initiated publish, since the one app that
/// previously used it (<c>SampleTickerService</c>) migrated to the topic lane below. The topic-bus
/// members (<see cref="Subscribe{TPayload}(TopicName, Action{TopicMessage{TPayload}})"/>,
/// <see cref="SubscribeChannel{TPayload}"/>, <see cref="Publish{TPayload}(TopicName, TPayload)"/>,
/// <see cref="RegisterSharedChannel"/>) are the app lane apps are expected to use instead: publishing is
/// authorized by namespace ownership or shared-channel policy rather than being unrestricted.
/// </remarks>
public interface IAppEventGateway
{
    /// <summary>Subscribes to every event of type <typeparamref name="TEvent"/>.</summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;

    /// <summary>
    /// Publishes one event to every current subscriber of <typeparamref name="TEvent"/>, when
    /// <typeparamref name="TEvent"/> is on the app-publishable allow-list. No event type is currently
    /// allow-listed; use <see cref="Publish{TPayload}(TopicName, TPayload)"/> instead. A non-allow-listed
    /// publish is silently denied (returns an empty result, matching the existing fault-isolation
    /// convention) rather than throwing.
    /// </summary>
    IReadOnlyList<EventDispatchFault> Publish<TEvent>(TEvent @event) where TEvent : notnull;

    /// <summary>Subscribes to every message published on <paramref name="topic"/>.</summary>
    IDisposable Subscribe<TPayload>(TopicName topic, Action<TopicMessage<TPayload>> handler) where TPayload : notnull;

    /// <summary>
    /// Returns a disposable, <see cref="System.Threading.Channels.Channel{T}"/>-backed subscription for
    /// <paramref name="topic"/>. Disposing stops delivery and completes the channel.
    /// </summary>
    ITopicChannelSubscription<TPayload> SubscribeChannel<TPayload>(TopicName topic, int? boundedCapacity = null)
        where TPayload : notnull;

    /// <summary>
    /// Publishes one message to <paramref name="topic"/>, stamping this instance's own app/user/process
    /// identity as the publisher — never a caller-supplied identity. Denied when <paramref name="topic"/>
    /// is outside this app's own namespace and is not a shared channel this app may publish to.
    /// </summary>
    TopicPublishResult Publish<TPayload>(TopicName topic, TPayload payload) where TPayload : notnull;

    /// <summary>
    /// Idempotently registers <paramref name="root"/> as a shared channel owned by this app instance.
    /// </summary>
    void RegisterSharedChannel(TopicName root, SharedChannelPolicy policy);
}

/// <summary>
/// Provides one app instance's authorized notification posting, requiring
/// <see cref="AppCapabilities.NotificationsPost"/>.
/// </summary>
public interface IAppNotificationGateway
{
    /// <summary>Posts a notification scoped to the acting user.</summary>
    /// <exception cref="AppGatewayAccessDeniedException">Policy denies <see cref="AppCapabilities.NotificationsPost"/>.</exception>
    NotificationId Post(
        NotificationSeverity severity,
        string title,
        string message,
        IReadOnlyList<NotificationAction>? actions = null,
        TimeSpan? expiresAfter = null);
}

/// <summary>Provides one app instance's structured diagnostic logging, stamped with app identity.</summary>
public interface IAppLoggingGateway
{
    /// <summary>Records one diagnostic entry attributed to the calling app.</summary>
    void Log(DiagnosticSeverity severity, string message, IReadOnlyDictionary<string, string>? properties = null);
}

/// <summary>
/// Provides one app instance's authorized read access to system diagnostic log entries,
/// requiring <see cref="AppCapabilities.DiagnosticsRead"/>.
/// </summary>
public interface IAppDiagnosticsGateway
{
    /// <summary>Gets every retained diagnostic entry, oldest first.</summary>
    /// <exception cref="AppGatewayAccessDeniedException">Policy denies <see cref="AppCapabilities.DiagnosticsRead"/>.</exception>
    IReadOnlyList<DiagnosticEntry> Entries { get; }

    /// <summary>Discards every retained diagnostic entry.</summary>
    /// <exception cref="AppGatewayAccessDeniedException">Policy denies <see cref="AppCapabilities.DiagnosticsClear"/>.</exception>
    void Clear();
}

/// <summary>Provides one app instance's read-only deterministic simulation clock access.</summary>
public interface IAppClockGateway
{
    /// <summary>Gets the current simulated UTC time.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Gets the current monotonic tick number.</summary>
    long CurrentTick { get; }

    /// <summary>Schedules a callback to run once simulated time reaches at least <paramref name="delay"/> from now.</summary>
    IDisposable Schedule(TimeSpan delay, Action callback);

    /// <summary>Returns a task that completes once simulated time reaches at least <paramref name="delay"/> from now.</summary>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides one app instance's authorized process/job access. An app may always observe and
/// stop its own process; observing or managing other processes requires
/// <see cref="AppCapabilities.ProcessList"/>/<see cref="AppCapabilities.ProcessManage"/>.
/// </summary>
public interface IAppProcessGateway
{
    /// <summary>Gets the process record hosting the calling app instance.</summary>
    ProcessRecord OwnProcess { get; }

    /// <summary>Starts a new child process under the calling app's own process.</summary>
    /// <exception cref="AppGatewayAccessDeniedException">Policy denies <see cref="AppCapabilities.ProcessManage"/>.</exception>
    ProcessRecord StartChild(string appId, AppInstanceId appInstanceId, AppKind kind, ResourceProfile resourceProfile);

    /// <summary>Gets every active process; requires <see cref="AppCapabilities.ProcessList"/> to see processes other than the app's own.</summary>
    IReadOnlyList<ProcessRecord> ListProcesses();

    /// <summary>Requests a graceful stop of one process; requires <see cref="AppCapabilities.ProcessManage"/> unless it is the app's own process.</summary>
    /// <exception cref="AppGatewayAccessDeniedException">Policy denies management of another app's process.</exception>
    Task<ProcessRecord> StopAsync(
        ProcessId pid, TimeSpan timeout, ProcessExitReason reason = ProcessExitReason.CloseRequested, CancellationToken cancellationToken = default);

    /// <summary>Immediately force-stops one process; requires <see cref="AppCapabilities.ProcessManage"/> unless it is the app's own process.</summary>
    /// <exception cref="AppGatewayAccessDeniedException">Policy denies management of another app's process.</exception>
    ProcessRecord Kill(ProcessId pid, ProcessExitReason reason = ProcessExitReason.Killed);
}

/// <summary>Identifies the stable outcome of one <see cref="IAppIntentGateway.LaunchAsync"/> request.</summary>
public enum AppIntentLaunchOutcome
{
    /// <summary>The target app was launched, or an existing single-instance was focused.</summary>
    Launched,

    /// <summary>No catalog app matches the requested target ID.</summary>
    NotFound,

    /// <summary>The resolved target app is currently disabled.</summary>
    Disabled,

    /// <summary>The resolved target's entry point faulted while starting.</summary>
    Faulted
}

/// <summary>Contains the result of one <see cref="IAppIntentGateway.LaunchAsync"/> request.</summary>
/// <param name="Outcome">Stable launch outcome.</param>
/// <param name="ErrorCode">Stable machine-readable error code when the outcome did not succeed.</param>
public sealed record AppIntentLaunchResult(AppIntentLaunchOutcome Outcome, string? ErrorCode = null);

/// <summary>Identifies the stable outcome of one <see cref="IAppIntentGateway.OpenFileAsync"/> request.</summary>
public enum AppIntentOpenFileOutcome
{
    /// <summary>A handler was resolved (explicit preference, configured default, or sole candidate) and launched.</summary>
    Opened,

    /// <summary>Multiple enabled apps can handle this file; the caller must show a chooser and retry via <see cref="IAppIntentGateway.LaunchAsync"/>.</summary>
    ChooserRequired,

    /// <summary>No enabled app declares handling this file.</summary>
    NoHandler,

    /// <summary>The resolved target's entry point faulted while starting.</summary>
    Faulted
}

/// <summary>Contains the result of one <see cref="IAppIntentGateway.OpenFileAsync"/> request.</summary>
/// <param name="Outcome">Stable resolution outcome.</param>
/// <param name="CandidateAppIds">Every candidate app ID considered, populated only for <see cref="AppIntentOpenFileOutcome.ChooserRequired"/>.</param>
/// <param name="ErrorCode">Stable machine-readable error code when the outcome did not succeed.</param>
public sealed record AppIntentOpenFileResult(
    AppIntentOpenFileOutcome Outcome,
    IReadOnlyList<string>? CandidateAppIds = null,
    string? ErrorCode = null);

/// <summary>
/// Provides one app instance's authorized ability to launch another installed application or
/// open a file, per <see cref="AppCapabilities.AppsLaunch"/>. This is the single, kernel-owned
/// entry point for "run something" -- launching an app outright, opening a file with its
/// resolved default handler, or opening a file with one specific app -- so that responsibility
/// never needs to be reimplemented by individual apps (e.g. a file manager only ever calls this
/// gateway; it never resolves handlers or starts processes itself).
/// </summary>
public interface IAppIntentGateway
{
    /// <summary>
    /// Requests that another installed app be launched with the given arguments. Passing a file
    /// path as an argument (e.g. from an "Open With" picker) opens that file with exactly the
    /// requested app, bypassing file-association resolution.
    /// </summary>
    /// <exception cref="AppGatewayAccessDeniedException">Policy denies <see cref="AppCapabilities.AppsLaunch"/>.</exception>
    ValueTask<AppIntentLaunchResult> LaunchAsync(
        string appId, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests that a file be opened using the system's file-association resolution (an
    /// administrator-configured default, then any manifest-declared candidate). Never guesses:
    /// when more than one app can handle the file, returns <see cref="AppIntentOpenFileOutcome.ChooserRequired"/>
    /// with every candidate so the caller can show a picker and open with an explicit choice via
    /// <see cref="LaunchAsync"/>.
    /// </summary>
    /// <param name="path">Canonical virtual filesystem path to open.</param>
    /// <param name="mediaType">
    /// Caller-supplied media type when already known (e.g. <c>"inode/directory"</c> for a directory-open
    /// request per ADR 0039); never auto-detected from content.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="AppGatewayAccessDeniedException">Policy denies <see cref="AppCapabilities.AppsLaunch"/>.</exception>
    ValueTask<AppIntentOpenFileResult> OpenFileAsync(
        VirtualPath path, string? mediaType = null, CancellationToken cancellationToken = default);
}

/// <summary>Identifies the stable outcome of one <see cref="IAppServiceControlGateway"/> request.</summary>
public enum ServiceControlOutcome
{
    /// <summary>The request completed: the service was started/stopped, or its start mode was read/set.</summary>
    Succeeded,

    /// <summary>No catalog app matches the requested target ID.</summary>
    NotFound,

    /// <summary>The resolved target app is not a <see cref="AppKind.Service"/> app.</summary>
    NotAService,

    /// <summary>The target service's start mode is <see cref="ServiceStartMode.Disabled"/>; it cannot be started.</summary>
    ServiceDisabled,

    /// <summary>The target's entry point faulted while starting.</summary>
    Faulted
}

/// <summary>Contains the result of one <see cref="IAppServiceControlGateway"/> request.</summary>
/// <param name="Outcome">Stable request outcome.</param>
/// <param name="ErrorCode">Stable machine-readable error code when the outcome did not succeed.</param>
public sealed record ServiceControlResult(ServiceControlOutcome Outcome, string? ErrorCode = null);

/// <summary>
/// Provides one app instance's authorized ability to start, stop, or reconfigure the start mode
/// of a <see cref="AppKind.Service"/> app other than itself. Always permitted between apps whose
/// entry points are compiled into the same assembly (a companion Window/Terminal app controlling
/// its own bundled background service); any other caller needs
/// <see cref="AppCapabilities.ServicesManage"/> (a "service manager" role, e.g. System Monitor).
/// </summary>
public interface IAppServiceControlGateway
{
    /// <summary>
    /// Starts the target service, or focuses it if it is already running. Refused when the
    /// service's effective start mode is <see cref="ServiceStartMode.Disabled"/>.
    /// </summary>
    /// <exception cref="AppGatewayAccessDeniedException">
    /// Policy denies control of this service: the caller is neither in the same assembly nor
    /// holds <see cref="AppCapabilities.ServicesManage"/>.
    /// </exception>
    ValueTask<ServiceControlResult> StartAsync(string serviceAppId, CancellationToken cancellationToken = default);

    /// <summary>Stops the target service if it is currently running.</summary>
    /// <exception cref="AppGatewayAccessDeniedException">
    /// Policy denies control of this service: the caller is neither in the same assembly nor
    /// holds <see cref="AppCapabilities.ServicesManage"/>.
    /// </exception>
    ValueTask<ServiceControlResult> StopAsync(string serviceAppId, CancellationToken cancellationToken = default);

    /// <summary>Reads the target service's effective start mode.</summary>
    /// <exception cref="AppGatewayAccessDeniedException">
    /// Policy denies control of this service: the caller is neither in the same assembly nor
    /// holds <see cref="AppCapabilities.ServicesManage"/>.
    /// </exception>
    ValueTask<ServiceStartMode> GetStartModeAsync(string serviceAppId, CancellationToken cancellationToken = default);

    /// <summary>Sets the target service's effective start mode.</summary>
    /// <exception cref="AppGatewayAccessDeniedException">
    /// Policy denies control of this service: the caller is neither in the same assembly nor
    /// holds <see cref="AppCapabilities.ServicesManage"/>.
    /// </exception>
    ValueTask<ServiceControlResult> SetStartModeAsync(
        string serviceAppId, ServiceStartMode mode, CancellationToken cancellationToken = default);
}
