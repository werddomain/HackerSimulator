using System.Globalization;
using HackerOs.App.Abstractions;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Platform.Core.Shell;
using HackerOs.Simulation.Abstractions.Sessions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace HackerOs.Platform.Blazor.Shell;

/// <summary>
/// Implements the Windows 7-style Start menu interaction model while keeping application launch
/// authority in the owning desktop shell.
/// </summary>
public partial class AppLauncher : ComponentBase, IDisposable
{
    internal const string FileExplorerAppId = "org.hackeros.file-explorer";
    internal const string SettingsAppId = "org.hackeros.settings";

    private ElementReference _searchInputRef;
    private AuthenticatedPrincipal? _principal;
    private IReadOnlyList<string> _pinnedAppIds = [];
    private LauncherAppProjectionResult _projection = LauncherAppProjectionResult.Empty;
    private string _searchQuery = string.Empty;
    private string? _selectedCategory;
    private int _selectedIndex;
    private bool _isUpdatingPreferences;
    private bool _preferencesSubscribed;
    private bool _disposed;

    /// <summary>Gets the immutable application catalog projected by this launcher.</summary>
    [Inject]
    private AppCatalog Catalog { get; set; } = null!;

    /// <summary>Gets the live enablement registry used to suppress disabled applications.</summary>
    [Inject]
    private IAppEnablementRegistry Enablement { get; set; } = null!;

    /// <summary>Gets the active local session and its user-scoped identity.</summary>
    [Inject]
    private ISessionService SessionService { get; set; } = null!;

    /// <summary>Gets the offline-first, user-isolated Start menu preference store.</summary>
    [Inject]
    private StartMenuPreferencesService Preferences { get; set; } = null!;

    /// <summary>Raised when the launcher should be dismissed.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>
    /// Raised for every launch request, including quick links. The desktop shell remains the sole
    /// lifecycle/orchestration authority; this component never dispatches an intent directly.
    /// </summary>
    [Parameter]
    public EventCallback<AppManifest> OnAppSelected { get; set; }

    private bool HasSearchQuery => !string.IsNullOrWhiteSpace(_searchQuery);

    private bool CanManagePins => _principal is not null && !_isUpdatingPreferences;

    private string PrincipalDisplayName => _principal?.DisplayName ?? "Local user";

    private string PrincipalLoginName => _principal is null ? "Offline session" : $"@{_principal.LoginName.Value}";

    private AppManifest? FileExplorerApp => FindLaunchableApp(FileExplorerAppId);

    private AppManifest? SettingsApp => FindLaunchableApp(SettingsAppId);

    private string? ActiveOptionId => _selectedIndex >= 0 && _selectedIndex < _projection.VisibleApps.Count
        ? GetOptionId(_projection.VisibleApps[_selectedIndex])
        : null;

    private string SearchStatus => HasSearchQuery
        ? $"{_projection.VisibleApps.Count} application{(_projection.VisibleApps.Count == 1 ? string.Empty : "s")} found."
        : string.Empty;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        _principal = SessionService.CurrentPrincipal;
        if (_principal is not null)
        {
            Preferences.Changed += OnPreferencesChanged;
            _preferencesSubscribed = true;
            await Preferences.InitializeAsync();
            RefreshPinnedAppIds();
        }

