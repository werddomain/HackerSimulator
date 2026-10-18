using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Provides one app instance's authorized process/job access. An app may always observe and
/// stop its own process; observing or managing other processes requires
/// <see cref="AppCapabilities.ProcessList"/>/<see cref="AppCapabilities.ProcessManage"/>.
/// </summary>
public sealed class AppProcessGateway : IAppProcessGateway
{
    private readonly IProcessManager _processManager;
    private readonly ICapabilityChecker _capabilities;
    private readonly bool _isRoot;

    /// <summary>Initializes a process gateway bound to the process hosting this app instance, for a process that is not root.</summary>
    public AppProcessGateway(IProcessManager processManager, ICapabilityChecker capabilities, ProcessId ownPid)
        : this(processManager, capabilities, ownPid, isRoot: false)
    {
    }

    /// <summary>Initializes a process gateway bound to the process hosting this app instance.</summary>
    /// <param name="processManager">The process table.</param>
    /// <param name="capabilities">The app's capability checks.</param>
    /// <param name="ownPid">The process hosting the app.</param>
    /// <param name="isRoot">
    /// Whether the process runs as root (<c>sudo</c>, ADR 0042): only root may lower a niceness or
    /// renice another user's process (ADR 0127).
    /// </param>
    public AppProcessGateway(IProcessManager processManager, ICapabilityChecker capabilities, ProcessId ownPid, bool isRoot)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        OwnPid = ownPid;
        _isRoot = isRoot;
    }

    private ProcessId OwnPid { get; }

    /// <inheritdoc />
    public ProcessRecord OwnProcess =>
        _processManager.TryGetActive(OwnPid, out ProcessRecord process)
            ? process
            : throw new InvalidOperationException("The hosting process is no longer active.");

    /// <inheritdoc />
    public ProcessRecord StartChild(string appId, AppInstanceId appInstanceId, AppKind kind, ResourceProfile resourceProfile)
    {
        ProcessRecord own = OwnProcess;
        ProcessStartRequest request = new(OwnPid, appId, appInstanceId, kind, own.UserId, own.SessionId, resourceProfile);
        return _processManager.Start(request);
    }

    /// <inheritdoc />
    public IReadOnlyList<ProcessRecord> ListProcesses()
    {
        IReadOnlyList<ProcessRecord> all = _processManager.GetActiveProcesses();
        if (_capabilities.Evaluate(AppCapabilities.ProcessList).Granted)
        {
            return all;
        }

        // Its own process and its children (ADR 0140: its HackBackground tasks).
        return [.. all.Where(p => p.Pid == OwnPid || IsDescendant(p.Pid))];
    }

    /// <inheritdoc />
    public async Task<ProcessRecord> StopAsync(
        ProcessId pid, TimeSpan timeout, ProcessExitReason reason = ProcessExitReason.CloseRequested, CancellationToken cancellationToken = default)
    {
        RequireManageUnlessOwn(pid);
        return await _processManager.StopAsync(pid, timeout, reason, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ProcessRecord Kill(ProcessId pid, ProcessExitReason reason = ProcessExitReason.Killed)
    {
        RequireManageUnlessOwn(pid);
        return _processManager.Kill(pid, reason);
    }

    /// <inheritdoc />
    public int GetNice(ProcessId pid)
    {
        if (pid != OwnPid)
        {
            _capabilities.Require(AppCapabilities.ProcessList);
        }

        return _processManager.GetNice(pid);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The checks of Linux's <c>setpriority(2)</c>: <c>EPERM</c> for another user's process, and
    /// <c>EACCES</c> for a lower value, unless the caller is root. Both surface as
    /// <see cref="UnauthorizedAccessException"/>.
    /// </remarks>
    public int SetNice(ProcessId pid, int nice)
    {
        RequireManageUnlessOwn(pid);
        if (!_processManager.TryGetActive(pid, out ProcessRecord target))
        {
            throw new KeyNotFoundException($"No active process with PID '{pid}'.");
        }

        int requested = ProcessNice.Clamp(nice);
        if (!_isRoot)
        {
            if (target.UserId != OwnProcess.UserId)
            {
                throw new UnauthorizedAccessException("Operation not permitted");
            }

            if (requested < _processManager.GetNice(pid))
            {
                throw new UnauthorizedAccessException("Permission denied");
            }
        }

        return _processManager.SetNice(pid, requested);
    }

    private void RequireManageUnlessOwn(ProcessId pid)
    {
        if (pid != OwnPid && !IsDescendant(pid))
        {
            _capabilities.Require(AppCapabilities.ProcessManage);
        }
    }

    /// <summary>
    /// ADR 0140: an app may stop or kill its own children (its HackBackground tasks), as a Unix
    /// parent signals the processes it started, without <see cref="AppCapabilities.ProcessManage"/>.
    /// </summary>
    private bool IsDescendant(ProcessId pid)
    {
        // Bounded walk up the parent chain: a process table never holds a cycle, but stay safe.
        for (int depth = 0; depth < 64 && _processManager.TryGetActive(pid, out ProcessRecord process); depth++)
        {
            if (process.ParentPid is not { } parent)
            {
                return false;
            }

            if (parent == OwnPid)
            {
                return true;
            }

            pid = parent;
        }

        return false;
    }
}
