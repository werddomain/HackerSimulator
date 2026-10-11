using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Diagnostics;
using HackerOs.AppSdk.IO;
using HackerOs.AppSdk.ServiceProcess;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Apps.SystemMonitor.Tabs;

/// <summary>
/// The Services tab (ADR 0058, section 2): every installed service with its status, start type,
/// and start/stop actions, on <see cref="HackServiceController"/> (ADR 0056, section 5).
/// </summary>
/// <remarks>
/// The list and the start types are read when the tab opens and after each action; the status
/// follows the processes the window reads every second, so a service that crashes shows as
/// stopped without reloading the list.
/// </remarks>
public partial class ServicesTab
{
    private IReadOnlyList<HackServiceController> _services = [];
    private bool _busy;
    private string? _message;

    /// <summary>Gets or sets the processes of the last refresh, by PID, for the live status.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyDictionary<long, HackProcess> Processes { get; set; } = new Dictionary<long, HackProcess>();

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => LoadAsync();

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

    /// <summary>The status from the processes read this second, more recent than the list.</summary>
    private HackServiceControllerStatus LiveStatus(HackServiceController service) =>
        FindProcess(service)?.State switch
        {
            null or HackProcessState.Dead or HackProcessState.Zombie => HackServiceControllerStatus.Stopped,
            HackProcessState.Idle or HackProcessState.Sleeping => HackServiceControllerStatus.StartPending,
            HackProcessState.Stopping => HackServiceControllerStatus.StopPending,
            _ => HackServiceControllerStatus.Running
        };

    private long? LivePid(HackServiceController service) => FindProcess(service)?.Id;

    private HackProcess? FindProcess(HackServiceController service) =>
        Processes.Values.FirstOrDefault(process => string.Equals(process.AppId, service.ServiceName, StringComparison.Ordinal));

    private static string StatusText(HackServiceControllerStatus status) => status switch
    {
        HackServiceControllerStatus.StartPending => "Starting",
        HackServiceControllerStatus.StopPending => "Stopping",
        _ => status.ToString()
    };
}
