using System.Globalization;
using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.AppSdk.Globalization;
using Microsoft.Extensions.Localization;

namespace HackerOs.Commands.Ps;

/// <summary>
/// Implements the <c>ps</c> terminal command (`P4-W5-CMD-004`).
/// Lists running processes by reading <c>/proc</c>, like procps on Linux (ADR 0046): one
/// <c>/proc/&lt;pid&gt;/status</c> file per process, and the <c>/proc/&lt;pid&gt;</c> directory's
/// creation time as the process start time. Which processes appear is decided by <c>/proc</c>
/// itself (own process always, others with <c>process.list</c>); <c>kill</c> keeps using the
/// process gateway for actions.
/// </summary>
public sealed class PsCommand : TerminalAppBase
{
    private static readonly VirtualPath ProcRoot = VirtualPath.Parse("/proc");

    /// <summary>Initializes the command with its manifest.</summary>
    public PsCommand(AppManifest manifest) : base(manifest) { }

    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(
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

        FileSystemResult<FileSystemDirectorySnapshot> listing =
            await context.App.FileSystem.EnumerateAsync(new FileSystemEnumerateRequest(ProcRoot), cancellationToken);
        if (!listing.Succeeded || listing.Value is null)
        {
            // The table below stays English for scripts (ADR 0104, section 5); only this message is translated.
            context.StandardError.WriteLine(context.App.GetLocalizer<Strings>()["CannotReadProc", listing.Error?.Code.ToString() ?? string.Empty]);
            return 1;
        }

        List<ProcEntry> processes = [];
        foreach (FileSystemDirectoryItem item in listing.Value.Entries)
        {
            // Only the numeric directories are processes; self, meminfo, uptime... are skipped.
            if (item.Metadata.Kind != FileSystemEntryKind.Directory
                || !long.TryParse(item.Name.Value, NumberStyles.None, CultureInfo.InvariantCulture, out long pid))
            {
                continue;
            }

            // A process can exit between the listing and this read; procps skips it the same way.
            IReadOnlyDictionary<string, string>? status = await ReadStatusAsync(context, pid, cancellationToken);
            if (status is not null)
            {
                processes.Add(new ProcEntry(pid, item.Metadata.Timestamps.CreatedAtUtc, status));
            }
        }

        DateTimeOffset now = context.App.Clock.UtcNow;
        string currentSession = context.App.SessionId.ToString();
        IEnumerable<ProcEntry> visible = allUsers
            ? processes
            : processes.Where(p => p.Field("Session") == currentSession);

        context.StandardOutput.WriteLine(userFormat ? "USER       PID STAT     TIME CMD" : "  PID STAT     TIME CMD");

        foreach (ProcEntry proc in visible.OrderBy(p => p.Pid))
        {
            string pid = proc.Pid.ToString(CultureInfo.InvariantCulture).PadLeft(5);
            // "R (running)" -> "R", the single-letter STAT code procps prints.
            string stat = proc.Field("State").Split(' ')[0].PadRight(8);
            TimeSpan elapsed = now > proc.StartedAtUtc ? now - proc.StartedAtUtc : TimeSpan.Zero;
            string time = elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
            // A background task (ADR 0128) shows as <app command>:<task>, like a named thread of its app.
            string command = proc.Status.TryGetValue("Job", out string? job) ? job : proc.Field("AppId");

            context.StandardOutput.WriteLine(userFormat
                ? $"{proc.Field("Uid"),-10} {pid} {stat} {time} {command}"
                : $"{pid} {stat} {time} {command}");
        }

        return 0;
    }

    /// <summary>Reads and parses <c>/proc/&lt;pid&gt;/status</c>, or returns null if it vanished.</summary>
    private static async ValueTask<IReadOnlyDictionary<string, string>?> ReadStatusAsync(
        TerminalExecutionContext context,
        long pid,
        CancellationToken cancellationToken)
    {
        VirtualPath path = VirtualPath.Parse($"/proc/{pid.ToString(CultureInfo.InvariantCulture)}/status");
        FileSystemResult<FileSystemContentReadHandle> read =
            await context.App.FileSystem.ReadAsync(new FileSystemReadRequest(path), cancellationToken);
        if (!read.Succeeded || read.Value is null)
        {
            return null;
        }

        await using FileSystemContentReadHandle handle = read.Value;
        using StreamReader reader = new(handle.Content, Encoding.UTF8, leaveOpen: true);
        string text = await reader.ReadToEndAsync(cancellationToken);
        return ParseStatus(text);
    }

    /// <summary>Parses the <c>Key:\tvalue</c> lines of a <c>/proc/&lt;pid&gt;/status</c> file.</summary>
    internal static IReadOnlyDictionary<string, string> ParseStatus(string text)
    {
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = line.IndexOf(':');
            if (separator > 0)
            {
                fields[line[..separator]] = line[(separator + 1)..].Trim();
            }
        }

        return fields;
    }

    private sealed record ProcEntry(long Pid, DateTimeOffset StartedAtUtc, IReadOnlyDictionary<string, string> Status)
    {
        public string Field(string key) => Status.TryGetValue(key, out string? value) ? value : "?";
    }
}
