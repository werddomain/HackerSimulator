using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Diagnostics;
using HackerOs.Platform.Core.Execution;
using HackerOs.Platform.Core.Processes;
using HackerOs.Platform.WorkerHost.Protocol;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.WorkerHost.Worker;

/// <summary>
/// The OS inside one Web Worker (ADR 0070, section 4): it runs the processes the OS places here, each
/// with a remote <see cref="IAppExecutionContext"/>, on this worker's single thread; it delivers their
/// stop requests and kills (ADR 0069), and sends a heartbeat every 250 ms with their CPU time and the
/// runtime's memory (section 5). A heartbeat that stops is how the OS sees this thread is stuck.
/// </summary>
/// <remarks>
/// Messages arrive through <see cref="Receive"/> and leave through the <c>post</c> delegate. The host
/// never blocks: every wait is asynchronous, so one process awaiting the OS lets the others run.
/// </remarks>
public sealed class WorkerProcessHost
{
    private readonly Action<string> _post;
    private readonly IWorkerAssemblyLoader _loader;
    private readonly bool _canMeterCpu;
    private readonly WorkerRpcClient _rpc;
    private readonly Lock _gate = new();
    private readonly Dictionary<long, WorkerProcess> _processes = [];
    private WorkerClock? _clock;
    private WorkerCpuMeter? _meter;
    private TimeSpan _heartbeatPeriod = TimeSpan.FromMilliseconds(250);
    private int _workerId;
    private long _heartbeatSequence;
    private bool _initialized;
    private volatile bool _stopped;

    /// <summary>Initializes the worker's host.</summary>
    /// <param name="post">Posts one message to the OS (<c>self.postMessage</c> in a real worker).</param>
    /// <param name="loader">Loads the apps' assemblies.</param>
    /// <param name="canMeterCpu">
    /// Whether this runtime belongs to the worker alone, so the context-switch hook may be installed
    /// (<see langword="false"/> for the in-process loopback, which shares the OS's statics).
    /// </param>
    public WorkerProcessHost(Action<string> post, IWorkerAssemblyLoader loader, bool canMeterCpu)
    {
        _post = post ?? throw new ArgumentNullException(nameof(post));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _canMeterCpu = canMeterCpu;
        _rpc = new WorkerRpcClient(post);
    }

    /// <summary>Gets the number of processes running here.</summary>
    public int ProcessCount
    {
        get
        {
            lock (_gate)
            {
                return _processes.Count;
            }
        }
    }

    /// <summary>Handles one message from the OS.</summary>
    /// <param name="message">The JSON text.</param>
    public void Receive(string message)
    {
        if (_stopped)
        {
            return;
        }

        try
        {
            using JsonDocument document = WorkerMessage.Parse(message, out string type);
            JsonElement root = document.RootElement;
            switch (type)
            {
                case WorkerMessageType.Init:
                    Initialize(root);
                    break;
                case WorkerMessageType.Start:
                    Start(root);
                    break;
                case WorkerMessageType.Result:
                    _rpc.Complete(root);
                    break;
                case WorkerMessageType.Stop:
                    Find(root)?.DeliverStop(
                        (ProcessExitReason)root.GetInt32("r"), WireCodec.ReadTime(root, "ra"), WireCodec.ReadTime(root, "dl"));
                    break;
                case WorkerMessageType.Interrupt:
                    Find(root)?.Interrupt();
                    break;
                case WorkerMessageType.Kill:
                    Kill(root.GetInt64("p"));
                    break;
                case WorkerMessageType.ServiceStop:
                    Find(root)?.StopService((ServiceStopReason)root.GetInt32("r"));
                    break;
                case WorkerMessageType.Clock:
                    _clock?.Update(WireCodec.ReadTime(root, "u"), root.GetInt64("k"));
                    break;
                case WorkerMessageType.PermissionDenied:
                    Find(root)?.Context.RaisePermissionDenied(WireCodec.ReadError(root.GetProperty("err")));
                    break;
                case WorkerMessageType.OutputAck:
                    Find(root)?.Output.Acknowledge(root.GetInt64("n"));
                    break;
                case WorkerMessageType.PipeClosed:
                    Find(root)?.Output.Close(root.GetInt32("f"));
                    break;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Log($"worker {_workerId}: bad message: {exception.GetType().Name}: {exception.Message}");
        }
    }

    /// <summary>Stops the heartbeat (the loopback's terminate; a real worker simply dies).</summary>
    public void Shutdown() => _stopped = true;

    private void Initialize(JsonElement root)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _workerId = root.GetInt32("w");
        _heartbeatPeriod = TimeSpan.FromMilliseconds(Math.Max(10, root.GetInt32("hb")));
        if (_canMeterCpu && root.GetFlag("cpu") && !HackExecutionContext.IsContextSwitchHookInstalled)
        {
            _meter = new WorkerCpuMeter();
            HackExecutionContext.InstallContextSwitchHook(_meter);
        }

        _ = WarmUpAsync(WireCodec.ReadStrings(root, "pre"));
    }

