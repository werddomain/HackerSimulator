using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Windowing.Core;
using HackerOs.Windowing.Abstractions;

namespace HackerOs.Platform.Blazor.Windows;

/// <summary>Projects a successful window-app process launch into the window runtime shared by both shells.</summary>
public sealed class WindowLaunchCoordinator
{
    /// <summary>Offset between two windows of the same app, and between two cascade slots, in CSS pixels.</summary>
    public const double CascadeStep = 28;

    private readonly WindowRuntime _windows;
    private readonly Func<AppManifest, string> _title;
    private readonly WindowGeometryRecorder? _geometry;
    private readonly HashSet<WindowOwnerId> _presenting = [];
    private readonly Dictionary<string, int> _shellSurfaces = new(StringComparer.Ordinal);

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
    /// Gets the title a new window of <paramref name="manifest"/> gets, and the one an empty title
    /// restores (ADR 0117 §1): the app's name in the desktop language.
    /// </summary>
    /// <param name="manifest">The app's manifest.</param>
    public string DefaultTitle(AppManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return _title(manifest);
    }

    /// <summary>
    /// Marks <paramref name="appId"/> as a shell surface (ADR 0160 §1): the shell draws it itself (the
    /// desktop under the windows), so none of its processes is ever presented as a window, whoever
    /// launched it. Reserved before the launch, so the process's <c>Running</c> state, which the shell
    /// turns into a window, finds the reservation already there.
    /// </summary>
    /// <param name="appId">The app drawn by the shell.</param>
    /// <returns>Disposing it releases the reservation.</returns>
    public IDisposable ReserveShellSurface(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        _shellSurfaces[appId] = _shellSurfaces.GetValueOrDefault(appId) + 1;
        return new SurfaceReservation(this, appId);
    }

    /// <summary>Gets whether <paramref name="appId"/> is drawn by the shell rather than in a window.</summary>
    /// <param name="appId">The app id.</param>
    public bool IsShellSurface(string appId) => _shellSurfaces.ContainsKey(appId);

    private void ReleaseShellSurface(string appId)
    {
        if (_shellSurfaces.TryGetValue(appId, out int count))
        {
            if (count <= 1)
            {
                _shellSurfaces.Remove(appId);
            }
            else
            {
                _shellSurfaces[appId] = count - 1;
            }
        }
    }

    private sealed class SurfaceReservation(WindowLaunchCoordinator owner, string appId) : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (!_released)
            {
                _released = true;
                owner.ReleaseShellSurface(appId);
            }
        }
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

        if (manifest.Kind != AppKind.Window || IsShellSurface(manifest.Id))
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

            int cascadeIndex = _windows.Windows.Count % 8;
            WindowBounds bounds = saved is null
                ? InitialBounds(options, cascadeIndex, _windows.WorkArea)
                : RestoredBounds(options, saved.Width, saved.Height, cascadeIndex, _windows.WorkArea);
            // Icons are package-relative in the manifest. All shell surfaces consume this resolved
            // RCL URL from the window snapshot instead of requesting icons/app.svg from the host.
            WindowRuntimeState state = new(
                WindowId.FromGuid(Guid.NewGuid()),
                manifest.Id,
                ownerInstanceId,
                _title(manifest),
                manifest.Presentation.IconAssetPaths.FirstOrDefault() is { } iconPath
                    ? LazyLoading.StaticWebAssetAppAssetSource.RelativeUrl(manifest, iconPath)
                    : null,
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
    /// Opens a window at its cascade slot with its saved size (ADR 0116 §4 as amended by ADR 0122:
    /// size only, never the position, like a GTK app under GNOME), corrected for what may have changed
    /// since it was written: the size is brought between the minimum and maximum the app declares
    /// <em>today</em> (a fixed-size window takes its declared size), then shrunk to the work area
    /// without going below the minimum.
    /// </summary>
    /// <param name="options">Declared or default window options.</param>
    /// <param name="savedWidth">Normal width read from the state file.</param>
    /// <param name="savedHeight">Normal height read from the state file.</param>
    /// <param name="cascadeIndex">Cascade slot, 0 to 7.</param>
    /// <param name="workArea">Current desktop work area.</param>
    public static WindowBounds RestoredBounds(
        WindowOptions options, double savedWidth, double savedHeight, int cascadeIndex, WindowBounds workArea)
    {
        ArgumentNullException.ThrowIfNull(options);

        double width = options.Resizable ? savedWidth : options.DefaultWidth;
        double height = options.Resizable ? savedHeight : options.DefaultHeight;
        width = Math.Max(options.MinWidth, Math.Min(width, options.MaxWidth ?? double.MaxValue));
        height = Math.Max(options.MinHeight, Math.Min(height, options.MaxHeight ?? double.MaxValue));
        width = Math.Max(options.MinWidth, Math.Min(width, workArea.Width));
        height = Math.Max(options.MinHeight, Math.Min(height, workArea.Height));

        WindowBounds slot = InitialBounds(options, cascadeIndex, workArea);
        return new WindowBounds(slot.X, slot.Y, width, height);
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
