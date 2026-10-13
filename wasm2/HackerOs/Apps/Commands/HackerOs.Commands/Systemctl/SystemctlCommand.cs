using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.ServiceProcess;

namespace HackerOs.Commands.Systemctl;

/// <summary>
/// <c>systemctl [list-units|status|start|stop|restart|enable|disable|mask|is-active|is-enabled] [UNIT...]</c>
/// (ADR 0064): the HackerOS services (the catalog's <c>Service</c> apps, through
/// <see cref="HackServiceController"/>) shown and driven as systemd units. A unit is named by its
/// app id or short name, with or without <c>.service</c> (<c>sync</c>, <c>sync.service</c>,
/// <c>org.hackeros.sync</c>). Start types map to systemd states: automatic = <c>enabled</c>,
/// manual = <c>disabled</c>, disabled = <c>masked</c>. Changing another app's service needs
/// <c>services.manage</c>. Also answers as <c>service NAME start</c>.
/// </summary>
public sealed class SystemctlCommand(AppManifest manifest) : TerminalAppBase(manifest)
{
    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        List<string> args = [.. context.Arguments.Where(a => a is not ("--no-pager" or "--user" or "--system" or "-q" or "--quiet" or "--now" or "--all" or "-a"))];
        IReadOnlyList<string> argv = context.App.Environment.GetCommandLineArgs();
        if (argv.Count > 0 && argv[0] == "service" && args.Count >= 2)
        {
            // `service NAME ACTION` is `systemctl ACTION NAME`.
            args = [args[1], args[0], .. args.Skip(2)];
        }

        string verb = args.Count == 0 ? "list-units" : args[0];
        List<string> units = [.. args.Skip(1)];
        HackServiceController[] services;
        try
        {
            services = await HackServiceController.GetServicesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or NotSupportedException or IOException)
        {
            await context.StandardError.WriteLineAsync($"Failed to connect to bus: {exception.Message}");
            return 1;
        }

        if (verb is "list-units" or "list-unit-files" or "list")
        {
            return await ListAsync(context, services, verb == "list-unit-files");
        }

        if (units.Count == 0)
        {
            await context.StandardError.WriteLineAsync($"Too few arguments.");
            return 1;
        }

