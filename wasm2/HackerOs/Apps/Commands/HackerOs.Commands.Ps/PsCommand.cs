using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Commands.Ps;

/// <summary>
/// Implements the <c>ps</c> terminal command (`P4-W5-CMD-004`).
/// Lists running processes in the system.
/// </summary>
public sealed class PsCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.ps",
        Name = "ps",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Report current processes",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Ps.dll", "HackerOs.Commands.Ps.PsCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.ProcessList],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("ps", [], "ps [-a] [-u]"),
        SingleInstancePerUser = false
    };

    public PsCommand(AppManifest manifest) : base(manifest) { }

    public override ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        bool allUsers = false;
        bool userFormat = false;
        foreach (var arg in context.Arguments)
        {
            if (arg == "-a") allUsers = true;
            else if (arg == "-u") userFormat = true;
        }

        IReadOnlyList<ProcessRecord> processes;
        try
        {
            processes = context.App.Processes.ListProcesses();
        }
        catch (Exception exception)
        {
            context.StandardError.WriteLine($"ps: {exception.Message}");
            return ValueTask.FromResult(1);
        }

        DateTimeOffset now = context.App.Clock.UtcNow;
        IEnumerable<ProcessRecord> visible = allUsers
            ? processes
            : processes.Where(p => p.SessionId == context.App.SessionId);

        context.StandardOutput.WriteLine(userFormat ? "USER       PID STAT     TIME CMD" : "  PID STAT     TIME CMD");

        foreach (var proc in visible.OrderBy(p => p.Pid.Value))
        {
            string pid = proc.Pid.Value.ToString().PadLeft(5);
            string stat = proc.State.ToString().PadRight(8);
            TimeSpan elapsed = proc.StartedAtUtc is { } started ? now - started : TimeSpan.Zero;
            string time = elapsed.ToString(@"hh\:mm\:ss");

            context.StandardOutput.WriteLine(userFormat
                ? $"{proc.UserId,-10} {pid} {stat} {time} {proc.AppId}"
                : $"{pid} {stat} {time} {proc.AppId}");
        }

        return ValueTask.FromResult(0);
    }
}
