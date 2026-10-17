using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Administration;
using HackerOs.AppSdk.Apps;
using HackerOs.AppSdk.ServiceProcess;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Apps.Settings;

/// <summary>
/// Settings &gt; Application (ADR 0096, section 4): the page of one installed app, shown for
/// <c>ShowAppSettingsIntent</c> when the app has no settings page of its own.
/// </summary>
/// <remarks>
/// Uses only the public SDK (ADR 0055): <see cref="HackApps"/> for the identity, declared
/// capabilities and file handlers; <see cref="HackPermissionAdministration"/> for the state of the
/// dangerous ones; <see cref="HackServiceController"/> (capabilities <c>process.list</c> and
/// <c>services.manage</c>) for a service's state and start mode; <see cref="InstalledAppsPanel"/>
/// for uninstalling.
/// </remarks>
public sealed partial class AppDetailsPanel
{
    private HackAppInfo? _app;
    private HackAppPermissionInfo? _permissions;
    private HackServiceController? _service;
    private string? _serviceMessage;
    private bool _loaded;
    private bool _isBusy;
    private string? _shownAppId;

    /// <summary>Gets or sets the id of the app whose page is shown.</summary>
    [Parameter]
    [EditorRequired]
    public string AppId { get; set; } = string.Empty;

    private IAppExecutionContext Context => AppContext
        ?? throw new InvalidOperationException("This panel must be rendered inside the Settings window.");

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (_shownAppId == AppId)
        {
            return;
        }

        _shownAppId = AppId;
        _loaded = false;
        _app = null;
        _permissions = null;
        _service = null;
        _serviceMessage = null;
        IReadOnlyList<HackAppInfo> installed = await HackApps.GetInstalledAsync(Context.CancellationToken);
        _app = installed.FirstOrDefault(app => string.Equals(app.Id, AppId, StringComparison.Ordinal));
        _loaded = true;
        if (_app is null)
        {
            return;
        }

        try
        {
            IReadOnlyList<HackAppPermissionInfo> overview = await HackPermissionAdministration.GetOverviewAsync(Context, Context.CancellationToken);
            _permissions = overview.FirstOrDefault(entry => entry.AppId == AppId);
        }
        catch (Exception exception) when (exception is NotSupportedException or UnauthorizedAccessException)
        {
            // A host without the administration services: declared capabilities are still shown.
        }

        if (_app.Kind == AppKind.Service)
        {
            await LoadServiceAsync();
        }
    }

    private async Task LoadServiceAsync()
    {
        try
        {
            HackServiceController[] services = await HackServiceController.GetServicesAsync(Context.CancellationToken);
            _service = services.FirstOrDefault(service => service.ServiceName == AppId);
            _serviceMessage = _service is null ? "The service is not available for this user." : null;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            _serviceMessage = $"The service state cannot be read: {exception.Message}";
        }
    }

    private Task StartServiceAsync() => RunServiceAsync(service => service.StartAsync(Context.CancellationToken), "Service started.");

    private Task StopServiceAsync() => RunServiceAsync(service => service.StopAsync(Context.CancellationToken), "Service stopped.");

    private Task ChangeStartModeAsync(ChangeEventArgs e) =>
        Enum.TryParse(e.Value?.ToString(), out ServiceStartMode mode)
            ? RunServiceAsync(service => service.SetStartTypeAsync(mode, Context.CancellationToken), $"Start mode: {mode}.")
            : Task.CompletedTask;

    private async Task RunServiceAsync(Func<HackServiceController, Task> action, string success)
    {
        if (_service is null || _isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            await action(_service);
            _serviceMessage = success;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException)
        {
            _serviceMessage = exception.Message;
        }
        finally
        {
            _isBusy = false;
        }
    }

    private static string KindLabel(AppKind kind) => kind switch
    {
        AppKind.Window => "window app",
        AppKind.Terminal => "command",
        AppKind.Service => "service",
        AppKind.Web => "web site",
        _ => kind.ToString()
    };

    private static string StateLabel(HackPermissionState state) => state switch
    {
        HackPermissionState.System => "system",
        HackPermissionState.Allowed => "allowed",
        HackPermissionState.Pending => "pending",
        _ => $"refused ({state})"
    };

    /// <summary>A handler as a reader sees it: <c>x-scheme-handler/https</c> becomes "https: links".</summary>
    private static string DescribeHandler(HackAppFileHandler handler)
    {
        if (handler.MediaType is { } mediaType
            && mediaType.StartsWith(AppIntentIds.SchemeHandlerMediaTypePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return $"{mediaType[AppIntentIds.SchemeHandlerMediaTypePrefix.Length..]}: links";
        }

        string extensions = string.Join(" ", handler.Extensions);
        return handler.MediaType is null ? extensions
            : extensions.Length == 0 ? handler.MediaType
            : $"{handler.MediaType} ({extensions})";
    }
}
