using System.Net;
using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Apps;
using HackerOs.AppSdk.Clipboard;
using HackerOs.AppSdk.IO;
using HackerOs.AppSdk.Net;
using HackerOs.Platform.Core.Processes;
using HackerOs.Platform.WorkerHost.Protocol;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Notifications;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Platform.WorkerHost.Worker;

/// <summary>
/// One process's line to the OS from inside a worker: its calls carry its PID, flush its pending
/// output first (so its writes and its calls stay in order), refuse to run once it was killed
/// (ADR 0069, section 3), and count as kernel time in the worker's CPU meter (ADR 0067, section 2).
/// </summary>
internal sealed class RemoteProcessChannel
{
    private readonly WorkerRpcClient _rpc;
    private readonly RemoteProcessOutput _output;
    private readonly ProcessControlBlock _control;

    public RemoteProcessChannel(long pid, WorkerRpcClient rpc, RemoteProcessOutput output, ProcessControlBlock control)
    {
        Pid = pid;
        _rpc = rpc;
        _output = output;
        _control = control;
    }

    /// <summary>Gets the calling process.</summary>
    public long Pid { get; }

    /// <summary>Calls the OS and waits for its answer.</summary>
    public async Task<JsonElement> CallAsync(string operation, Action<Utf8JsonWriter>? arguments, CancellationToken cancellationToken)
    {
        _control.ThrowIfKilled();
        _output.Flush();
        using (HackExecutionContext.EnterKernel())
        {
            return await _rpc.CallAsync(Pid, operation, arguments, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Posts a call that has no answer, in order with the others.</summary>
    public void Notify(string operation, Action<Utf8JsonWriter>? arguments)
    {
        _control.ThrowIfKilled();
        _output.Flush();
        _rpc.Notify(Pid, operation, arguments);
    }
}

/// <summary>
/// <see cref="IAppEnvironmentGateway"/> in a worker: the process's environment is copied at start, read
/// locally like <c>getenv</c>, and every change is written through to the OS's process table, so
/// <c>/proc/&lt;pid&gt;/environ</c> and <c>cwd</c> follow it (ADR 0050, section 6).
/// </summary>
internal sealed class RemoteEnvironmentGateway(
    RemoteProcessChannel channel,
    IReadOnlyList<string> arguments,
    string currentDirectory,
    Dictionary<string, string> variables,
    string userName,
    string machineName) : IAppEnvironmentGateway
{
    private readonly Lock _gate = new();
    private string _currentDirectory = currentDirectory;

    public string CurrentDirectory
    {
        get
        {
            lock (_gate)
            {
                return _currentDirectory;
            }
        }
    }

    public string UserName => userName;

    public string MachineName => machineName;

    public IReadOnlyList<string> GetCommandLineArgs() => arguments;

    public void SetCurrentDirectory(VirtualPath directory)
    {
        lock (_gate)
        {
            _currentDirectory = directory.Value;
            variables["PWD"] = directory.Value;
        }

        channel.Notify(RemoteOps.EnvSetDirectory, writer => writer.WriteString("p", directory.Value));
    }

    public string? GetVariable(string name)
    {
        lock (_gate)
        {
            return variables.GetValueOrDefault(name);
        }
    }

    public IReadOnlyDictionary<string, string> GetVariables()
    {
        lock (_gate)
        {
            return new Dictionary<string, string>(variables, StringComparer.Ordinal);
        }
    }

    public void SetVariable(string name, string? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (name.Contains('=') || name.Contains('\0') || value?.Contains('\0') == true)
        {
            throw new ArgumentException($"'{name}' is not a valid environment variable assignment.", nameof(name));
        }

        lock (_gate)
        {
            if (value is null)
            {
                variables.Remove(name);
            }
            else
            {
                variables[name] = value;
            }
        }

        channel.Notify(RemoteOps.EnvSetVariable, writer =>
        {
            writer.WriteString("n", name);
            if (value is not null)
            {
                writer.WriteString("v", value);
            }
        });
    }
}

/// <summary><see cref="IAppConsoleGateway"/> in a worker: the process's message streams.</summary>
internal sealed class RemoteConsoleGateway(bool attached, TextReader input, TextWriter output, TextWriter error) : IAppConsoleGateway
{
    public bool IsAttached => attached;

    public TextReader In => input;

    public TextWriter Out => output;

    public TextWriter Error => error;
}

/// <summary><see cref="IAppLoggingGateway"/> in a worker: each entry is posted to <c>/var/log/apps/&lt;appId&gt;.log</c>.</summary>
internal sealed class RemoteLoggingGateway(RemoteProcessChannel channel) : IAppLoggingGateway
{
    public void Log(DiagnosticSeverity severity, string message, IReadOnlyDictionary<string, string>? properties = null) =>
        channel.Notify(RemoteOps.Log, writer =>
        {
            writer.WriteNumber("s", (int)severity);
            writer.WriteString("m", message);
            if (properties is not null)
            {
                WireCodec.WriteDictionary(writer, "pr", properties);
            }
        });
}

/// <summary>
/// <see cref="IAppClockGateway"/> in a worker: the simulated time as of the last synchronization (the
/// start of the process, then each heartbeat answer, every 250 ms); waits are timed by the OS's clock.
/// </summary>
internal sealed class RemoteClockGateway(RemoteProcessChannel channel, WorkerClock clock) : IAppClockGateway
{
    public DateTimeOffset UtcNow => clock.UtcNow;

    public long CurrentTick => clock.CurrentTick;

    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        CancellationTokenSource cancel = new();
        _ = RunAsync();
        return cancel;

        async Task RunAsync()
        {
            try
            {
                await DelayAsync(delay, cancel.Token).ConfigureAwait(false);
                callback();
            }
            catch (OperationCanceledException)
            {
                // Disposed before it fired.
            }
        }
    }

    public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
        await channel.CallAsync(RemoteOps.ClockDelay, writer => writer.WriteNumber("d", delay.Ticks), cancellationToken).ConfigureAwait(false);
}

/// <summary>The simulated clock as the worker last saw it.</summary>
internal sealed class WorkerClock
{
    private readonly Lock _gate = new();
    private DateTimeOffset _utcNow;
    private long _tick = long.MinValue;

    public WorkerClock(DateTimeOffset utcNow, long tick) => Update(utcNow, tick);

    public DateTimeOffset UtcNow
    {
        get
        {
            lock (_gate)
            {
                return _utcNow;
            }
        }
    }

    public long CurrentTick
    {
        get
        {
            lock (_gate)
            {
                return _tick;
            }
        }
    }

    /// <summary>Takes the OS's clock: the start of a process, then every heartbeat answer.</summary>
    public void Update(DateTimeOffset utcNow, long tick)
    {
        lock (_gate)
        {
            if (tick < _tick || (tick == _tick && utcNow < _utcNow))
            {
                return; // An older answer arrived late: time never goes backwards.
            }

            _utcNow = utcNow;
            _tick = tick;
        }
    }
}

/// <summary>
/// <see cref="IAppProcessGateway"/> in a worker. Listing and stopping are calls to the OS; the
/// synchronous members cannot wait for it from a worker's single thread: <see cref="Kill"/> sends the
/// <c>SIGKILL</c> and returns the expected record (use <c>HackProcess.KillAsync</c>, which goes through
/// <see cref="KillAsync"/>, to see a refusal), <see cref="StartChild"/> is not offered.
/// </summary>
internal sealed class RemoteProcessGateway(RemoteProcessChannel channel, ProcessRecord ownProcess) : IAppProcessGateway
{
    public ProcessRecord OwnProcess => ownProcess;

    public ProcessRecord StartChild(string appId, AppInstanceId appInstanceId, AppKind kind, ResourceProfile resourceProfile) =>
        throw RemoteUnsupported.Member("IAppProcessGateway.StartChild");

    public IReadOnlyList<ProcessRecord> ListProcesses() =>
        throw RemoteUnsupported.Member("IAppProcessGateway.ListProcesses (synchronous; read /proc or use HackProcess.GetProcessesAsync)");

    public async Task<ProcessRecord> StopAsync(
        ProcessId pid, TimeSpan timeout, ProcessExitReason reason = ProcessExitReason.CloseRequested, CancellationToken cancellationToken = default)
    {
        JsonElement value = await channel.CallAsync(RemoteOps.ProcessStop, writer =>
        {
            writer.WriteNumber("pid", pid.Value);
            writer.WriteNumber("to", timeout.Ticks);
            writer.WriteNumber("r", (int)reason);
        }, cancellationToken).ConfigureAwait(false);
        return WireCodec.ReadProcess(value.GetProperty("pr"));
    }

    public ProcessRecord Kill(ProcessId pid, ProcessExitReason reason = ProcessExitReason.Killed)
    {
        channel.Notify(RemoteOps.ProcessKill, writer =>
        {
            writer.WriteNumber("pid", pid.Value);
            writer.WriteNumber("r", (int)reason);
        });
        return ownProcess.Pid == pid ? ownProcess : throw RemoteUnsupported.Member("IAppProcessGateway.Kill of another process (synchronous; use HackProcess.KillAsync)");
    }

    public async Task<ProcessRecord> KillAsync(ProcessId pid, ProcessExitReason reason, CancellationToken cancellationToken)
    {
        JsonElement value = await channel.CallAsync(RemoteOps.ProcessKill, writer =>
        {
            writer.WriteNumber("pid", pid.Value);
            writer.WriteNumber("r", (int)reason);
        }, cancellationToken).ConfigureAwait(false);
        return WireCodec.ReadProcess(value.GetProperty("pr"));
    }
}

/// <summary><see cref="IAppNetworkGateway"/> in a worker: the OS's network stack, with its real-network fallback.</summary>
internal sealed class RemoteNetworkGateway(RemoteProcessChannel channel) : IAppNetworkGateway
{
    public async ValueTask<HackNetworkResult<HackHostEntry>> ResolveAsync(string hostName, CancellationToken cancellationToken = default) =>
        WireCodec.ReadNetwork(await channel.CallAsync(RemoteOps.NetResolve, writer => writer.WriteString("h", hostName), cancellationToken).ConfigureAwait(false),
            WireCodec.ReadHostEntry);

    public async ValueTask<HackNetworkResult<HackHostEntry>> ReverseLookupAsync(IPAddress address, CancellationToken cancellationToken = default) =>
        WireCodec.ReadNetwork(await channel.CallAsync(RemoteOps.NetReverse, writer => writer.WriteString("ad", address.ToString()), cancellationToken).ConfigureAwait(false),
            WireCodec.ReadHostEntry);

    public async ValueTask<HackNetworkResult<HackPingReply>> PingAsync(string hostNameOrAddress, TimeSpan timeout, CancellationToken cancellationToken = default) =>
        WireCodec.ReadNetwork(await channel.CallAsync(RemoteOps.NetPing, writer =>
        {
            writer.WriteString("h", hostNameOrAddress);
            writer.WriteNumber("to", timeout.Ticks);
        }, cancellationToken).ConfigureAwait(false), WireCodec.ReadPingReply);

    public async ValueTask<HackNetworkResult<HackHttpResponse>> SendAsync(HackHttpRequest request, CancellationToken cancellationToken = default) =>
        WireCodec.ReadNetwork(await channel.CallAsync(RemoteOps.NetHttp, writer => WireCodec.WriteHttpRequest(writer, request), cancellationToken).ConfigureAwait(false),
            WireCodec.ReadHttpResponse);

    public async ValueTask<HackNetworkResult<IReadOnlyList<HackPortProbeResult>>> ProbeTcpAsync(
        string hostNameOrAddress, int firstPort, int lastPort, CancellationToken cancellationToken = default) =>
        WireCodec.ReadNetwork(await channel.CallAsync(RemoteOps.NetTcp, writer =>
        {
            writer.WriteString("h", hostNameOrAddress);
            writer.WriteNumber("f", firstPort);
            writer.WriteNumber("l", lastPort);
        }, cancellationToken).ConfigureAwait(false), WireCodec.ReadPortProbes);

    public async ValueTask<HackNetworkResult<HackOsFingerprint?>> DetectOperatingSystemAsync(
        string hostNameOrAddress, CancellationToken cancellationToken = default) =>
        WireCodec.ReadNetwork(await channel.CallAsync(RemoteOps.NetOs, writer => writer.WriteString("h", hostNameOrAddress), cancellationToken).ConfigureAwait(false),
            WireCodec.ReadFingerprint);
}

/// <summary><see cref="IAppCatalogGateway"/> in a worker. <see cref="Changed"/> is never raised: a process in a worker reads the catalog again when it needs it.</summary>
internal sealed class RemoteCatalogGateway(RemoteProcessChannel channel) : IAppCatalogGateway
{
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public async ValueTask<IReadOnlyList<HackAppInfo>> GetInstalledAsync(CancellationToken cancellationToken = default) =>
        WireCodec.ReadApps(await channel.CallAsync(RemoteOps.CatalogInstalled, null, cancellationToken).ConfigureAwait(false));

    public async ValueTask<IReadOnlyList<HackAppInfo>> GetHandlersAsync(
        VirtualPath path, string? mediaType = null, CancellationToken cancellationToken = default) =>
        WireCodec.ReadApps(await channel.CallAsync(RemoteOps.CatalogHandlers, writer =>
        {
            writer.WriteString("p", path.Value);
            if (mediaType is not null)
            {
                writer.WriteString("mt", mediaType);
            }
        }, cancellationToken).ConfigureAwait(false));
}

/// <summary><see cref="IAppServiceControlGateway"/> in a worker (<c>systemctl</c>).</summary>
internal sealed class RemoteServiceControlGateway(RemoteProcessChannel channel) : IAppServiceControlGateway
{
    public async ValueTask<ServiceControlResult> StartAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
        WireCodec.ReadServiceResult(await channel.CallAsync(RemoteOps.ServiceStart, writer => writer.WriteString("id", serviceAppId), cancellationToken).ConfigureAwait(false));

    public async ValueTask<ServiceControlResult> StopAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
        WireCodec.ReadServiceResult(await channel.CallAsync(RemoteOps.ServiceStop, writer => writer.WriteString("id", serviceAppId), cancellationToken).ConfigureAwait(false));

    public async ValueTask<ServiceStartMode> GetStartModeAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
        (ServiceStartMode)(await channel.CallAsync(RemoteOps.ServiceGetMode, writer => writer.WriteString("id", serviceAppId), cancellationToken).ConfigureAwait(false))
            .GetInt32("m");

    public async ValueTask<ServiceControlResult> SetStartModeAsync(
        string serviceAppId, ServiceStartMode mode, CancellationToken cancellationToken = default) =>
        WireCodec.ReadServiceResult(await channel.CallAsync(RemoteOps.ServiceSetMode, writer =>
        {
            writer.WriteString("id", serviceAppId);
            writer.WriteNumber("m", (int)mode);
        }, cancellationToken).ConfigureAwait(false));
}

/// <summary><see cref="IAppDriveGateway"/> in a worker (<c>df</c>).</summary>
internal sealed class RemoteDriveGateway(RemoteProcessChannel channel) : IAppDriveGateway
{
    public async ValueTask<IReadOnlyList<HackDriveSnapshot>> GetDrivesAsync(CancellationToken cancellationToken = default) =>
        WireCodec.ReadDrives(await channel.CallAsync(RemoteOps.Drives, null, cancellationToken).ConfigureAwait(false));
}

/// <summary><see cref="IAppIntentGateway"/> in a worker (<c>launch</c>): the OS opens the app on the desktop.</summary>
internal sealed class RemoteIntentGateway(RemoteProcessChannel channel) : IAppIntentGateway
{
    public async ValueTask<AppIntentLaunchResult> LaunchAsync(string appId, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default) =>
        WireCodec.ReadLaunchResult(await channel.CallAsync(RemoteOps.IntentLaunch, writer =>
        {
            writer.WriteString("id", appId);
            WireCodec.WriteStrings(writer, "ar", arguments);
        }, cancellationToken).ConfigureAwait(false));

    public async ValueTask<AppIntentOpenFileResult> OpenFileAsync(VirtualPath path, string? mediaType = null, CancellationToken cancellationToken = default) =>
        WireCodec.ReadOpenFileResult(await channel.CallAsync(RemoteOps.IntentOpenFile, writer =>
        {
            writer.WriteString("p", path.Value);
            if (mediaType is not null)
            {
                writer.WriteString("mt", mediaType);
            }
        }, cancellationToken).ConfigureAwait(false));
}

/// <summary><see cref="IAppClipboardGateway"/> in a worker. <see cref="Changed"/> is never raised.</summary>
internal sealed class RemoteClipboardGateway(RemoteProcessChannel channel) : IAppClipboardGateway
{
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public async ValueTask<string?> GetTextAsync(CancellationToken cancellationToken = default) =>
        (await channel.CallAsync(RemoteOps.ClipboardGetText, null, cancellationToken).ConfigureAwait(false)).GetOptionalString("tx");

    public async ValueTask SetTextAsync(string text, CancellationToken cancellationToken = default) =>
        await channel.CallAsync(RemoteOps.ClipboardSetText, writer => writer.WriteString("tx", text), cancellationToken).ConfigureAwait(false);

    public async ValueTask<HackClipboardFileList?> GetFileDropListAsync(CancellationToken cancellationToken = default) =>
        WireCodec.ReadClipboardFiles(await channel.CallAsync(RemoteOps.ClipboardGetFiles, null, cancellationToken).ConfigureAwait(false));

    public async ValueTask SetFileDropListAsync(HackClipboardFileList files, CancellationToken cancellationToken = default) =>
        await channel.CallAsync(RemoteOps.ClipboardSetFiles, writer => WireCodec.WriteClipboardFiles(writer, files), cancellationToken).ConfigureAwait(false);

    public async ValueTask ClearAsync(CancellationToken cancellationToken = default) =>
        await channel.CallAsync(RemoteOps.ClipboardClear, null, cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// <see cref="IAppNotificationGateway"/> in a worker: the notification is posted to the OS without
/// waiting; the returned identifier is the worker's own (the desktop keeps its own one).
/// </summary>
internal sealed class RemoteNotificationGateway(RemoteProcessChannel channel) : IAppNotificationGateway
{
    public NotificationId Post(
        NotificationSeverity severity,
        string title,
        string message,
        IReadOnlyList<NotificationAction>? actions = null,
        TimeSpan? expiresAfter = null)
    {
        channel.Notify(RemoteOps.NotificationPost, writer =>
        {
            writer.WriteNumber("s", (int)severity);
            writer.WriteString("ti", title);
            writer.WriteString("m", message);
            if (actions is not null)
            {
                WireCodec.WritePairs(writer, "ac", actions.Select(static action => new KeyValuePair<string, string>(action.Id, action.Label)));
            }

            if (expiresAfter is { } expiry)
            {
                writer.WriteNumber("ex", expiry.Ticks);
            }
        });
        return NotificationId.FromGuid(Guid.NewGuid());
    }
}

/// <summary>
/// <see cref="ICapabilityChecker"/> in a worker, from the capabilities granted when the process
/// started. The OS checks every remote call again with its live policy: this only answers the app's
/// own questions.
/// </summary>
internal sealed class RemoteCapabilityChecker(string appId, string userId, IReadOnlySet<string> granted) : ICapabilityChecker
{
    public CapabilityPolicyEvaluation Evaluate(
        string capability,
        AppAuthority requiredAuthority = AppAuthority.User,
        CapabilityResourceCandidate? resourceCandidate = null) =>
        granted.Contains(capability)
            ? CapabilityPolicyEvaluation.Permit(new CapabilityGrant(
                CapabilityGrantId.FromGuid(Guid.NewGuid()), appId, userId, capability, 1, CapabilityGrantSource.SystemPolicy))
            : CapabilityPolicyEvaluation.DenyMissing(1);

    public void Require(
        string capability,
        AppAuthority requiredAuthority = AppAuthority.User,
        CapabilityResourceCandidate? resourceCandidate = null)
    {
        CapabilityPolicyEvaluation evaluation = Evaluate(capability, requiredAuthority, resourceCandidate);
        if (!evaluation.Granted)
        {
            throw new AppGatewayAccessDeniedException(capability, evaluation);
        }
    }
}

/// <summary><see cref="IAppPermissionErrorGateway"/> in a worker: raised when the OS reports a denial on this process's calls.</summary>
internal sealed class RemotePermissionErrorGateway : IAppPermissionErrorGateway
{
    public event EventHandler<AppPermissionErrorEventArgs>? PermissionDenied;

    public void Raise(FileSystemError error) => PermissionDenied?.Invoke(this, new AppPermissionErrorEventArgs(error));
}

/// <summary>
/// <see cref="IAppEventGateway"/> in a worker: not offered. Typed events are .NET objects of the OS's
/// own heap; a process that needs them stays on the main thread.
/// </summary>
internal sealed class RemoteEventGateway : IAppEventGateway
{
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull =>
        throw RemoteUnsupported.Member("IAppEventGateway.Subscribe");

    public IReadOnlyList<EventDispatchFault> Publish<TEvent>(TEvent @event) where TEvent : notnull =>
        throw RemoteUnsupported.Member("IAppEventGateway.Publish");

    public IDisposable Subscribe<TPayload>(TopicName topic, Action<TopicMessage<TPayload>> handler) where TPayload : notnull =>
        throw RemoteUnsupported.Member("IAppEventGateway.Subscribe");

    public ITopicChannelSubscription<TPayload> SubscribeChannel<TPayload>(TopicName topic, int? boundedCapacity = null) where TPayload : notnull =>
        throw RemoteUnsupported.Member("IAppEventGateway.SubscribeChannel");

    public TopicPublishResult Publish<TPayload>(TopicName topic, TPayload payload) where TPayload : notnull =>
        throw RemoteUnsupported.Member("IAppEventGateway.Publish");

    public void RegisterSharedChannel(TopicName root, SharedChannelPolicy policy) =>
        throw RemoteUnsupported.Member("IAppEventGateway.RegisterSharedChannel");
}

/// <summary><see cref="IAppFileSystemWatchGateway"/> in a worker: not offered (the change feed is an event of the OS's heap).</summary>
internal sealed class RemoteWatchGateway : IAppFileSystemWatchGateway
{
    public ValueTask<ITopicChannelSubscription<FileSystemChangeEvent>> WatchAsync(
        VirtualPath path, FileSystemWatchScope scope, CancellationToken cancellationToken = default) =>
        throw RemoteUnsupported.Member("IAppFileSystemWatchGateway.WatchAsync");
}
