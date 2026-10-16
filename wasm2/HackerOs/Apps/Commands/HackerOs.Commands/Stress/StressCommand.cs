using System.Diagnostics;
using System.Globalization;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Diagnostics;

namespace HackerOs.Commands.Stress;

/// <summary>
/// Implements <c>stress</c>, after the Linux load generator: <c>stress -c 1</c> keeps one CPU busy
/// until <c>-t</c> seconds have passed or the process is killed.
/// </summary>
/// <remarks>
/// <para>
/// By default the CPU hog is a tight loop that never gives its thread back, like <c>stress</c>'s own
/// <c>sqrt()</c> loop: the case ADR 0070 exists for. In its worker the heartbeat stops ("Not
/// responding"), <c>kill</c> cannot be delivered, and only terminating the worker stops it
/// (ADR 0069, step 4) — the desktop never freezes.
/// </para>
/// <para>
/// With <c>--yield</c> it is a good citizen: it calls <see cref="HackProcess.CheckpointAsync"/> on
/// every slice, so it stays responsive and stops on <c>SIGTERM</c> (exit 130).
/// </para>
/// </remarks>
public sealed class StressCommand : TerminalAppBase
{
    /// <summary>Initializes the command with its manifest.</summary>
    public StressCommand(AppManifest manifest) : base(manifest)
    {
    }

    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int hogs = 1;
        double? timeout = null;
        bool yieldSlices = false;
        IReadOnlyList<string> args = context.Arguments;
        for (int index = 0; index < args.Count; index++)
        {
            string arg = args[index];
            if (arg is "-c" or "--cpu" && index + 1 < args.Count && int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) && count > 0)
            {
                hogs = count;
                index++;
            }
            else if (arg is "-t" or "--timeout" && index + 1 < args.Count && TryParseSeconds(args[index + 1], out double seconds))
            {
                timeout = seconds;
                index++;
            }
            else if (arg == "--yield")
            {
                yieldSlices = true;
            }
            else
            {
                await context.StandardError.WriteLineAsync($"stress: invalid option '{arg}'");
                await context.StandardError.WriteLineAsync("Usage: stress [-c N] [-t SECONDS] [--yield]");
                return 1;
            }
        }

        long pid = context.App.ProcessId.Value;
        await context.StandardOutput.WriteLineAsync(
            string.Create(CultureInfo.InvariantCulture, $"stress: info: [{pid}] dispatching hogs: {hogs} cpu, 0 io, 0 vm, 0 hdd"));
        await context.StandardOutput.FlushAsync(cancellationToken);

        // One thread per process: N hogs share it, as N processes would share one core.
        Stopwatch elapsed = Stopwatch.StartNew();
        long deadline = timeout is { } limit ? (long)(limit * Stopwatch.Frequency) : long.MaxValue;
        double sink = 0;
        while (elapsed.ElapsedTicks < deadline)
        {
            for (int i = 1; i < 20_000; i++)
            {
                sink += Math.Sqrt(i);
            }

            if (yieldSlices)
            {
                await HackProcess.CheckpointAsync(cancellationToken);
            }
        }

        GC.KeepAlive(sink);
        await context.StandardOutput.WriteLineAsync(
            string.Create(CultureInfo.InvariantCulture, $"stress: info: [{pid}] successful run completed in {elapsed.Elapsed.TotalSeconds:0}s"));
        return 0;
    }

    private static bool TryParseSeconds(string text, out double seconds)
    {
        string number = text.TrimEnd('s');
        return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0;
    }
}
