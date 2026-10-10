using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Commands.Kill;

/// <summary>
/// Implements the <c>kill</c> terminal command (`P4-W5-CMD-004`).
/// Terminates a process by PID.
/// </summary>
public sealed class KillCommand : TerminalAppBase
{
    public KillCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        bool forceKill = context.Arguments.Any(a => a is "-9" or "-KILL" or "-SIGKILL");
        var pids = context.Arguments.Where(a => !a.StartsWith('-')).ToList();
        if (pids.Count == 0)
        {
            context.StandardError.WriteLine("kill: usage: kill [-9] <pid...>");
            return 1;
        }

        int status = 0;
        foreach (var pidStr in pids)
        {
            if (!long.TryParse(pidStr, out var pidVal))
            {
                context.StandardError.WriteLine($"kill: illegal process id: {pidStr}");
                status = 1;
                continue;
            }

            var pid = ProcessId.FromInt64(pidVal);
            try
            {
                if (forceKill)
                {
                    context.App.Processes.Kill(pid);
                }
                else
                {
                    await context.App.Processes.StopAsync(pid, TimeSpan.FromSeconds(5), cancellationToken: cancellationToken);
                }
            }
            catch (KeyNotFoundException)
            {
                context.StandardError.WriteLine($"kill: ({pidVal}) - No such process");
                status = 1;
            }
            catch (AppGatewayAccessDeniedException exception)
            {
                context.StandardError.WriteLine($"kill: ({pidVal}) - {exception.Message}");
                status = 1;
            }
        }

        return status;
    }
}