    /// <summary>Loads the preloaded assemblies, says hello, then starts the heartbeat.</summary>
    private async Task WarmUpAsync(IReadOnlyList<string> preload)
    {
        try
        {
            await _loader.PreloadAsync(preload).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A missing assembly only delays the first process that needs it.
            Log($"worker {_workerId}: preload failed: {exception.Message}");
        }

        _post(WorkerMessage.Create(WorkerMessageType.Hello, writer => writer.WriteNumber("w", _workerId)));
        await HeartbeatAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Every period, while this thread is free: the times of each process and the runtime's memory.
    /// A process stuck in a loop keeps this thread, so the heartbeat stops with it.
    /// </summary>
    private async Task HeartbeatAsync()
    {
        while (!_stopped)
        {
            await Task.Delay(_heartbeatPeriod).ConfigureAwait(true);
            if (_stopped)
            {
                return;
            }

            SendHeartbeat();
        }
    }

    private void SendHeartbeat()
    {
        List<WorkerProcess> processes;
        lock (_gate)
        {
            processes = [.. _processes.Values];
        }

        GCMemoryInfo memory = GC.GetGCMemoryInfo();
        _post(WorkerMessage.Create(WorkerMessageType.Heartbeat, writer =>
        {
            writer.WriteNumber("w", _workerId);
            writer.WriteNumber("sq", ++_heartbeatSequence);
            writer.WriteNumber("heap", GC.GetTotalMemory(forceFullCollection: false));
            writer.WriteNumber("comm", memory.TotalCommittedBytes);
            if (_meter is { } meter)
            {
                writer.WriteNumber("busy", meter.BusyTime.Ticks);
            }

            writer.WriteStartArray("ps");
            foreach (WorkerProcess process in processes)
            {
                writer.WriteStartObject();
                writer.WriteNumber("p", process.Pid);
                if (_meter?.Get(process.Pid) is { } times)
                {
                    writer.WriteNumber("ut", times.UserTime.Ticks);
                    writer.WriteNumber("st", times.SystemTime.Ticks);
                    writer.WriteNumber("lt", times.LongTasks);
                    writer.WriteNumber("ls", times.LongestSlice.Ticks);
                    writer.WriteNumber("cs", times.Slices);
                    writer.WriteNumber("al", times.AllocatedBytes);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }));
    }

    private void Start(JsonElement root)
    {
        long pid = root.GetInt64("p");
        JsonElement identity = root.GetProperty("id");
        JsonElement environment = root.GetProperty("env");
        _clock ??= new WorkerClock(WireCodec.ReadTime(root, "cu"), root.GetInt64("ck"));
        _clock.Update(WireCodec.ReadTime(root, "cu"), root.GetInt64("ck"));
        AppManifest manifest = AppManifestJsonSerializer.NormalizeOptionalCollections(
            AppManifestJsonSerializer.DeserializeStrict(root.GetString("mf")));
        RemoteProcessSnapshot snapshot = new(
            manifest,
            Guid.ParseExact(identity.GetString("in"), "N"),
            identity.GetString("u"),
            identity.GetString("home"),
            identity.GetFlag("root"),
            identity.GetString("login"),
            identity.GetString("disp"),
            (AppAuthority)identity.GetInt32("auth"),
            new HashSet<string>(WireCodec.ReadStrings(identity, "caps"), StringComparer.Ordinal),
            SessionId.FromGuid(Guid.ParseExact(identity.GetString("s"), "N")),
            WireCodec.ReadProcess(identity.GetProperty("pr")),
            WireCodec.ReadStrings(environment, "argv"),
            environment.GetString("cwd"),
            WireCodec.ReadDictionary(environment, "vars"),
            environment.GetString("user"),
            environment.GetString("host"));

        WorkerProcess process = new(this, pid, snapshot, root.GetFlag("in"), _clock);
        lock (_gate)
        {
            _processes[pid] = process;
        }

        StartInfo start = new(
            root.GetInt32("k") == 1 ? AppKind.Service : AppKind.Terminal,
            root.GetString("asm"),
            root.GetString("ty"),
            WireCodec.ReadStrings(root, "argv"),
            root.GetString("cwd"),
            WireCodec.ReadDictionary(root, "envs"));
        _ = RunAsync(process, start);
    }

    private async Task RunAsync(WorkerProcess process, StartInfo start)
    {
        RemoteProcessExit exit;
        try
        {
            Type entryType = await _loader.LoadEntryTypeAsync(start.Assembly, start.TypeName).ConfigureAwait(true);
            object instance = CreateInstance(entryType, process.Context.Manifest);
            exit = start.Kind == AppKind.Service
                ? await process.RunServiceAsync((ServiceAppBase)instance).ConfigureAwait(true)
                : await process.RunTerminalAsync((TerminalAppBase)instance, start).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            exit = new RemoteProcessExit(RemoteProcessExitStatus.Faulted, FaultMessage: $"{exception.GetType().Name}: {exception.Message}");
        }

        End(process, exit);
    }

    /// <summary>
    /// Creates the app's entry point with its manifest, as the OS does in-process (ADR 0055: an app's
    /// constructor takes its manifest only).
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming", "IL2067",
        Justification = "The entry-point type was resolved and validated by the OS's discovery against the host's " +
            "explicit app allowlist; hosts publish untrimmed (P1-GATE-004).")]
    private static object CreateInstance(Type entryType, AppManifest manifest) =>
        Activator.CreateInstance(entryType, manifest)
        ?? throw new InvalidOperationException($"Could not create '{entryType.FullName}'.");

    private void End(WorkerProcess process, RemoteProcessExit exit)
    {
        process.Output.Flush();
        process.Output.Abort();
        lock (_gate)
        {
            _processes.Remove(process.Pid);
        }

        _meter?.Forget(process.Pid);
        _post(WorkerMessage.Create(WorkerMessageType.Exit, writer =>
        {
            writer.WriteNumber("p", process.Pid);
            writer.WriteNumber("s", (int)exit.Status);
            writer.WriteNumber("c", exit.ExitCode);
            if (exit.FaultMessage is { } message)
            {
                writer.WriteString("m", message);
            }
        }));
    }

    /// <summary>
    /// <c>SIGKILL</c> while this thread still answers (escalation step 3): the process's pending
    /// continuations are dropped and its pending calls fail; the worker and its other processes live on.
    /// </summary>
    private void Kill(long pid)
    {
        WorkerProcess? process;
        lock (_gate)
        {
            _processes.TryGetValue(pid, out process);
        }

        process?.Kill();
        _rpc.FailProcess(pid, new HackProcessKilledException(pid));
        _post(WorkerMessage.Create(WorkerMessageType.KillAck, writer => writer.WriteNumber("p", pid)));
    }

    private WorkerProcess? Find(JsonElement root)
    {
        long pid = root.GetInt64("p");
        lock (_gate)
        {
            return _processes.GetValueOrDefault(pid);
        }
    }

    private void Log(string line)
    {
        try
        {
            _post(WorkerMessage.Create(WorkerMessageType.Log, writer => writer.WriteString("m", line)));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Nothing else to tell.
        }
    }

    internal WorkerRpcClient Rpc => _rpc;

    internal void Post(string message) => _post(message);

    /// <summary>What a <c>start</c> message asks to run.</summary>
    internal sealed record StartInfo(
        AppKind Kind,
        string Assembly,
        string TypeName,
        IReadOnlyList<string> Arguments,
        string WorkingDirectory,
        IReadOnlyDictionary<string, string> Environment);
}

/// <summary>One process running in a worker: its remote context, streams, control block and token.</summary>
internal sealed class WorkerProcess
{
    private readonly WorkerProcessHost _host;
    private readonly CancellationTokenSource _token = new();
    private ServiceAppBase? _service;

