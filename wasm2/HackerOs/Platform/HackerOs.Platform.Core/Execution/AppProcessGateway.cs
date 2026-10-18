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

    /// <summary>Initializes a process gateway bound to the process hosting this app instance.</summary>
    public AppProcessGateway(IProcessManager processManager, ICapabilityChecker capabilities, ProcessId ownPid)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        OwnPid = ownPid;
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
