using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Commands.Kill;

/// <summary>
/// Implements the <c>kill</c> terminal command (`P4-W5-CMD-004`).
/// Terminates a process by PID.
/// </summary>
public sealed class KillCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.kill",
        Name = "kill",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Terminate process",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Kill.dll", "HackerOs.Commands.Kill.KillCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.ProcessManage],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("kill", [], "kill [-9] <pid...>"),
        SingleInstancePerUser = false
    };

    public KillCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

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

            try
            {
                var pid = ProcessId.FromInt64(pidVal);
                var record = context.App.Processes.Kill(pid);
                if (record is null)
                {
                    context.StandardError.WriteLine($"kill: ({pidVal}) - No such process");
                    status = 1;
                }
            }
            catch
            {
                status = 1;
            }
        }

        return status;
    }
}