    public WorkerProcess(WorkerProcessHost host, long pid, RemoteProcessSnapshot snapshot, bool inputAttached, WorkerClock clock)
    {
        _host = host;
        Pid = pid;
        Control = new ProcessControlBlock(ProcessId.FromInt64(pid));
        Output = new RemoteProcessOutput(pid, host.Post);
        RemoteProcessChannel channel = new(pid, host.Rpc, Output, Control);
        Input = inputAttached
            ? new RemoteStandardInput(async (count, cancellationToken) => StandardInputWire.ReadChunk(
                await channel.CallAsync(RemoteOps.StdinRead, writer => writer.WriteNumber("n", count), cancellationToken).ConfigureAwait(false)))
            : new StringReader(string.Empty);
        Context = new RemoteAppExecutionContext(
            snapshot, channel, clock,
            new RemoteConsoleGateway(true, Input, Output.StandardOutput, Output.StandardError),
            _token.Token);
    }

    public long Pid { get; }

    public ProcessControlBlock Control { get; }

    public RemoteProcessOutput Output { get; }

    public TextReader Input { get; }

    public RemoteAppExecutionContext Context { get; }

    /// <summary>Runs a command, mapping its ending like the OS does in-process.</summary>
    public async Task<RemoteProcessExit> RunTerminalAsync(TerminalAppBase app, WorkerProcessHost.StartInfo start)
    {
        TerminalExecutionContext terminal = new(
            Context, start.Arguments, Input, Output.StandardOutput, Output.StandardError,
            start.WorkingDirectory, start.Environment);
        try
        {
            using IDisposable ambient = HackExecutionContext.Enter(Context);
            int code = await Control.RunAsync(() => app.ExecuteAsync(terminal, _token.Token).AsTask()).ConfigureAwait(true);
            return RemoteProcessExit.Exited(code);
        }
        catch (HackProcessKilledException)
        {
            return new RemoteProcessExit(RemoteProcessExitStatus.Killed);
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested || Context.StopRequest is not null)
        {
            return new RemoteProcessExit(RemoteProcessExitStatus.Cancelled);
        }
        catch (RemoteBrokenPipeException)
        {
            return new RemoteProcessExit(RemoteProcessExitStatus.BrokenPipe);
        }
    }

