using HackerOs.Windowing.Abstractions;

namespace HackerOs.Windowing.Core;

/// <summary>Owns authoritative window snapshots and applies deterministic transitions.</summary>
public sealed class WindowRuntime
{
    private readonly Dictionary<WindowId, WindowRuntimeState> _windows = [];
    private readonly HashSet<WindowId> _pendingClose = [];
    private WindowBounds _workArea;
    private int _nextZOrder;

    /// <summary>Event raised whenever window state or active windows change.</summary>
    public event Action? StateChanged;

    /// <summary>Creates a runtime for the supplied desktop work area.</summary>
    public WindowRuntime(WindowBounds workArea) => _workArea = workArea;

    /// <summary>Gets stable snapshots ordered from back to front.</summary>
    public IReadOnlyList<WindowRuntimeState> Windows =>
        [.. _windows.Values.OrderBy(window => window.ZOrder).ThenBy(window => window.Id.Value)];

    /// <summary>Gets whether an owner is blocked by one of its active modal windows.</summary>
    public bool IsInteractionBlocked(WindowId id) => _windows.Values.Any(window =>
        window.Modality == WindowModality.OwnerModal
        && window.OwnerId == id
        && window.VisualState != WindowVisualState.Minimized);

    /// <summary>Applies one command atomically and returns ordered facts describing the change.</summary>
    public IReadOnlyList<WindowEvent> Apply(WindowCommand command)
    {
        IReadOnlyList<WindowEvent> events = command switch
        {
            CreateWindowCommand create => Create(create.InitialState),
            FocusWindowCommand focus => Focus(focus.WindowId),
            MoveWindowCommand move => Move(move),
            ResizeWindowCommand resize => Resize(resize),
            MinimizeWindowCommand minimize => ChangeVisualState(minimize.WindowId, WindowVisualState.Minimized),
            MaximizeWindowCommand maximize => ChangeVisualState(maximize.WindowId, WindowVisualState.Maximized),
            RestoreWindowCommand restore => ChangeVisualState(restore.WindowId, WindowVisualState.Normal),
            RequestWindowCloseCommand close => RequestClose(close.WindowId),
            CancelWindowCloseCommand close => CancelClose(close.WindowId),
            ForceWindowCloseCommand close => ForceClose(close.WindowId),
            ChangeViewportCommand viewport => ChangeViewport(viewport.WorkArea),
            null => throw new ArgumentNullException(nameof(command)),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown window command."),
        };

        if (events.Count > 0)
        {
            StateChanged?.Invoke();
        }

        return events;
    }

    private IReadOnlyList<WindowEvent> Create(WindowRuntimeState initialState)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        if (_windows.ContainsKey(initialState.Id))
        {
            throw new InvalidOperationException($"Window '{initialState.Id}' is already registered.");
        }

        if (initialState.OwnerId is WindowId ownerId && !_windows.ContainsKey(ownerId))
        {
            throw new InvalidOperationException($"Owner window '{ownerId}' is not registered.");
        }