        RebuildProjection();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            await _searchInputRef.FocusAsync();
        }
        catch (InvalidOperationException)
        {
            // Headless component renderers do not provide a browser focus implementation.
        }
    }

    private void HandleSearchInput(ChangeEventArgs args)
    {
        _searchQuery = args.Value?.ToString() ?? string.Empty;
        RebuildProjection(resetSelection: true);
    }

    private async Task ClearSearch()
    {
        _searchQuery = string.Empty;
        RebuildProjection(resetSelection: true);

        try
        {
            await _searchInputRef.FocusAsync();
        }
        catch (InvalidOperationException)
        {
            // Headless component renderers do not provide a browser focus implementation.
        }
    }

    private void SelectCategory(string? category)
    {
        _selectedCategory = category;
        RebuildProjection(resetSelection: true);
    }

    private void SetSelectedIndex(int index)
    {
        if (index >= 0 && index < _projection.VisibleApps.Count)
        {
            _selectedIndex = index;
        }
    }

    private async Task HandleDialogKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
        {
            await CloseAsync();
        }
    }

    private async Task HandleSearchKeyDownAsync(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            case "Escape":
                await CloseAsync();
                break;
            case "ArrowDown":
                MoveSelection(1);
                break;
            case "ArrowUp":
                MoveSelection(-1);
                break;
            case "Enter" when _selectedIndex >= 0 && _selectedIndex < _projection.VisibleApps.Count:
                await LaunchAppAsync(_projection.VisibleApps[_selectedIndex]);
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_projection.VisibleApps.Count == 0)
        {
            _selectedIndex = -1;
            return;
        }

        _selectedIndex = (_selectedIndex + delta + _projection.VisibleApps.Count)
            % _projection.VisibleApps.Count;
    }

    private async Task TogglePinAsync(string appId)
    {
        if (_principal is null || _isUpdatingPreferences)
        {
            return;
        }

        _isUpdatingPreferences = true;
        try
        {
            await Preferences.ToggleAsync(_principal.UserId, appId);
        }
        finally
        {
            RefreshPinnedAppIds();
            RebuildProjection();
            _isUpdatingPreferences = false;
        }
    }

    private async Task MovePinnedAsync(string appId, int direction)
    {
        if (_principal is null || _isUpdatingPreferences || direction is < -1 or > 1 || direction == 0)
        {
            return;
        }

        int visibleIndex = FindVisiblePinnedIndex(appId);
        int adjacentVisibleIndex = visibleIndex + direction;
        if (visibleIndex < 0 || adjacentVisibleIndex < 0 || adjacentVisibleIndex >= _projection.PinnedApps.Count)
        {
            return;
        }

        // Move against the raw persisted index so unavailable IDs remain stored and keep their own
        // relative order while the two visible neighbors exchange places.
        string adjacentAppId = _projection.PinnedApps[adjacentVisibleIndex].Id;
        int targetPersistedIndex = FindPersistedPinnedIndex(adjacentAppId);
        if (targetPersistedIndex < 0)
        {
            return;
        }

        _isUpdatingPreferences = true;
        try
        {
            await Preferences.MoveAsync(_principal.UserId, appId, targetPersistedIndex);
        }
        finally
        {
            RefreshPinnedAppIds();
            RebuildProjection();
            _isUpdatingPreferences = false;
        }
    }

    private async Task LaunchQuickAppAsync(string appId)
    {
        AppManifest? app = FindLaunchableApp(appId);
        if (app is not null)
        {
            await LaunchAppAsync(app);
        }
    }

    private async Task LaunchAppAsync(AppManifest app)
    {
        if (!OnAppSelected.HasDelegate)
        {
            return;
        }

        await OnAppSelected.InvokeAsync(app);
        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (OnClose.HasDelegate)
        {
            await OnClose.InvokeAsync();
        }
    }

    private void OnPreferencesChanged()
    {
        if (_disposed)
        {
            return;
        }

        // The service may raise from a persistence continuation. Marshal the refresh onto Blazor's
        // renderer without blocking the service's mutation gate.
        _ = InvokeAsync(() =>
        {
            if (_disposed)
            {
                return;
            }

            RefreshPinnedAppIds();
            RebuildProjection();
            StateHasChanged();
        });
    }

    private void RefreshPinnedAppIds()
    {
        _pinnedAppIds = _principal is null
            ? []
            : Preferences.GetPinnedAppIds(_principal.UserId).ToArray();
    }

    private void RebuildProjection(bool resetSelection = false)
    {
        _projection = LauncherAppProjection.Create(
            Catalog.Manifests.Values,
            Enablement,
            _pinnedAppIds,
            _selectedCategory,
            _searchQuery);

        if (resetSelection)
        {
            _selectedIndex = 0;
        }

        if (_projection.VisibleApps.Count == 0)
        {
            _selectedIndex = -1;
        }
        else if (_selectedIndex < 0 || _selectedIndex >= _projection.VisibleApps.Count)
        {
            _selectedIndex = 0;
        }
    }

    private AppManifest? FindLaunchableApp(string appId) => _projection.LaunchableApps.FirstOrDefault(
        app => string.Equals(app.Id, appId, StringComparison.Ordinal));

    private bool IsPinned(string appId) => _pinnedAppIds.Contains(appId, StringComparer.Ordinal);

    private int FindVisiblePinnedIndex(string appId)
    {
        for (int index = 0; index < _projection.PinnedApps.Count; index++)
        {
            if (string.Equals(_projection.PinnedApps[index].Id, appId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private int FindPersistedPinnedIndex(string appId)
    {
        for (int index = 0; index < _pinnedAppIds.Count; index++)
        {
            if (string.Equals(_pinnedAppIds[index], appId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static string GetOptionId(AppManifest app) => $"launcher-option-{app.Id}";

    private static string FormatCategory(string category) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
        category.Replace('-', ' '));

    private static string GetAppIconName(AppManifest app)
    {
        if (string.Equals(app.Id, FileExplorerAppId, StringComparison.Ordinal))
        {
            return "folder";
        }

        if (string.Equals(app.Id, SettingsAppId, StringComparison.Ordinal))
        {
            return "settings";
        }

        return app.Presentation.Category.ToLowerInvariant() switch
        {
            "developer" => "code-2",
            "games" => "gamepad-2",
            "graphics" => "image",
            "internet" => "globe-2",
            "system" => "monitor-cog",
            "utilities" => "wrench",
            _ => "app-window"
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        if (_preferencesSubscribed)
        {
            Preferences.Changed -= OnPreferencesChanged;
            _preferencesSubscribed = false;
        }
    }
}