        int status = 0;
        foreach (string unit in units)
        {
            HackServiceController? service = Find(services, unit);
            if (service is null)
            {
                await context.StandardError.WriteLineAsync(verb is "status" or "is-active" or "is-enabled"
                    ? $"Unit {UnitName(unit)} could not be found."
                    : $"Failed to {verb} {UnitName(unit)}: Unit {UnitName(unit)} not found.");
                status = verb == "is-active" ? 3 : verb is "status" ? 4 : 5;
                continue;
            }

            try
            {
                status = Math.Max(status, await RunAsync(context, verb, service, cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                await context.StandardError.WriteLineAsync($"Failed to {verb} {UnitName(service)}: Access denied");
                await context.StandardError.WriteLineAsync("See system logs and 'systemctl status' for details. (Needs the services.manage capability; try sudo.)");
                status = Math.Max(status, 4);
            }
            catch (InvalidOperationException exception)
            {
                await context.StandardError.WriteLineAsync($"Failed to {verb} {UnitName(service)}: {exception.Message}");
                status = Math.Max(status, 1);
            }
        }

        return status;
    }

    private static async Task<int> RunAsync(TerminalExecutionContext context, string verb, HackServiceController service, CancellationToken cancellationToken)
    {
        switch (verb)
        {
            case "start":
                await service.StartAsync(cancellationToken);
                return 0;
            case "stop":
                await service.StopAsync(cancellationToken);
                return 0;
            case "restart" or "reload" or "try-restart":
                await service.StopAsync(cancellationToken);
                await service.StartAsync(cancellationToken);
                return 0;
            case "enable":
                await service.SetStartTypeAsync(ServiceStartMode.Automatic, cancellationToken);
                await context.StandardOutput.WriteLineAsync($"Created symlink /etc/systemd/system/multi-user.target.wants/{UnitName(service)} → /usr/lib/systemd/system/{UnitName(service)}.");
                return 0;
            case "disable":
                await service.SetStartTypeAsync(ServiceStartMode.Manual, cancellationToken);
                await context.StandardOutput.WriteLineAsync($"Removed \"/etc/systemd/system/multi-user.target.wants/{UnitName(service)}\".");
                return 0;
            case "mask":
                await service.SetStartTypeAsync(ServiceStartMode.Disabled, cancellationToken);
                await context.StandardOutput.WriteLineAsync($"Created symlink /etc/systemd/system/{UnitName(service)} → /dev/null.");
                return 0;
            case "unmask":
                await service.SetStartTypeAsync(ServiceStartMode.Manual, cancellationToken);
                await context.StandardOutput.WriteLineAsync($"Removed \"/etc/systemd/system/{UnitName(service)}\".");
                return 0;
            case "is-active":
                await context.StandardOutput.WriteLineAsync(Active(service).Sub);
                return service.Status == HackServiceControllerStatus.Running ? 0 : 3;
            case "is-enabled":
                await context.StandardOutput.WriteLineAsync(Enabled(service));
                return service.StartType == ServiceStartMode.Automatic ? 0 : 1;
            case "status":
                (string active, string sub) = Active(service);
                string dot = service.Status == HackServiceControllerStatus.Running ? "\u001b[32m●\u001b[0m" : "○";
                await context.StandardOutput.WriteLineAsync($"{dot} {UnitName(service)} - {service.DisplayName}");
                await context.StandardOutput.WriteLineAsync($"     Loaded: loaded (/usr/lib/systemd/system/{UnitName(service)}; {Enabled(service)}; preset: enabled)");
                await context.StandardOutput.WriteLineAsync($"     Active: {active} ({sub})");
                if (service.ProcessId is { } pid)
                {
                    await context.StandardOutput.WriteLineAsync($"   Main PID: {pid} ({ShortName(service.ServiceName)})");
                }

                await context.StandardOutput.WriteLineAsync($"       Docs: {service.Description}");
                return service.Status == HackServiceControllerStatus.Running ? 0 : 3;
            default:
                await context.StandardError.WriteLineAsync($"Unknown command verb '{verb}'.");
                return 1;
        }
    }

    private static async Task<int> ListAsync(TerminalExecutionContext context, HackServiceController[] services, bool files)
    {
        if (files)
        {
            await context.StandardOutput.WriteLineAsync($"{"UNIT FILE",-40} {"STATE",-9} PRESET");
            foreach (HackServiceController service in services)
            {
                await context.StandardOutput.WriteLineAsync($"{UnitName(service),-40} {Enabled(service),-9} enabled");
            }

            await context.StandardOutput.WriteLineAsync();
            await context.StandardOutput.WriteLineAsync($"{services.Length} unit files listed.");
            return 0;
        }

        await context.StandardOutput.WriteLineAsync($"  {"UNIT",-40} {"LOAD",-6} {"ACTIVE",-8} {"SUB",-8} DESCRIPTION");
        foreach (HackServiceController service in services)
        {
            (string active, string sub) = Active(service);
            await context.StandardOutput.WriteLineAsync($"  {UnitName(service),-40} {"loaded",-6} {active,-8} {sub,-8} {service.DisplayName}");
        }

        await context.StandardOutput.WriteLineAsync();
        await context.StandardOutput.WriteLineAsync("LOAD   = Reflects whether the unit definition was properly loaded.");
        await context.StandardOutput.WriteLineAsync("ACTIVE = The high-level unit activation state, i.e. generalization of SUB.");
        await context.StandardOutput.WriteLineAsync("SUB    = The low-level unit activation state, values depend on unit type.");
        await context.StandardOutput.WriteLineAsync($"{services.Length} loaded units listed.");
        return 0;
    }

    private static (string Active, string Sub) Active(HackServiceController service) => service.Status switch
    {
        HackServiceControllerStatus.Running => ("active", "running"),
        HackServiceControllerStatus.StartPending => ("activating", "start"),
        HackServiceControllerStatus.StopPending => ("deactivating", "stop"),
        _ => ("inactive", "dead")
    };

    private static string Enabled(HackServiceController service) => service.StartType switch
    {
        ServiceStartMode.Automatic => "enabled",
        ServiceStartMode.Manual => "disabled",
        ServiceStartMode.Disabled => "masked",
        _ => "static"
    };

    private static HackServiceController? Find(HackServiceController[] services, string unit)
    {
        string name = unit.EndsWith(".service", StringComparison.Ordinal) ? unit[..^8] : unit;
        return services.FirstOrDefault(s => string.Equals(s.ServiceName, name, StringComparison.OrdinalIgnoreCase))
            ?? services.FirstOrDefault(s => string.Equals(ShortName(s.ServiceName), name, StringComparison.OrdinalIgnoreCase));
    }

    private static string ShortName(string appId) => appId[(appId.LastIndexOf('.') + 1)..];

    private static string UnitName(HackServiceController service) => ShortName(service.ServiceName) + ".service";

    private static string UnitName(string unit) => unit.EndsWith(".service", StringComparison.Ordinal) ? unit : unit + ".service";
}