        WindowId? previousFocus = _windows.Values.SingleOrDefault(window => window.IsFocused)?.Id;
        ClearFocus();
        WindowRuntimeState created = Copy(
            initialState,
            bounds: Constrain(initialState.Bounds, initialState.Constraints),
            zOrder: NextZOrder(),
            isFocused: true);
        _windows.Add(created.Id, created);
        return [new WindowCreatedEvent(created), new WindowFocusedEvent(previousFocus, created.Id, created.ZOrder)];
    }

    private IReadOnlyList<WindowEvent> Focus(WindowId id)
    {
        WindowRuntimeState target = Get(id);
        if (IsInteractionBlocked(id))
        {
            throw new InvalidOperationException("A window blocked by an owner-modal child cannot receive focus.");
        }
        if (target.VisualState == WindowVisualState.Minimized)
        {
            throw new InvalidOperationException("A minimized window must be restored before it can receive focus.");
        }

        WindowId? previousFocus = _windows.Values.SingleOrDefault(window => window.IsFocused)?.Id;
        if (previousFocus == id)
        {
            return [];
        }

        ClearFocus();
        target = Copy(target, zOrder: NextZOrder(), isFocused: true);
        _windows[id] = target;
        return [new WindowFocusedEvent(previousFocus, id, target.ZOrder)];
    }

    private IReadOnlyList<WindowEvent> Move(MoveWindowCommand command)
    {
        WindowRuntimeState current = RequireNormal(command.WindowId);
        WindowBounds bounds = Constrain(
            new WindowBounds(command.X, command.Y, current.Bounds.Width, current.Bounds.Height),
            current.Constraints);
        _windows[current.Id] = Copy(current, bounds: bounds);
        return [new WindowMovedEvent(current.Id, current.Bounds, bounds)];
    }

    private IReadOnlyList<WindowEvent> Resize(ResizeWindowCommand command)
    {
        WindowRuntimeState current = RequireNormal(command.WindowId);
        if (!current.Constraints.IsResizable)
        {
            throw new InvalidOperationException("This window is not resizable.");
        }

        WindowBounds bounds = Constrain(command.Bounds, current.Constraints);
        _windows[current.Id] = Copy(current, bounds: bounds);
        return [new WindowResizedEvent(current.Id, current.Bounds, bounds)];
    }

    private IReadOnlyList<WindowEvent> ChangeVisualState(WindowId id, WindowVisualState requestedState)
    {
        WindowRuntimeState current = Get(id);
        if (current.VisualState == requestedState)
        {
            return [];
        }

        WindowBounds bounds = current.Bounds;
        WindowBounds? restoreBounds = current.RestoreBounds;
        if (requestedState == WindowVisualState.Maximized)
        {
            restoreBounds = current.VisualState == WindowVisualState.Normal ? current.Bounds : restoreBounds;
            bounds = _workArea;
        }
        else if (requestedState == WindowVisualState.Normal && restoreBounds is not null)
        {
            bounds = restoreBounds.Value;
            restoreBounds = null;
        }

        bool retainFocus = requestedState != WindowVisualState.Minimized && current.IsFocused;
        WindowRuntimeState changed = Copy(
            current,
            bounds: bounds,
            restoreBounds: restoreBounds,
            replaceRestoreBounds: true,
            visualState: requestedState,
            isFocused: retainFocus);
        _windows[id] = changed;

        List<WindowEvent> events = [new WindowVisualStateChangedEvent(id, current.VisualState, requestedState, bounds, restoreBounds)];
        if (requestedState == WindowVisualState.Minimized && current.IsFocused)
        {
            events.AddRange(FocusTopVisible(id));
        }
        else if (!retainFocus)
        {
            events.AddRange(Focus(id));
        }

        return events;
    }

    private IReadOnlyList<WindowEvent> RequestClose(WindowId id)
    {
        if (!_windows.ContainsKey(id))
        {
            return [];
        }

        return _pendingClose.Add(id) ? [new WindowCloseRequestedEvent(id)] : [];
    }

    private IReadOnlyList<WindowEvent> CancelClose(WindowId id)
    {
        if (!_windows.ContainsKey(id))
        {
            return [];
        }

        return _pendingClose.Remove(id) ? [new WindowCloseCancelledEvent(id)] : [];
    }

    private IReadOnlyList<WindowEvent> ForceClose(WindowId id)
    {
        if (!_windows.TryGetValue(id, out WindowRuntimeState? current))
        {
            return [];
        }

        _windows.Remove(id);
        bool wasRequested = _pendingClose.Remove(id);
        List<WindowEvent> events = [new WindowClosedEvent(id, !wasRequested)];
        if (current.IsFocused && current.OwnerId is WindowId ownerId && _windows.ContainsKey(ownerId))
        {
            events.AddRange(Focus(ownerId));
        }
        else if (current.IsFocused)
        {
            events.AddRange(FocusTopVisible(id));
        }

        return events;
    }

    private IReadOnlyList<WindowEvent> ChangeViewport(WindowBounds workArea)
    {
        _workArea = workArea;
        foreach (WindowRuntimeState window in _windows.Values.ToArray())
        {
            WindowBounds bounds = window.VisualState == WindowVisualState.Maximized
                ? workArea
                : Constrain(window.Bounds, window.Constraints);
            WindowBounds? restoreBounds = window.RestoreBounds is WindowBounds restore
                ? Constrain(restore, window.Constraints)
                : null;
            _windows[window.Id] = Copy(
                window,
                bounds: bounds,
                restoreBounds: restoreBounds,
                replaceRestoreBounds: true);
        }

        return [new WindowViewportChangedEvent(workArea, Windows)];
    }

    private IReadOnlyList<WindowEvent> FocusTopVisible(WindowId excludedId)
    {
        WindowRuntimeState? next = _windows.Values
            .Where(window => window.Id != excludedId && window.VisualState != WindowVisualState.Minimized)
            .OrderByDescending(window => window.ZOrder)
            .FirstOrDefault();
        return next is null ? [] : Focus(next.Id);
    }

    private WindowRuntimeState RequireNormal(WindowId id)
    {
        WindowRuntimeState window = Get(id);
        return window.VisualState == WindowVisualState.Normal
            ? window
            : throw new InvalidOperationException("Move and resize commands require a normal window.");
    }

    private WindowRuntimeState Get(WindowId id) => _windows.TryGetValue(id, out WindowRuntimeState? window)
        ? window
        : throw new KeyNotFoundException($"Window '{id}' is not registered.");

    private void ClearFocus()
    {
        foreach (WindowRuntimeState focused in _windows.Values.Where(window => window.IsFocused).ToArray())
        {
            _windows[focused.Id] = Copy(focused, isFocused: false);
        }
    }

    private int NextZOrder() => checked(++_nextZOrder);

    private WindowBounds Constrain(WindowBounds requested, WindowConstraints constraints)
    {
        double minimumWidth = Math.Min(constraints.MinWidth, _workArea.Width);
        double minimumHeight = Math.Min(constraints.MinHeight, _workArea.Height);
        double maximumWidth = Math.Min(constraints.MaxWidth ?? _workArea.Width, _workArea.Width);
        double maximumHeight = Math.Min(constraints.MaxHeight ?? _workArea.Height, _workArea.Height);
        double width = Math.Clamp(requested.Width, minimumWidth, maximumWidth);
        double height = Math.Clamp(requested.Height, minimumHeight, maximumHeight);
        double x = Math.Clamp(requested.X, _workArea.X, _workArea.X + _workArea.Width - width);
        double y = Math.Clamp(requested.Y, _workArea.Y, _workArea.Y + _workArea.Height - height);
        return new WindowBounds(x, y, width, height);
    }

    private static WindowRuntimeState Copy(
        WindowRuntimeState state,
        WindowBounds? bounds = null,
        WindowBounds? restoreBounds = null,
        bool replaceRestoreBounds = false,
        int? zOrder = null,
        WindowVisualState? visualState = null,
        bool? isFocused = null) =>
        new(
            state.Id,
            state.AppId,
            state.OwnerInstanceId,
            state.Title,
            state.IconAssetPath,
            bounds ?? state.Bounds,
            replaceRestoreBounds ? restoreBounds : state.RestoreBounds,
            zOrder ?? state.ZOrder,
            visualState ?? state.VisualState,
            state.Constraints,
            state.Modality,
            state.OwnerId,
            isFocused ?? state.IsFocused,
            state.Content,
            state.OnRequestClose);
}