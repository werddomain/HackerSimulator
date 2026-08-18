using HackerOs.Platform.Blazor.Windows;
using HackerOs.Simulation.Abstractions.Time;
using HackerOs.Taskbar.Blazor;
using HackerOs.Windowing.Core;
using HackerOs.Windowing.Abstractions;

namespace HackerOs.Platform.Blazor.Shell;

/// <summary>Projects the authoritative window runtime into the taskbar's generic window contract.</summary>
public sealed class TaskbarWindowSourceAdapter : ITaskbarWindowSource, IDisposable
{
    private readonly WindowRuntime _runtime;

    /// <summary>Creates an adapter over the desktop window runtime.</summary>
    public TaskbarWindowSourceAdapter(WindowRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _runtime.StateChanged += OnRuntimeStateChanged;
    }

    /// <inheritdoc />
    public IReadOnlyList<TaskbarWindowItem> Windows => [.. _runtime.Windows.Select(ToItem)];

    /// <inheritdoc />
    public event Action? Changed;

    private static TaskbarWindowItem ToItem(WindowRuntimeState window) => new(
        window.Id,
        window.Title,
        window.IconAssetPath,
        window.IsFocused,
        window.VisualState == WindowVisualState.Minimized);

    private void OnRuntimeStateChanged() => Changed?.Invoke();

    /// <inheritdoc />
    public void Dispose() => _runtime.StateChanged -= OnRuntimeStateChanged;
}

/// <summary>Dispatches taskbar-initiated window commands onto the authoritative window runtime.</summary>
public sealed class TaskbarCommandDispatcherAdapter(
    WindowRuntime runtime,
    WindowCloseCoordinator closeCoordinator,
    WindowCloseGuardRegistry closeGuards) : ITaskbarCommandDispatcher
{
    /// <inheritdoc />
    public void Activate(WindowId id) => runtime.Apply(new FocusWindowCommand(id));

    /// <inheritdoc />
    public void Minimize(WindowId id) => runtime.Apply(new MinimizeWindowCommand(id));

    /// <inheritdoc />
    public void Restore(WindowId id) => runtime.Apply(new RestoreWindowCommand(id));

    /// <inheritdoc />
    public void Close(WindowId id) => _ = CloseAsync(id);

    private async Task CloseAsync(WindowId id)
    {
        WindowRuntimeState? window = runtime.Windows.FirstOrDefault(w => w.Id == id);
        if (window?.OnRequestClose is { } onRequestClose)
        {
            await onRequestClose();
            return;
        }

        await closeCoordinator.CloseAsync(
            id,
            cancellationToken => closeGuards.ConfirmCloseAsync(id, cancellationToken));
    }
}

/// <summary>
/// Owns the desktop shell's application launcher open/closed state so both the shell's own
/// launcher panel and the taskbar's launcher trigger stay in sync regardless of which one
/// changed it.
/// </summary>
public sealed class TaskbarLauncherAdapter : ITaskbarLauncher
{
    /// <inheritdoc />
    public bool IsOpen { get; private set; }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public void Toggle() => SetOpen(!IsOpen);

    /// <summary>Opens the launcher.</summary>
    public void Open() => SetOpen(true);

    /// <summary>Closes the launcher.</summary>
    public void Close() => SetOpen(false);

    private void SetOpen(bool value)
    {
        if (IsOpen == value)
        {
            return;
        }

        IsOpen = value;
        Changed?.Invoke();
    }
}

/// <summary>
/// Owns the clock panel's open/closed state. Unlike the notification bell it replaces, this state
/// now actually gates the panel's render in <c>DesktopShell</c> — the panel's own content (unread
/// count included) is computed inside <c>ClockPanel</c>, not on this trigger contract, since the
/// taskbar package must never need to know what's inside <c>ClockPanelContent</c>.
/// </summary>
public sealed class TaskbarClockPanelSourceAdapter : ITaskbarClockPanelSource
{
    /// <inheritdoc />
    public bool IsOpen { get; private set; }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public void Toggle() => SetOpen(!IsOpen);

    /// <summary>Closes the clock panel.</summary>
    public void Close() => SetOpen(false);

    private void SetOpen(bool value)
    {
        if (IsOpen == value)
        {
            return;
        }

        IsOpen = value;
        Changed?.Invoke();
    }
}

/// <summary>Ticks the taskbar clock once per simulated second from the session's clock.</summary>
public sealed class TaskbarStatusSourceAdapter : ITaskbarStatusSource, IDisposable
{
    private readonly ISimulationClock _clock;
    private IDisposable? _subscription;

    /// <summary>Creates a status source over the session's simulation clock.</summary>
    public TaskbarStatusSourceAdapter(ISimulationClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        ScheduleNextTick();
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow => _clock.UtcNow;

    /// <inheritdoc />
    public event Action? Changed;

    private void ScheduleNextTick()
    {
        _subscription?.Dispose();
        _subscription = _clock.Schedule(TimeSpan.FromSeconds(1), () =>
        {
            Changed?.Invoke();
            ScheduleNextTick();
        });
    }

    /// <inheritdoc />
    public void Dispose() => _subscription?.Dispose();
}

/// <summary>Routes the taskbar's logout command into the desktop shell's own logout flow.</summary>
public sealed class TaskbarSessionCommandsAdapter(Action requestLogout) : ITaskbarSessionCommands
{
    /// <inheritdoc />
    public void RequestLogout() => requestLogout();
}
