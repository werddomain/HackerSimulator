using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Platform.Blazor.Windows;

/// <summary>Describes the visible layout state of a window.</summary>
public enum WindowVisualState
{
    /// <summary>The window uses its current freeform geometry.</summary>
    Normal,
    /// <summary>The window is represented by the taskbar and hidden from the desktop.</summary>
    Minimized,
    /// <summary>The window fills the current desktop work area.</summary>
    Maximized,
}

/// <summary>Describes whether a window blocks interaction with an owner.</summary>
public enum WindowModality
{
    /// <summary>The window does not block another window.</summary>
    Modeless,
    /// <summary>The window blocks its owner until it closes.</summary>
    OwnerModal,
}

/// <summary>Contains all renderer-independent authoritative state for one window.</summary>
public sealed record WindowRuntimeState
{
    /// <summary>Creates a validated window state snapshot.</summary>
    public WindowRuntimeState(
        WindowId id,
        string appId,
        ProcessId processId,
        AppInstanceId appInstanceId,
        string title,
        string? iconAssetPath,
        WindowBounds bounds,
        WindowBounds? restoreBounds,
        int zOrder,
        WindowVisualState visualState,
        WindowConstraints constraints,
        WindowModality modality = WindowModality.Modeless,
        WindowId? ownerId = null,
        bool isFocused = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (id == default)
        {
            throw new ArgumentException("Window ID must be initialized.", nameof(id));
        }

        if (zOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zOrder), "Z-order cannot be negative.");
        }

        if (modality == WindowModality.OwnerModal && ownerId is null)
        {
            throw new ArgumentException("An owner-modal window requires an owner.", nameof(ownerId));
        }

        if (ownerId == id)
        {
            throw new ArgumentException("A window cannot own itself.", nameof(ownerId));
        }

        Id = id;
        AppId = appId;
        ProcessId = processId;
        AppInstanceId = appInstanceId;
        Title = title;
        IconAssetPath = iconAssetPath;
        Bounds = bounds;
        RestoreBounds = restoreBounds;
        ZOrder = zOrder;
        VisualState = visualState;
        Constraints = constraints ?? throw new ArgumentNullException(nameof(constraints));
        Modality = modality;
        OwnerId = ownerId;
        IsFocused = isFocused;
    }

    /// <summary>Gets the window identity.</summary>
    public WindowId Id { get; }

    /// <summary>Gets the immutable application identity.</summary>
    public string AppId { get; }

    /// <summary>Gets the simulated process identity.</summary>
    public ProcessId ProcessId { get; }

    /// <summary>Gets the running app instance identity.</summary>
    public AppInstanceId AppInstanceId { get; }

    /// <summary>Gets the displayed title.</summary>
    public string Title { get; }

    /// <summary>Gets the optional package-local icon asset path.</summary>
    public string? IconAssetPath { get; }

    /// <summary>Gets the current normal-state geometry.</summary>
    public WindowBounds Bounds { get; }

    /// <summary>Gets geometry captured before maximizing.</summary>
    public WindowBounds? RestoreBounds { get; }

    /// <summary>Gets the deterministic stacking order.</summary>
    public int ZOrder { get; }

    /// <summary>Gets the visible layout state.</summary>
    public WindowVisualState VisualState { get; }

    /// <summary>Gets resize and dimension constraints.</summary>
    public WindowConstraints Constraints { get; }

    /// <summary>Gets the blocking relationship type.</summary>
    public WindowModality Modality { get; }

    /// <summary>Gets the optional owning window.</summary>
    public WindowId? OwnerId { get; }

    /// <summary>Gets whether this window currently owns keyboard focus.</summary>
    public bool IsFocused { get; }
}