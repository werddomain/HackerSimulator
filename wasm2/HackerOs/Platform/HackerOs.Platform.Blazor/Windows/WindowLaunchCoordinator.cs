using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Simulation.Abstractions.Processes;

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

        WindowRuntimeState? existing = _windows.Windows.FirstOrDefault(window => window.ProcessId == process.Pid);
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
        WindowRuntimeState state = new(
            WindowId.FromGuid(Guid.NewGuid()),
            manifest.Id,
            process.Pid,
            process.AppInstanceId,
            manifest.Name,
            manifest.Presentation.IconAssetPaths.FirstOrDefault(),
            new WindowBounds(48 + (cascadeIndex * 28), 42 + (cascadeIndex * 28), 800, 560),
            restoreBounds: null,
            zOrder: 0,
            WindowVisualState.Normal,
            new WindowConstraints(isResizable: true, minWidth: 360, minHeight: 240),
            WindowModality.Modeless);
        _windows.Apply(new CreateWindowCommand(state));
        return true;
    }
}
