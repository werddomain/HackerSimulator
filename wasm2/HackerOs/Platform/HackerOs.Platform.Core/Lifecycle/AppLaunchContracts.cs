using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.Policy;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Lifecycle;

/// <summary>Requests that one catalog app be started for one authenticated user, per `P1-APP-004`.</summary>
/// <param name="AppId">Exact target app ID.</param>
/// <param name="Principal">Authenticated principal the new process will run as.</param>
/// <param name="Arguments">
/// Opaque launch arguments. For <see cref="HackerOs.App.Abstractions.AppKind.Terminal"/> apps
/// these become <see cref="TerminalExecutionContext.Arguments"/>.
/// </param>
/// <param name="ParentPid">Optional parent process, when launched as a child of another process.</param>
/// <param name="WorkingDirectory">
/// Initial working directory of the new process (e.g. the invoking shell's current directory,
/// ADR 0050 section 5); when omitted, the parent's directory is inherited, or the principal's home
/// for a process with no parent.
/// </param>
/// <param name="ElevationToken">
/// Optional single-use elevation ticket token (`ADR 0042`), minted by <see cref="IElevationService"/>.
/// Consumed exactly once against <see cref="ElevationScope"/> before this launch proceeds.
/// </param>
/// <param name="ElevationScope">The scope the ticket in <see cref="ElevationToken"/> authorizes.</param>
/// <param name="ApprovalPrompt">
/// Prompt for pending dangerous capabilities (ADR 0049 section 5); the terminal passes its text
/// mode. <see langword="null"/> uses the host's default (the secure-desktop overlay).
/// </param>
/// <param name="Interactive">
/// <see langword="false"/> for a launch nobody is watching (a service auto-started with the
/// session): pending dangerous capabilities are then deferred instead of prompted.
/// </param>
/// <param name="Environment">
/// Environment variables applied over the inherited ones (ADR 0050, section 5): the parent's when
/// <see cref="ParentPid"/> is set, otherwise the login environment.
/// </param>
public sealed record AppLaunchRequest(
    string AppId,
    AuthenticatedPrincipal Principal,
    IReadOnlyList<string> Arguments,
    ProcessId? ParentPid = null,
    VirtualPath? WorkingDirectory = null,
    Guid? ElevationToken = null,
    ElevationScope? ElevationScope = null,
    IDangerousCapabilityApprovalPrompt? ApprovalPrompt = null,
    bool Interactive = true,
    IReadOnlyDictionary<string, string>? Environment = null);

/// <summary>Identifies the stable outcome of one launch attempt.</summary>
public enum AppLaunchStatus
{
    /// <summary>A new process and execution context were created and started.</summary>
    Launched,

    /// <summary>An existing singleton instance was found; no new process was created (`P1-APP-006`).</summary>
    FocusedExisting,

    /// <summary>No catalog app has this ID.</summary>
    NotFound,

    /// <summary>The app is currently disabled.</summary>
    Disabled,

    /// <summary>The entry point threw while starting or executing.</summary>
    EntryPointFault,

    /// <summary>The supplied elevation token was missing, expired, wrong-scope, or already consumed (`ADR 0042`).</summary>
    ElevationDenied
}

/// <summary>Contains the result of one launch attempt, including any fault, per `P1-APP-004`.</summary>
/// <param name="Status">Stable launch outcome.</param>
/// <param name="Process">Process record, when one exists or was created.</param>
/// <param name="Context">Scoped execution context, when one was created.</param>
/// <param name="ExitCode">
/// Exit code returned by a <see cref="HackerOs.App.Abstractions.AppKind.Terminal"/> app that ran
/// to completion as part of this launch.
/// </param>
/// <param name="StandardOutput">Captured standard output for a completed Terminal launch.</param>
/// <param name="StandardError">Captured standard error for a completed Terminal launch.</param>
/// <param name="ErrorCode">Stable machine-readable error code when the launch did not succeed.</param>
/// <param name="ErrorMessage">Human-readable diagnostic, populated for <see cref="AppLaunchStatus.EntryPointFault"/>.</param>
/// <param name="PermissionDenials">
/// Filesystem permission-class errors raised while a completed Terminal launch ran (`ADR 0042`), so
/// the caller can offer an admin-approval retry without the command itself needing to report them.
/// </param>
public sealed record AppLaunchResult(
    AppLaunchStatus Status,
    ProcessRecord? Process = null,
    IAppExecutionContext? Context = null,
    int? ExitCode = null,
    string? StandardOutput = null,
    string? StandardError = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    IReadOnlyList<FileSystemError>? PermissionDenials = null)
{
    /// <summary>Gets whether the app is now running (or already was, for a focused singleton).</summary>
    public bool IsSuccess => Status is AppLaunchStatus.Launched or AppLaunchStatus.FocusedExisting;
}
