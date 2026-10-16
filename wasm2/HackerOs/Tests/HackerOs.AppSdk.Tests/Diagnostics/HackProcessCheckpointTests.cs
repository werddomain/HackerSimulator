using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Diagnostics;
using HackerOs.AppSdk.Tests.IO;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Tests.Support;

namespace HackerOs.AppSdk.Tests.Diagnostics;

/// <summary>ADR 0069, sections 1 and 2: <see cref="HackProcess.Current"/> and <see cref="HackProcess.CheckpointAsync"/>.</summary>
public sealed class HackProcessCheckpointTests
{
    [Fact]
    public async Task A_checkpoint_inside_the_slice_returns_at_once_without_allocating()
    {
        StoppableContext context = new(pid: 31);
        using IDisposable scope = HackExecutionContext.Enter(context);
        // Warm up: JIT, thread statics.
        for (int i = 0; i < 100; i++)
        {
            await HackProcess.CheckpointAsync();
        }

        HackProcessSlice.Begin(31);
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool allCompleted = true;
        for (int i = 0; i < 1000; i++)
        {
            if (i % 50 == 0)
            {
                // Keep every call well inside the 8 ms budget even on a slow, preempted test runner.
                HackProcessSlice.Begin(31);
            }

            allCompleted &= HackProcess.CheckpointAsync().IsCompletedSuccessfully;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allCompleted);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public async Task A_checkpoint_past_the_slice_budget_gives_the_thread_back_then_starts_a_new_slice()
    {
        StoppableContext context = new(pid: 32);
        using IDisposable scope = HackExecutionContext.Enter(context);
        HackProcessSlice.Begin(32);
        Thread.Sleep(HackProcessSlice.Budget + TimeSpan.FromMilliseconds(5));

        ValueTask yielded = HackProcess.CheckpointAsync();

        Assert.False(yielded.IsCompleted);
        await yielded;
        // A new slice began when the thread came back.
        Assert.True(HackProcess.CheckpointAsync().IsCompletedSuccessfully);
    }

    [Fact]
    public void Another_process_reaching_a_checkpoint_starts_its_own_slice()
    {
        HackProcessSlice.Begin(40);
        Thread.Sleep(HackProcessSlice.Budget + TimeSpan.FromMilliseconds(5));

        using IDisposable scope = HackExecutionContext.Enter(new StoppableContext(pid: 41));

        Assert.True(HackProcess.CheckpointAsync().IsCompletedSuccessfully);
    }

    [Fact]
    public async Task A_checkpoint_throws_once_the_process_is_asked_to_stop_even_before_its_token_is_cancelled()
    {
        StoppableContext context = new(pid: 33);
        using IDisposable scope = HackExecutionContext.Enter(context);
        await HackProcess.CheckpointAsync();

        context.RaiseStopRequest(ProcessExitReason.Terminated, cancelToken: false);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await HackProcess.CheckpointAsync());
    }

    [Fact]
    public async Task A_checkpoint_throws_on_the_callers_token_and_on_the_process_token()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await HackProcess.CheckpointAsync(cancelled.Token));

        StoppableContext context = new(pid: 34);
        using IDisposable scope = HackExecutionContext.Enter(context);
        await context.CancelAsync();
        OperationCanceledException stopped = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await HackProcess.CheckpointAsync());
        Assert.Equal(context.CancellationToken, stopped.CancellationToken);
    }

    [Fact]
    public void Current_exposes_the_pid_the_token_and_the_stop_request()
    {
        StoppableContext context = new(pid: 35);
        using IDisposable scope = HackExecutionContext.Enter(context);
        HackCurrentProcess current = HackProcess.Current;
        List<HackStopRequestedEventArgs> heard = [];
        bool tokenCancelledWhenHeard = true;
        EventHandler<HackStopRequestedEventArgs> handler = (_, e) =>
        {
            heard.Add(e);
            tokenCancelledWhenHeard = current.StopToken.IsCancellationRequested;
        };
        current.StopRequested += handler;

        Assert.Equal(35, current.Id);
        Assert.False(current.IsStopRequested);
        Assert.Null(current.StopRequest);
        context.RaiseStopRequest(ProcessExitReason.CloseRequested, cancelToken: true);

        HackStopRequestedEventArgs request = Assert.Single(heard);
        Assert.Equal(ProcessExitReason.CloseRequested, request.Reason);
        Assert.False(tokenCancelledWhenHeard);
        Assert.True(current.IsStopRequested);
        Assert.Same(request, HackProcess.Current.StopRequest);
        HackProcess.Current.StopRequested -= handler;
        Assert.Equal(0, context.HandlerCount);
    }

    /// <summary>
    /// A context that can be asked to stop like the OS does it: the request first, then the token.
    /// </summary>
    private sealed class StoppableContext(long pid) : IAppExecutionContext
    {
        private readonly MinimalAppExecutionContext _inner = HackTestHost.CreateContext(new FakeAppFileSystemGateway());
        private readonly CancellationTokenSource _token = new();
        private EventHandler<HackStopRequestedEventArgs>? _stopRequested;

        public AppManifest Manifest => _inner.Manifest;
        public Guid InstanceId => _inner.InstanceId;
        public string UserId => _inner.UserId;
        public AppAuthority UserAuthority => _inner.UserAuthority;
        public IReadOnlySet<string> GrantedCapabilities => _inner.GrantedCapabilities;
        public SessionId SessionId => _inner.SessionId;
        public ProcessId ProcessId { get; } = ProcessId.FromInt64(pid);
        public CancellationToken CancellationToken => _token.Token;
        public ICapabilityChecker Capabilities => _inner.Capabilities;
        public IAppFileSystemGateway FileSystem => _inner.FileSystem;
        public IAppEventGateway Events => _inner.Events;
        public IAppNotificationGateway Notifications => _inner.Notifications;
        public IAppLoggingGateway Logging => _inner.Logging;
        public IAppClockGateway Clock => _inner.Clock;
        public IAppProcessGateway Processes => _inner.Processes;
        public HackStopRequestedEventArgs? StopRequest { get; private set; }
        public int HandlerCount => _stopRequested?.GetInvocationList().Length ?? 0;

        public event EventHandler<HackStopRequestedEventArgs>? StopRequested
        {
            add => _stopRequested += value;
            remove => _stopRequested -= value;
        }

        public void RaiseStopRequest(ProcessExitReason reason, bool cancelToken)
        {
            DateTimeOffset now = HackTestHost.Now;
            StopRequest = new HackStopRequestedEventArgs(reason, now, now.AddSeconds(5));
            _stopRequested?.Invoke(this, StopRequest);
            if (cancelToken)
            {
                _token.Cancel();
            }
        }

        public Task CancelAsync() => _token.CancelAsync();
    }
}
