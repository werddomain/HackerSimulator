using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Apps;
using HackerOs.Simulation.Abstractions.Gateways;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Apps.Settings;

/// <summary>
/// Settings &gt; Installed Apps (ADR 0048 section 5): one row per catalog app with its install state,
/// an Uninstall button with a confirmation listing the dependents, and Reinstall where the user may
/// change the scope that uninstalled it. System apps get a badge and no button.
/// </summary>
/// <remarks>
/// Uses only the public SDK (ADR 0055, ADR 0056 section 3): <see cref="IAppManagementGateway"/>
/// (capability <c>apps.manage</c>) for the list and the changes, <see cref="IAppCatalogGateway.Changed"/>
/// to refresh after any <c>apps.config</c> change, and <see cref="HackUser"/> to know whether the
/// "for all users" scope is offered. A machine-scope change makes the OS ask an administrator to
/// authenticate in its own window; this panel never sees a password.
/// </remarks>
public partial class InstalledAppsPanel : IDisposable
{
    private IReadOnlyList<HackAppInstallInfo>? _apps;
    private HackAppInstallInfo? _pending;
    private HackInstallScope _scope = HackInstallScope.User;
    private IAppCatalogGateway? _catalog;
    private bool _isBusy;
    private string? _message;

    /// <summary>
    /// Gets the Settings window's execution context, cascaded by the window host. The panel derives
    /// from <c>HackComponentBase</c> so its lifecycle and event handlers run inside the app's ambient
    /// frame (ADR 0067: otherwise their time would be charged to the compositor).
    /// </summary>
    private IAppExecutionContext Context => AppContext
        ?? throw new InvalidOperationException("This panel must be rendered inside the Settings window.");

    /// <summary>Window apps, sorted by name. Services are managed from System Monitor's Services tab.</summary>
    private IReadOnlyList<HackAppInstallInfo> Applications => [.. (_apps ?? [])
        .Where(static entry => entry.App.Kind == AppKind.Window)];

    /// <summary>Terminal commands, sorted by name; all are system apps on the Ecosystem host.</summary>
    private IReadOnlyList<HackAppInstallInfo> Commands => [.. (_apps ?? [])
        .Where(static entry => entry.App.Kind == AppKind.Terminal)];

    /// <summary>Only an administrator is offered "for all users" (ADR 0048 section 3); the OS still asks for their password.</summary>
    private bool IsAdministrator => HackUser.Describe(Context).IsAdministrator;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        _catalog = Context.Catalog;
        _catalog.Changed += OnCatalogChanged;
        await ReloadAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_catalog is not null)
        {
            _catalog.Changed -= OnCatalogChanged;
        }
    }

    private void OnCatalogChanged(object? sender, EventArgs e) => _ = InvokeAsync(async () =>
    {
        await ReloadAsync();
        StateHasChanged();
    });

    private async Task ReloadAsync()
    {
        try
        {
            _apps = await Context.AppManagement.GetAllAsync(Context.CancellationToken);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or AppGatewayAccessDeniedException)
        {
            _apps = [];
            _message = "Settings is not allowed to manage apps (capability apps.manage).";
        }
    }

    private string NameOf(string appId) =>
        _apps?.FirstOrDefault(entry => entry.App.Id == appId)?.App.Name ?? appId;

    /// <summary>The web sites of an uninstall closure: their browser database is deleted with them (ADR 0088 §4).</summary>
    private IEnumerable<string> WebSitesIn(IReadOnlyList<string> closure) =>
        closure.Where(id => _apps?.FirstOrDefault(entry => entry.App.Id == id)?.App.Kind == HackerOs.App.Abstractions.AppKind.Web);

    private static string RowClass(HackAppInstallInfo entry) =>
        entry.App.IsSystemApp ? "is-system"
        : entry.State == HackAppInstallState.Installed ? "is-installed"
        : "is-uninstalled";

    private string ScopeClass(HackInstallScope scope) =>
        scope == _scope ? "scope-option is-selected" : "scope-option";

    private void AskUninstall(HackAppInstallInfo entry)
    {
        _message = null;
        _pending = entry;
        _scope = HackInstallScope.User;
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
        if (_pending is not { } entry)
        {
            return;
        }

        HackInstallScope scope = IsAdministrator ? _scope : HackInstallScope.User;
        bool done = await RunAsync(() => Context.AppManagement.UninstallAsync(entry.App.Id, scope, Context.CancellationToken));
        if (done)
        {
            _pending = null;
        }
    }

    private Task ReinstallAsync(HackAppInstallInfo entry)
    {
        _message = null;
        HackInstallScope scope = entry.State == HackAppInstallState.UninstalledForMachine
            ? HackInstallScope.Machine
            : HackInstallScope.User;
        return RunAsync(() => Context.AppManagement.ReinstallAsync(entry.App.Id, scope, Context.CancellationToken));
    }

    /// <summary>Runs one install change and turns its outcome into a message; returns whether it succeeded.</summary>
    private async Task<bool> RunAsync(Func<ValueTask<IReadOnlyList<string>>> change)
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
            await change();
            await ReloadAsync();
            return true;
        }
        catch (AppGatewayAccessDeniedException)
        {
            _message = "Settings is not allowed to manage apps (capability apps.manage).";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            _message = "No administrator authenticated: /etc/hackeros/apps.config was not changed.";
            return false;
        }
        catch (InvalidOperationException exception)
        {
            _message = exception.Message;
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
}
