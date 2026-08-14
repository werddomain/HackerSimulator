namespace HackerOs.Windowing.Core;

/// <summary>Base contract for an immutable request to mutate window runtime state.</summary>
public abstract record WindowCommand;

/// <summary>Requests registration of a validated initial window snapshot.</summary>
public sealed record CreateWindowCommand(WindowRuntimeState InitialState) : WindowCommand;

/// <summary>Requests focus and front placement for a window.</summary>
public sealed record FocusWindowCommand(WindowId WindowId) : WindowCommand;

/// <summary>Requests a new top-left position in CSS pixels.</summary>
public sealed record MoveWindowCommand(WindowId WindowId, double X, double Y) : WindowCommand;

/// <summary>Requests new position and dimensions for an edge/corner resize.</summary>
public sealed record ResizeWindowCommand(WindowId WindowId, WindowBounds Bounds) : WindowCommand;

/// <summary>Requests taskbar minimization for a window.</summary>
public sealed record MinimizeWindowCommand(WindowId WindowId) : WindowCommand;

/// <summary>Requests maximization within the current work area.</summary>
public sealed record MaximizeWindowCommand(WindowId WindowId) : WindowCommand;

/// <summary>Requests restoration to the last normal geometry.</summary>
public sealed record RestoreWindowCommand(WindowId WindowId) : WindowCommand;

/// <summary>Requests a graceful, cancellable close.</summary>
public sealed record RequestWindowCloseCommand(WindowId WindowId) : WindowCommand;

/// <summary>Cancels a pending graceful close after confirmation is declined.</summary>
public sealed record CancelWindowCloseCommand(WindowId WindowId) : WindowCommand;

/// <summary>Requests unconditional final removal after lifecycle coordination.</summary>
public sealed record ForceWindowCloseCommand(WindowId WindowId) : WindowCommand;

/// <summary>Reports a new desktop work area that may constrain every window.</summary>
public sealed record ChangeViewportCommand(WindowBounds WorkArea) : WindowCommand;

/// <summary>Base contract for an immutable fact emitted after a window transition.</summary>
public abstract record WindowEvent;

/// <summary>Reports that a window entered the runtime.</summary>
public sealed record WindowCreatedEvent(WindowRuntimeState Window) : WindowEvent;

/// <summary>Reports a focus ownership change.</summary>
public sealed record WindowFocusedEvent(WindowId? PreviousWindowId, WindowId WindowId, int ZOrder) : WindowEvent;

/// <summary>Reports a completed move.</summary>
public sealed record WindowMovedEvent(WindowId WindowId, WindowBounds PreviousBounds, WindowBounds Bounds) : WindowEvent;

/// <summary>Reports a completed resize.</summary>
public sealed record WindowResizedEvent(WindowId WindowId, WindowBounds PreviousBounds, WindowBounds Bounds) : WindowEvent;

/// <summary>Reports a visual-state transition.</summary>
public sealed record WindowVisualStateChangedEvent(
    WindowId WindowId,
    WindowVisualState PreviousState,
    WindowVisualState State,
    WindowBounds Bounds,
    WindowBounds? RestoreBounds) : WindowEvent;

/// <summary>Reports that graceful close coordination must begin.</summary>
public sealed record WindowCloseRequestedEvent(WindowId WindowId) : WindowEvent;

/// <summary>Reports that a pending close was cancelled and the window remains active.</summary>
public sealed record WindowCloseCancelledEvent(WindowId WindowId) : WindowEvent;

/// <summary>Reports final removal from the runtime.</summary>
public sealed record WindowClosedEvent(WindowId WindowId, bool WasForced) : WindowEvent;

/// <summary>Reports a work-area change and the resulting window snapshots.</summary>
public sealed record WindowViewportChangedEvent(WindowBounds WorkArea, IReadOnlyList<WindowRuntimeState> Windows) : WindowEvent;