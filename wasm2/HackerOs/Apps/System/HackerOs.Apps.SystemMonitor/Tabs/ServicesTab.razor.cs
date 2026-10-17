using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Diagnostics;
using HackerOs.AppSdk.IO;
using HackerOs.AppSdk.ServiceProcess;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Apps.SystemMonitor.Tabs;

/// <summary>
/// The Services tab (ADR 0058, section 2): every installed service with its state, health, start
/// type, and start/stop actions, on <see cref="HackServiceController"/> (ADR 0056, section 5).
/// </summary>
/// <remarks>
/// The list and the start types are read when the tab opens and after each action. The state
/// (ADR 0097: active, activating, failed with its reason, the health and the status text the
/// service reported) is read again from the service manager each time the window refreshes its
/// processes, every second, so a service that crashes shows as failed without reloading the list.
/// </remarks>
public partial class ServicesTab
{
    private IReadOnlyList<HackServiceController> _services = [];
    private bool _busy;
    private bool _refreshing;
    private bool _hasManagerState = true;
    private string? _message;

    /// <summary>Gets or sets the processes of the last refresh, by PID, for the live status.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyDictionary<long, HackProcess> Processes { get; set; } = new Dictionary<long, HackProcess>();

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => LoadAsync();

    /// <inheritdoc />
    /// <remarks>The processes changed (the window's one-second refresh): read the service states again, cheaply.</remarks>
    protected override async Task OnParametersSetAsync()
    {
        if (_busy || _refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            foreach (HackServiceController service in _services)
            {
                _hasManagerState = await service.RefreshStatusAsync();
                if (!_hasManagerState)
                {
                    break;
                }
            }
        }
        catch (Exception exception) when (exception is HackIOException or NotSupportedException)
        {
            _hasManagerState = false;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async Task LoadAsync()
    {
        _busy = true;
        try
        {
            _services = await HackServiceController.GetServicesAsync();
        }
        catch (NotSupportedException)
        {
            // A host without the catalog gateway (a test harness): no service to show.
            _services = [];
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RunAsync(HackServiceController service, Func<CancellationToken, Task> action, string verb)
    {
        _busy = true;
        try
        {
            await action(CancellationToken.None);
            _message = null;
        }
        catch (HackUnauthorizedAccessException)
        {
            _message = $"Cannot {verb} {service.DisplayName}: this needs services.manage.";
        }
        catch (InvalidOperationException exception)
        {
            _message = exception.Message;
        }
        finally
        {
            _busy = false;
        }

        await LoadAsync();
    }

    private async Task SetStartTypeAsync(HackServiceController service, object? value)
    {
        if (value is string text && Enum.TryParse(text, out ServiceStartMode mode))
        {
            await RunAsync(service, cancellationToken => service.SetStartTypeAsync(mode, cancellationToken), "configure");
        }
    }

    /// <summary>
    /// The state to show: the service manager's (refreshed every second); on a host that keeps none,
    /// derived from the processes read this second, where nothing is ever failed.
    /// </summary>
    private HackServiceActiveState LiveState(HackServiceController service) =>
        _hasManagerState
            ? service.ActiveState
            : FindProcess(service)?.State switch
            {
                null or HackProcessState.Dead or HackProcessState.Zombie => HackServiceActiveState.Inactive,
                HackProcessState.Idle or HackProcessState.Sleeping => HackServiceActiveState.Activating,
                HackProcessState.Stopping => HackServiceActiveState.Deactivating,
                _ => HackServiceActiveState.Active
            };

    private long? LivePid(HackServiceController service) =>
        _hasManagerState && service.ActiveState is HackServiceActiveState.Inactive or HackServiceActiveState.Failed
            ? null
            : FindProcess(service)?.Id ?? service.ProcessId;

    private HackProcess? FindProcess(HackServiceController service) =>
        Processes.Values.FirstOrDefault(process => string.Equals(process.AppId, service.ServiceName, StringComparison.Ordinal));

    private static string StateText(HackServiceController service, HackServiceActiveState state) => state switch
    {
        HackServiceActiveState.Active => "Running",
        HackServiceActiveState.Activating => "Starting",
        HackServiceActiveState.Deactivating => "Stopping",
        HackServiceActiveState.Failed => $"Failed ({HackServiceStates.ToSystemdName(service.Result)})",
        _ => "Stopped"
    };

    private static string StateTitle(HackServiceController service, HackServiceActiveState state)
    {
        string text = $"{HackServiceStates.ToSystemdName(state)}";
        if (state == HackServiceActiveState.Failed)
        {
            text += $" (Result: {HackServiceStates.ToSystemdName(service.Result)}{(service.ExitCode is { } code ? $", status={code}" : string.Empty)})";
        }

        return service.ActiveStateChangedAt is { } since ? $"{text} since {since:yyyy-MM-dd HH:mm:ss} UTC" : text;
    }

    private static string HealthText(HackServiceHealth health) => health switch
    {
        HackServiceHealth.Healthy => "Healthy",
        HackServiceHealth.Degraded => "Degraded",
        HackServiceHealth.Unhealthy => "Unhealthy",
        _ => "Unknown"
    };
}
