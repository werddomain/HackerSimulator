using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Apps;
using HackerOs.Simulation.Abstractions.Sessions;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Apps.Settings;

/// <summary>
/// Settings &gt; Installed Apps (ADR 0048 section 5): one row per catalog app with its install state,
/// an Uninstall button with a confirmation listing the dependents, and Reinstall where the user may
/// change the scope that uninstalled it. System apps get a badge and no button.
/// </summary>
/// <remarks>
/// Writes go through <see cref="IAppInstallationService"/>; a machine-scope write uses this app's
/// own filesystem gateway, so a denied write raises the administrator approval prompt of ADR 0042
/// and the user simply tries again once approved, as in the Appearance tab.
/// </remarks>
public partial class InstalledAppsPanel : ComponentBase, IDisposable
{
    private AppManifest? _pending;
    private IReadOnlyList<string> _pendingClosure = [];
    private AppInstallationScope _scope = AppInstallationScope.User;
    private bool _isBusy;
    private string? _message;

    /// <summary>Gets or sets the Settings app's execution context (filesystem gateway and cancellation).</summary>
    [Parameter, EditorRequired]
    public IAppExecutionContext AppContext { get; set; } = null!;

    [Inject]
    private AppCatalog Catalog { get; set; } = null!;

    [Inject]
    private IAppInstallationService Installation { get; set; } = null!;

    [Inject]
    private ISessionService Session { get; set; } = null!;

    /// <summary>Window apps, sorted by name. Services are managed from System Monitor's Services tab.</summary>
    private IReadOnlyList<AppManifest> Applications => [.. Catalog.Manifests.Values
        .Where(static manifest => manifest.Kind == AppKind.Window)
        .OrderBy(static manifest => manifest.Name, StringComparer.OrdinalIgnoreCase)];

    /// <summary>Terminal commands, sorted by name; all are system apps on the Ecosystem host.</summary>
    private IReadOnlyList<AppManifest> Commands => [.. Catalog.Manifests.Values
        .Where(static manifest => manifest.Kind == AppKind.Terminal)
        .OrderBy(static manifest => manifest.Name, StringComparer.OrdinalIgnoreCase)];

    /// <summary>Only an administrator may choose "for all users" (ADR 0048 section 3).</summary>
    private bool IsAdministrator => Session.CurrentPrincipal?.HasAuthority(AppAuthority.Administrator) == true;

    /// <inheritdoc />
    protected override void OnInitialized() => Installation.Changed += OnInstallationChanged;

    /// <inheritdoc />
    public void Dispose() => Installation.Changed -= OnInstallationChanged;

    private void OnInstallationChanged(object? sender, EventArgs e) => _ = InvokeAsync(StateHasChanged);

    private AppInstallationState StateOf(string appId) => Installation.GetState(appId);

    private string NameOf(string appId) =>
        Catalog.Manifests.TryGetValue(appId, out AppManifest? manifest) ? manifest.Name : appId;

    private static string RowClass(AppInstallationState state) => state switch
    {
        AppInstallationState.System => "is-system",
        AppInstallationState.Installed => "is-installed",
        _ => "is-uninstalled"
    };

    private string ScopeClass(AppInstallationScope scope) =>
        scope == _scope ? "scope-option is-selected" : "scope-option";

    private void AskUninstall(AppManifest app)
    {
        _message = null;
        _pending = app;
        _pendingClosure = Installation.GetUninstallClosure(app.Id);
        _scope = AppInstallationScope.User;
    }

    private void CancelUninstall()
    {
        if (!_isBusy)
        {
            _pending = null;
        }
    }

    private async Task ConfirmUninstallAsync()
    {
        if (_pending is not AppManifest app)
        {
            return;
        }

        AppInstallationScope scope = IsAdministrator ? _scope : AppInstallationScope.User;
        bool done = await RunAsync(() => Installation.UninstallAsync(
            app.Id, scope, scope == AppInstallationScope.Machine ? AppContext.FileSystem : null, AppContext.CancellationToken));
        if (done)
        {
            _pending = null;
        }
    }

    private Task ReinstallAsync(AppManifest app, AppInstallationState state)
    {
        _message = null;
        AppInstallationScope scope = state == AppInstallationState.UninstalledForMachine
            ? AppInstallationScope.Machine
            : AppInstallationScope.User;
        return RunAsync(() => Installation.ReinstallAsync(
            app.Id, scope, scope == AppInstallationScope.Machine ? AppContext.FileSystem : null, AppContext.CancellationToken));
    }

    /// <summary>Runs one install change and turns its outcome into a message; returns whether it succeeded.</summary>
    private async Task<bool> RunAsync(Func<ValueTask<AppInstallationResult>> change)
    {
        if (_isBusy)
        {
            return false;
        }

        _isBusy = true;
        _message = null;
        StateHasChanged();
        try
        {
            AppInstallationResult result = await change();
            if (!result.Success)
            {
                _message = string.Join(" ", result.Errors.Select(Describe));
            }

            return result.Success;
        }
        catch (UnauthorizedAccessException)
        {
            _message = "Administrator approval is required to change /etc/hackeros/apps.config. Approve the prompt, then try again.";
            return false;
        }
        catch (IOException exception)
        {
            _message = $"Could not write apps.config: {exception.Message}";
            return false;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private string Describe(string error)
    {
        int separator = error.IndexOf(':');
        string code = separator < 0 ? error : error[..separator];
        string appName = separator < 0 ? string.Empty : NameOf(error[(separator + 1)..]);
        return code switch
        {
            "apps.app.system" => $"{appName} is a system app and cannot be uninstalled.",
            "apps.closure.system-app" => $"The system app {appName} needs it, so it cannot be uninstalled.",
            "apps.uninstalled-by-administrator" => $"{appName} was uninstalled by the administrator for every user.",
            "apps.session.required" => "Sign in to change installed apps.",
            _ => error
        };
    }
}
