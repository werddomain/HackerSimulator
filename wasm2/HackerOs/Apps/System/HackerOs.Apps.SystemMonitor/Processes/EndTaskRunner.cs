using HackerOs.AppSdk.IO;

namespace HackerOs.Apps.SystemMonitor.Processes;

/// <summary>One process that "End task" could not end, and why.</summary>
/// <param name="Pid">Process id.</param>
/// <param name="Name">Process name.</param>
/// <param name="Reason">Why it failed, for the user.</param>
public sealed record EndTaskFailure(long Pid, string Name, string Reason);

/// <summary>What one "End task" did (ADR 0058, section 2).</summary>
/// <param name="Ended">The processes ended (or already gone).</param>
/// <param name="Failures">The processes left running, with the reason.</param>
public sealed record EndTaskReport(IReadOnlyList<ProcessRow> Ended, IReadOnlyList<EndTaskFailure> Failures);

/// <summary>
/// Ends a selection of processes one by one. A refusal for one process (<c>process.manage</c>
/// missing, which only matters for another app's process) is recorded and the others still end,
/// like <c>kill 12 13 14</c> reporting each PID it could not signal.
/// </summary>
public static class EndTaskRunner
{
    /// <summary>Ends every row with <paramref name="kill"/>.</summary>
    /// <param name="rows">The selected processes.</param>
    /// <param name="kill">Ends one process (<c>HackProcess.KillAsync</c>).</param>
    /// <returns>The processes ended and the failures, in the order of <paramref name="rows"/>.</returns>
    public static async Task<EndTaskReport> EndAsync(IReadOnlyList<ProcessRow> rows, Func<ProcessRow, Task> kill)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(kill);
        List<ProcessRow> ended = [];
        List<EndTaskFailure> failures = [];

        foreach (ProcessRow row in rows)
        {
            try
            {
                await kill(row);
                ended.Add(row);
            }
            catch (HackUnauthorizedAccessException)
            {
                failures.Add(new EndTaskFailure(row.Pid, row.Name, "Access denied: ending another app's process needs process.manage."));
            }
            catch (InvalidOperationException)
            {
                // Exited between the refresh and the click: the goal is reached.
                ended.Add(row);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(new EndTaskFailure(row.Pid, row.Name, exception.Message));
            }
        }

        return new EndTaskReport(ended, failures);
    }
}
