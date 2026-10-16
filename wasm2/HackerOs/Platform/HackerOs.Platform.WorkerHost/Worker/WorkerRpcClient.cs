using System.Text.Json;
using HackerOs.Platform.WorkerHost.Protocol;

namespace HackerOs.Platform.WorkerHost.Worker;

/// <summary>
/// The worker's end of the remote gateway calls (ADR 0070, section 4): numbers each call, posts it
/// to the OS and completes it when the matching <c>result</c> arrives. Like a system call, each one
/// is a request to the kernel that runs on its own thread (the OS's), while this thread is free to
/// run other processes.
/// </summary>
internal sealed class WorkerRpcClient
{
    private readonly Action<string> _post;
    private readonly Lock _gate = new();
    private readonly Dictionary<int, PendingCall> _pending = [];
    private int _nextId;

    /// <summary>Initializes the client over the worker's outgoing pipe.</summary>
    /// <param name="post">Posts one message to the OS.</param>
    public WorkerRpcClient(Action<string> post) => _post = post ?? throw new ArgumentNullException(nameof(post));

    /// <summary>Gets the number of calls waiting for their answer.</summary>
    public int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _pending.Count;
            }
        }
    }

    /// <summary>
    /// Calls <paramref name="operation"/> on behalf of <paramref name="pid"/> and waits for its value.
    /// </summary>
    /// <param name="pid">The calling process.</param>
    /// <param name="operation">One of <see cref="RemoteOps"/>.</param>
    /// <param name="arguments">Writes the <c>a</c> object, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Abandons the call; the OS is told to cancel its side.</param>
    /// <returns>The <c>v</c> object of the result (an empty object when the operation returns nothing).</returns>
    public Task<JsonElement> CallAsync(long pid, string operation, Action<Utf8JsonWriter>? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PendingCall call = new(pid);
        int id;
        lock (_gate)
        {
            id = ++_nextId;
            _pending[id] = call;
        }

        if (cancellationToken.CanBeCanceled)
        {
            call.Registration = cancellationToken.Register(() => Abandon(id, cancellationToken));
        }

        _post(WorkerMessage.Create(WorkerMessageType.Call, writer =>
        {
            writer.WriteNumber("i", id);
            writer.WriteNumber("p", pid);
            writer.WriteString("o", operation);
            WriteArguments(writer, arguments);
        }));
        return call.Completion.Task;
    }

    /// <summary>Posts a call that has no answer (log line, environment change), in order with the others.</summary>
    /// <param name="pid">The calling process.</param>
    /// <param name="operation">One of <see cref="RemoteOps"/>.</param>
    /// <param name="arguments">Writes the <c>a</c> object.</param>
    public void Notify(long pid, string operation, Action<Utf8JsonWriter>? arguments) =>
        _post(WorkerMessage.Create(WorkerMessageType.Notify, writer =>
        {
            writer.WriteNumber("p", pid);
            writer.WriteString("o", operation);
            WriteArguments(writer, arguments);
        }));

    /// <summary>Completes the call a <c>result</c> message answers.</summary>
    /// <param name="message">The root of the <c>result</c> message.</param>
    public void Complete(JsonElement message)
    {
        int id = message.GetInt32("i");
        PendingCall? call;
        lock (_gate)
        {
            if (!_pending.Remove(id, out call))
            {
                return; // Abandoned (cancelled) or failed with its process: the late answer is dropped.
            }
        }

        call.Registration.Dispose();
        if (message.GetOptional("e") is { } error)
        {
            call.Completion.TrySetException(RemoteErrors.Read(error));
        }
        else
        {
            call.Completion.TrySetResult(message.GetOptional("v")?.Clone() ?? EmptyObject);
        }
    }

    /// <summary>Fails every pending call of one process (it was killed): they never get their answer.</summary>
    /// <param name="pid">The process.</param>
    /// <param name="exception">The exception the callers see.</param>
    public void FailProcess(long pid, Exception exception)
    {
        List<PendingCall> failed = [];
        lock (_gate)
        {
            foreach ((int id, PendingCall call) in _pending.Where(entry => entry.Value.Pid == pid).ToList())
            {
                _pending.Remove(id);
                failed.Add(call);
            }
        }

        foreach (PendingCall call in failed)
        {
            call.Registration.Dispose();
            call.Completion.TrySetException(exception);
        }
    }

    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    private static void WriteArguments(Utf8JsonWriter writer, Action<Utf8JsonWriter>? arguments)
    {
        writer.WriteStartObject("a");
        arguments?.Invoke(writer);
        writer.WriteEndObject();
    }

    private void Abandon(int id, CancellationToken cancellationToken)
    {
        PendingCall? call;
        lock (_gate)
        {
            if (!_pending.Remove(id, out call))
            {
                return;
            }
        }

        _post(WorkerMessage.Create(WorkerMessageType.Cancel, writer => writer.WriteNumber("i", id)));
        call.Completion.TrySetCanceled(cancellationToken);
    }

    private sealed class PendingCall(long pid)
    {
        public long Pid { get; } = pid;

        public TaskCompletionSource<JsonElement> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationTokenRegistration Registration { get; set; }
    }
}
