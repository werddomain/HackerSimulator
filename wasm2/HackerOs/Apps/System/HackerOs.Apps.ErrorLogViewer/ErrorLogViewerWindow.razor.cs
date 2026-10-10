using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Apps.ErrorLogViewer;

/// <summary>
/// Error Log Viewer: reads <c>/var/log/syslog</c> and <c>/var/log/apps/*.log</c> (plus their archives
/// on demand) as ordinary files, parses them with <see cref="LogLineFormat"/>, and follows new lines
/// through directory watches (ADR 0045, section 6). "Truncate" empties the selected active log, which
/// needs write access to <c>/var/log</c> and therefore an administrator approval for ordinary users.
/// </summary>
public partial class ErrorLogViewerWindow
{
    /// <summary>Maximum rows rendered per tab; the newest are shown first.</summary>
    public const int MaxDisplayedRows = 2000;

    private readonly Dictionary<VirtualPath, LogFileContent> _contents = [];
    private readonly List<ITopicChannelSubscription<FileSystemChangeEvent>> _watches = [];
    private readonly CancellationTokenSource _lifetime = new();
    private LogFileReader _reader = default!;
    private List<LogSource> _sources = [];
    private List<LogRow> _rows = [];
    private string? _errorMessage;

    /// <summary>Gets or sets the selected source path; empty shows every log.</summary>
    private string SelectedPath { get; set; } = string.Empty;

    /// <summary>Gets or sets whether rotated archives are listed and read.</summary>
    private bool IncludeArchives { get; set; }

    private LogSource? SelectedSource =>
        _sources.FirstOrDefault(source => source.Path.Value == SelectedPath);

    private bool CanTruncate => SelectedSource is { IsArchive: false };

    private string StatusText
    {
        get
        {
            int files = _contents.Count(pair => pair.Value.Error is null);
            int unparsed = _contents.Values.Sum(static content => content.UnparsedLineCount);
            string text = $"{_rows.Count} entries from {files} file(s)";
            return unparsed > 0 ? $"{text} · {unparsed} unparsed line(s)" : text;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAppInitializedAsync()
    {
        _reader = new LogFileReader(AppContext.FileSystem);
        await ReloadAllAsync();
        await AttachWatchesAsync();
    }

    /// <summary>Re-lists the log files and re-reads every visible one.</summary>
    private async Task ReloadAllAsync()
    {
        _errorMessage = null;
        _sources = [.. await _reader.ListSourcesAsync(IncludeArchives, _lifetime.Token)];
        if (SelectedPath.Length > 0 && SelectedSource is null)
        {
            SelectedPath = string.Empty;
        }

        _contents.Clear();
        foreach (LogSource source in VisibleSources())
        {
            _contents[source.Path] = await _reader.ReadAsync(source, _lifetime.Token);
        }

        RebuildRows();
    }

    private Task OnSelectionChangedAsync() => ReloadAllAsync();

    private IEnumerable<LogSource> VisibleSources() =>
        SelectedSource is LogSource selected ? [selected] : _sources;

    /// <summary>Merges every loaded file, newest first; file order then line order break ties.</summary>
    private void RebuildRows()
    {
        _rows = [.. _contents.Values
            .SelectMany(static content => content.Rows)
            .OrderByDescending(static row => row.Line.TimestampUtc)
            .ThenByDescending(static row => row.LineNumber)];

        FileSystemError? failure = _contents.Values.Select(static content => content.Error).FirstOrDefault(static error => error is not null);
        if (failure is not null && failure.Code != FileSystemErrorCode.NotFound)
        {
            _errorMessage = $"Could not read {failure.Path?.Value ?? "a log file"}: {failure.Code}.";
        }
    }

    /// <summary>Watches <c>/var/log</c> and <c>/var/log/apps</c> so new lines appear without a manual refresh.</summary>
    private async Task AttachWatchesAsync()
    {
        foreach (VirtualPath directory in new[] { LogFileReader.LogDirectory, LogFileReader.AppLogDirectory })
        {
            try
            {
                ITopicChannelSubscription<FileSystemChangeEvent> subscription =
                    await AppContext.Watch.WatchAsync(directory, FileSystemWatchScope.ImmediateChildren, _lifetime.Token);
                _watches.Add(subscription);
                _ = WatchLoopAsync(subscription, _lifetime.Token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A missing directory or a host without watch support only loses live updates;
                // Refresh still works.
            }
        }
    }

    private async Task WatchLoopAsync(
        ITopicChannelSubscription<FileSystemChangeEvent> subscription,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (TopicMessage<FileSystemChangeEvent> message in subscription.Reader.ReadAllAsync(cancellationToken))
            {
                await InvokeAsync(async () =>
                {
                    await OnFileChangedAsync(message.Payload);
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the window closes.
        }
    }

    /// <summary>
    /// Re-reads one file when only its content changed; any structural change (new app log,
    /// rotation, truncation by delete) re-lists everything.
    /// </summary>
    private async Task OnFileChangedAsync(FileSystemChangeEvent change)
    {
        if (change.Kind == FileSystemChangeKind.ContentModified)
        {
            LogSource? source = VisibleSources().FirstOrDefault(candidate => candidate.Path == change.Path);
            if (source is not null)
            {
                _contents[source.Path] = await _reader.ReadAsync(source, _lifetime.Token);
                RebuildRows();
            }

            return;
        }

        await ReloadAllAsync();
    }

    private async Task TruncateSelectedAsync()
    {
        if (SelectedSource is not LogSource source || source.IsArchive)
        {
            return;
        }

        FileSystemMutationResult result = await _reader.TruncateAsync(source, _lifetime.Token);
        if (result.Succeeded)
        {
            await ReloadAllAsync();
            return;
        }

        // Permission-class failures also raise the window host's approval prompt (ADR 0042);
        // after an administrator approves once, pressing Truncate again succeeds.
        _errorMessage = result.Transaction.Error?.Code switch
        {
            FileSystemErrorCode.PermissionDenied or FileSystemErrorCode.AuthorityDenied or FileSystemErrorCode.CapabilityDenied =>
                $"Truncating {source.Path.Value} needs administrator approval. Approve the request, then press Truncate again.",
            FileSystemErrorCode code => $"Could not truncate {source.Path.Value}: {code}.",
            null => $"Could not truncate {source.Path.Value}."
        };
    }

    private static string FormatSource(LogLine line) =>
        line.ProcessId is long pid ? $"{line.Source}[{pid}]" : line.Source;

    private static string FormatMessage(LogLine line) =>
        line.Properties.Count == 0
            ? line.Message
            : $"{line.Message} {{{string.Join(", ", line.Properties.Select(static pair => $"{pair.Key}={pair.Value}"))}}}";

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync();
        foreach (ITopicChannelSubscription<FileSystemChangeEvent> watch in _watches)
        {
            await watch.DisposeAsync();
        }

        _watches.Clear();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
