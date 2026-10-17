using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Windowing.Core;
using HackerOs.Windowing.Abstractions;

namespace HackerOs.Platform.Blazor.Windows;

/// <summary>Projects a successful window-app process launch into the visible desktop runtime.</summary>
public sealed class WindowLaunchCoordinator
{
    /// <summary>Offset between two windows of the same app, and between two cascade slots, in CSS pixels.</summary>
    public const double CascadeStep = 28;

    /// <summary>Width of the title bar that must stay on screen when restoring a saved position (ADR 0116 §4).</summary>
    public const double MinimumVisibleTitleBarWidth = 48;

    /// <summary>Height of the default title bar (<c>--hos-titlebar-height</c>), kept inside the work area.</summary>
    private const double TitleBarHeight = 38;

    private readonly WindowRuntime _windows;
    private readonly Func<AppManifest, string> _title;
    private readonly WindowGeometryRecorder? _geometry;
    private readonly HashSet<WindowOwnerId> _presenting = [];

    /// <summary>Initializes the coordinator over the authoritative desktop window runtime.</summary>
    /// <param name="windows">The desktop window runtime.</param>
    public WindowLaunchCoordinator(WindowRuntime windows)
        : this(windows, static manifest => manifest.Name, geometry: null)
    {
    }

    /// <summary>
    /// Initializes the coordinator with window geometry memory (ADR 0116) and untranslated titles.
    /// </summary>
    /// <param name="windows">The desktop window runtime.</param>
    /// <param name="geometry">Reads saved geometry and records the geometry of new windows.</param>
    public WindowLaunchCoordinator(WindowRuntime windows, WindowGeometryRecorder geometry)
        : this(windows, static manifest => manifest.Name, geometry ?? throw new ArgumentNullException(nameof(geometry)))
    {
    }