    /// <summary>Runs a service until it returns or honors its token.</summary>
    public async Task<RemoteProcessExit> RunServiceAsync(ServiceAppBase service)
    {
        _service = service;
        try
        {
            await Control.RunAsync(async () =>
            {
                await service.RunAsync(Context, Context.CancellationToken).ConfigureAwait(true);
                return 0;
            }).ConfigureAwait(true);
            return RemoteProcessExit.Exited(0);
        }
        catch (HackProcessKilledException)
        {
            return new RemoteProcessExit(RemoteProcessExitStatus.Killed);
        }
        catch (OperationCanceledException)
        {
            return new RemoteProcessExit(RemoteProcessExitStatus.Cancelled);
        }
    }

    /// <summary>
    /// <c>SIGTERM</c> (ADR 0069, section 1): raise <c>StopRequested</c> once, tell the OS when the work
    /// the app deferred is done, then cancel the process token.
    /// </summary>
    public void DeliverStop(ProcessExitReason reason, DateTimeOffset requestedAt, DateTimeOffset deadline)
    {
        if (Context.DeliverStopRequest(reason, requestedAt, deadline) is not { } request)
        {
            return;
        }

        _ = ReportStopWorkAsync(request);
        Interrupt();
    }

    /// <summary>Cancels the process token (Ctrl+C, or after the stop request).</summary>
    public void Interrupt()
    {
        try
        {
            _token.Cancel();
        }
        catch (AggregateException)
        {
            // A callback of the app threw; the token is cancelled anyway.
        }
    }

    /// <summary>Runs the service's <c>OnStoppingAsync</c> (logout, shutdown, disable) and reports it.</summary>
    public void StopService(ServiceStopReason reason) => _ = StopServiceAsync(reason);

    public void Kill()
    {
        Control.MarkKilled();
        Output.Abort();
        Interrupt();
    }

    private async Task ReportStopWorkAsync(HackStopRequestedEventArgs request)
    {
        try
        {
            await request.WhenDeferredWorkEndedAsync().ConfigureAwait(true);
        }
        finally
        {
            _host.Post(WorkerMessage.Create(WorkerMessageType.StopDone, writer => writer.WriteNumber("p", Pid)));
        }
    }

    private async Task StopServiceAsync(ServiceStopReason reason)
    {
        try
        {
            if (_service is { } service)
            {
                using CancellationTokenSource budget = new(TimeSpan.FromSeconds(5));
                await service.StopAsync(reason, budget.Token).ConfigureAwait(true);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Best effort, like a local service: the OS ends the process anyway.
        }
        finally
        {
            _host.Post(WorkerMessage.Create(WorkerMessageType.ServiceStopped, writer => writer.WriteNumber("p", Pid)));
        }
    }
}
