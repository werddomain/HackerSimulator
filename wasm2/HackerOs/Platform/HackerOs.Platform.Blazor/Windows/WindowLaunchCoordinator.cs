using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Windowing.Core;
using HackerOs.Windowing.Abstractions;

namespace HackerOs.Platform.Blazor.Windows;

/// <summary>Projects a successful window-app process launch into the visible desktop runtime.</summary>
public sealed class WindowLaunchCoordinator
{
    private readonly WindowRuntime _windows;

    /// <summary>Initializes the coordinator over the authoritative desktop window runtime.</summary>
    public WindowLaunchCoordinator(WindowRuntime windows) =>
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));

    /// <summary>
    /// Creates a desktop window for a newly launched process, or restores and focuses the
    /// existing window returned by a single-instance launch.
    /// </summary>
    /// <returns><see langword="true"/> when a window is now visible or focused.</returns>
    public bool Present(AppManifest manifest, AppLaunchResult launch)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(launch);

        if (!launch.IsSuccess || launch.Process is not ProcessRecord process)
        {
            return false;
        }

        return Present(manifest, process);
    }

    /// <summary>
    /// Creates a desktop window for a process already confirmed running, or focuses its existing
    /// window. Used to project window-app launches that were dispatched by policy code with no
    /// direct route back to this UI-owned coordinator (e.g. another app opening a file via
    /// <c>IAppIntentGateway</c>), reacting instead to the process's own state transition.
    /// </summary>
    /// <returns><see langword="true"/> when a window is now visible or focused.</returns>
    public bool Present(AppManifest manifest, ProcessRecord process)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(process);

        if (manifest.Kind != AppKind.Window)
        {
            return false;
        }

        WindowOwnerId ownerInstanceId = WindowOwnerId.FromGuid(process.AppInstanceId.Value);
        WindowRuntimeState? existing = _windows.Windows.FirstOrDefault(window => window.OwnerInstanceId == ownerInstanceId);
        if (existing is not null)
        {
            if (existing.VisualState == WindowVisualState.Minimized)
            {
                _windows.Apply(new RestoreWindowCommand(existing.Id));
            }

            _windows.Apply(new FocusWindowCommand(existing.Id));
            return true;
        }

        int cascadeIndex = _windows.Windows.Count % 8;
        WindowOptions options = manifest.Window ?? WindowOptions.Default;
        WindowRuntimeState state = new(
            WindowId.FromGuid(Guid.NewGuid()),
            manifest.Id,
            ownerInstanceId,
            manifest.Name,
            manifest.Presentation.IconAssetPaths.FirstOrDefault(),
            InitialBounds(options, cascadeIndex, _windows.WorkArea),
            restoreBounds: null,
            zOrder: 0,
            visualState: WindowVisualState.Normal,
            constraints: ToConstraints(options),
            modality: WindowModality.Modeless,
            supportsBack: manifest.SupportsBack);
        _windows.Apply(new CreateWindowCommand(state));
        return true;
    }

    /// <summary>
    /// Maps an app's declared window sizes (ADR 0095 §2) to window-manager constraints. The declared
    /// maximum only limits the user's resizes; maximizing still fills the work area.
    /// </summary>
    /// <param name="options">Declared or default window options.</param>
    public static WindowConstraints ToConstraints(WindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new WindowConstraints(
            isResizable: options.Resizable,
            minWidth: options.MinWidth,
            minHeight: options.MinHeight,
            maxWidth: options.MaxWidth,
            maxHeight: options.MaxHeight);
    }

    /// <summary>
    /// Places a new window at its cascade slot with its declared default size, shrunk to the work area
    /// on a smaller screen but never below the declared minimum (the window then overflows, as on a
    /// small real screen).
    /// </summary>
    /// <param name="options">Declared or default window options.</param>
    /// <param name="cascadeIndex">Cascade slot, 0 to 7.</param>
    /// <param name="workArea">Current desktop work area.</param>
    public static WindowBounds InitialBounds(WindowOptions options, int cascadeIndex, WindowBounds workArea)
    {
        ArgumentNullException.ThrowIfNull(options);
        double x = workArea.X + 48 + (cascadeIndex * 28);
        double y = workArea.Y + 42 + (cascadeIndex * 28);
        double width = Math.Max(options.MinWidth, Math.Min(options.DefaultWidth, workArea.Width));
        double height = Math.Max(options.MinHeight, Math.Min(options.DefaultHeight, workArea.Height));
        return new WindowBounds(x, y, width, height);
    }
}