    /// <summary>
    /// Initializes the coordinator with the session's translations of app names (ADR 0104 section 4):
    /// a new window's title is the app's name in the desktop language.
    /// </summary>
    /// <param name="windows">The desktop window runtime.</param>
    /// <param name="catalog">The catalog holding the translated names.</param>
    /// <param name="locale">The session language.</param>
    /// <param name="geometry">
    /// Window geometry memory (ADR 0116); <see langword="null"/> opens every window at its cascade slot.
    /// </param>
    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public WindowLaunchCoordinator(
        WindowRuntime windows,
        Core.AppCatalog catalog,
        Core.Globalization.SessionLocaleService locale,
        WindowGeometryRecorder? geometry = null)
        : this(windows, manifest => catalog.Localize(manifest, locale.Culture).Name, geometry)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(locale);
    }

    private WindowLaunchCoordinator(WindowRuntime windows, Func<AppManifest, string> title, WindowGeometryRecorder? geometry)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _title = title;
        _geometry = geometry;
    }

    /// <summary>
    /// Creates a desktop window for a newly launched process, or restores and focuses the
    /// existing window returned by a single-instance launch.
    /// </summary>
    /// <param name="manifest">Manifest of the launched app.</param>
    /// <param name="launch">Result of the launch.</param>
    /// <param name="cancellationToken">Cancels reading the saved geometry.</param>
    /// <returns><see langword="true"/> when a window is now visible or focused.</returns>
    public Task<bool> PresentAsync(AppManifest manifest, AppLaunchResult launch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(launch);

        if (!launch.IsSuccess || launch.Process is not ProcessRecord process)
        {
            return Task.FromResult(false);
        }

        return PresentAsync(manifest, process, cancellationToken);
    }

    /// <summary>
    /// Creates a desktop window for a process already confirmed running, or focuses its existing
    /// window. Used to project window-app launches that were dispatched by policy code with no
    /// direct route back to this UI-owned coordinator (e.g. another app opening a file via
    /// <c>IAppIntentGateway</c>), reacting instead to the process's own state transition.
    /// </summary>
    /// <remarks>
    /// A new window opens where its app's window was last left (ADR 0116 §4) when the app remembers
    /// its geometry and a state file exists; otherwise at its cascade slot. The shell may present the
    /// same process twice (its own launch and the process's Running transition): while the saved
    /// geometry is being read, a second call for the same process returns at once instead of opening
    /// a second window.
    /// </remarks>
    /// <param name="manifest">Manifest of the running app.</param>
    /// <param name="process">The app's process.</param>
    /// <param name="cancellationToken">Cancels reading the saved geometry.</param>
    /// <returns><see langword="true"/> when a window is now visible or focused.</returns>
    public async Task<bool> PresentAsync(AppManifest manifest, ProcessRecord process, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(process);

        if (manifest.Kind != AppKind.Window)
        {
            return false;
        }

        WindowOwnerId ownerInstanceId = WindowOwnerId.FromGuid(process.AppInstanceId.Value);
        if (FocusExisting(ownerInstanceId))
        {
            return true;
        }

        if (!_presenting.Add(ownerInstanceId))
        {
            return true;
        }

        try
        {
            WindowOptions options = manifest.Window ?? WindowOptions.Default;
            bool remember = _geometry is not null && options.RememberGeometry;
            SavedWindowGeometry? saved = remember
                ? await _geometry!.ReadAsync(manifest.Id, cancellationToken)
                : null;

            // The read may have yielded: another caller may have opened the window meanwhile.
            if (FocusExisting(ownerInstanceId))
            {
                return true;
            }

            int sameAppWindows = _windows.Windows.Count(window => window.AppId == manifest.Id);
            WindowBounds bounds = saved is null
                ? InitialBounds(options, _windows.Windows.Count % 8, _windows.WorkArea)
                : RestoredBounds(options, saved.Bounds, sameAppWindows, _windows.WorkArea);
            WindowRuntimeState state = new(
                WindowId.FromGuid(Guid.NewGuid()),
                manifest.Id,
                ownerInstanceId,
                _title(manifest),
                manifest.Presentation.IconAssetPaths.FirstOrDefault(),
                bounds,
                restoreBounds: null,
                zOrder: 0,
                visualState: WindowVisualState.Normal,
                constraints: ToConstraints(options),
                modality: WindowModality.Modeless,
                supportsBack: manifest.SupportsBack);
            _windows.Apply(new CreateWindowCommand(state));

            // Maximized last time: open maximized with the saved bounds as restore geometry. A fixed-size
            // window has no Maximize button, so it never reopens maximized.
            if (saved is { IsMaximized: true } && options.Resizable)
            {
                _windows.Apply(new MaximizeWindowCommand(state.Id));
            }

            if (remember)
            {
                _geometry!.Track(state.Id, manifest.Id);
            }

            return true;
        }
        finally
        {
            _presenting.Remove(ownerInstanceId);
        }
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
        double x = workArea.X + 48 + (cascadeIndex * CascadeStep);
        double y = workArea.Y + 42 + (cascadeIndex * CascadeStep);
        double width = Math.Max(options.MinWidth, Math.Min(options.DefaultWidth, workArea.Width));
        double height = Math.Max(options.MinHeight, Math.Min(options.DefaultHeight, workArea.Height));
        return new WindowBounds(x, y, width, height);
    }

    /// <summary>
    /// Places a window at its saved geometry (ADR 0116 §4), corrected for what may have changed since
    /// it was written: the size is brought between the minimum and maximum the app declares
    /// <em>today</em> (a fixed-size window takes its declared size), then shrunk to the work area without
    /// going below the minimum; the position keeps at least <see cref="MinimumVisibleTitleBarWidth"/>
    /// pixels of the title bar on screen horizontally and the top of the window inside the work area,
    /// like a window manager that lost a monitor. A second window of the same app is offset by
    /// <see cref="CascadeStep"/> per window already open, so it does not exactly cover the first.
    /// </summary>
    /// <remarks>
    /// <see cref="WindowRuntime"/> applies its own constraint afterwards and keeps a normal window
    /// inside the work area, so the window always ends up fully reachable.
    /// </remarks>
    /// <param name="options">Declared or default window options.</param>
    /// <param name="saved">Normal bounds read from the state file.</param>
    /// <param name="sameAppWindows">Windows of the same app already open.</param>
    /// <param name="workArea">Current desktop work area.</param>
    public static WindowBounds RestoredBounds(WindowOptions options, WindowBounds saved, int sameAppWindows, WindowBounds workArea)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(sameAppWindows);

        double width = options.Resizable ? saved.Width : options.DefaultWidth;
        double height = options.Resizable ? saved.Height : options.DefaultHeight;
        width = Math.Max(options.MinWidth, Math.Min(width, options.MaxWidth ?? double.MaxValue));
        height = Math.Max(options.MinHeight, Math.Min(height, options.MaxHeight ?? double.MaxValue));
        width = Math.Max(options.MinWidth, Math.Min(width, workArea.Width));
        height = Math.Max(options.MinHeight, Math.Min(height, workArea.Height));

        double offset = sameAppWindows * CascadeStep;
        double minimumX = workArea.X - width + MinimumVisibleTitleBarWidth;
        double maximumX = workArea.X + workArea.Width - MinimumVisibleTitleBarWidth;
        double maximumY = Math.Max(workArea.Y, workArea.Y + workArea.Height - TitleBarHeight);
        double x = Math.Clamp(saved.X + offset, minimumX, Math.Max(minimumX, maximumX));
        double y = Math.Clamp(saved.Y + offset, workArea.Y, maximumY);
        return new WindowBounds(x, y, width, height);
    }

    private bool FocusExisting(WindowOwnerId ownerInstanceId)
    {
        WindowRuntimeState? existing = _windows.Windows.FirstOrDefault(window => window.OwnerInstanceId == ownerInstanceId);
        if (existing is null)
        {
            return false;
        }

        if (existing.VisualState == WindowVisualState.Minimized)
        {
            _windows.Apply(new RestoreWindowCommand(existing.Id));
        }

        _windows.Apply(new FocusWindowCommand(existing.Id));
        return true;
    }
}
