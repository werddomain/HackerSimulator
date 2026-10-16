namespace HackerOs.AppSdk.FileView.Transfers;

/// <summary>What a paste or a drop does with its sources (ADR 0058, section 1).</summary>
public enum FileTransferMode
{
    /// <summary>Duplicate the sources; a name already taken gets a <c>(copy)</c> suffix.</summary>
    Copy = 0,

    /// <summary>Move the sources; a name already taken asks <see cref="FileConflictChoice"/>.</summary>
    Move = 1
}

/// <summary>Answer to a name conflict while moving (the "Replace / Keep both / Skip" window).</summary>
public enum FileConflictChoice
{
    /// <summary>Delete what is at the destination, then move the source there.</summary>
    Replace = 0,

    /// <summary>Move the source under a free <c>(copy)</c> name, keeping both.</summary>
    KeepBoth = 1,

    /// <summary>Leave this source where it is.</summary>
    Skip = 2
}

/// <summary>The user's answer to one conflict.</summary>
/// <param name="Choice">What to do with this source.</param>
/// <param name="ApplyToAll">Whether the same answer goes for every later conflict of this transfer, without asking.</param>
public sealed record FileConflictResolution(FileConflictChoice Choice, bool ApplyToAll = false);

/// <summary>One name conflict a move ran into.</summary>
/// <param name="SourcePath">Entry being moved.</param>
/// <param name="DestinationPath">Path already taken.</param>
/// <param name="SourceIsDirectory">Whether the source is a directory.</param>
/// <param name="DestinationIsDirectory">Whether what occupies the destination is a directory.</param>
public sealed record FileTransferConflict(string SourcePath, string DestinationPath, bool SourceIsDirectory, bool DestinationIsDirectory)
{
    /// <summary>Gets the conflicting name.</summary>
    public string Name => FileTransfer.GetName(DestinationPath);
}

/// <summary>Asks the user how to settle a name conflict.</summary>
/// <param name="conflict">The conflict.</param>
public delegate Task<FileConflictResolution> FileConflictResolver(FileTransferConflict conflict);

/// <summary>One source that could not be transferred, and why, for the end-of-operation summary.</summary>
/// <param name="Path">Source path (or host file name).</param>
/// <param name="Reason">Short reason, in English like the rest of the UI ("permission denied", "read-only file system"…).</param>
public sealed record FileTransferFailure(string Path, string Reason);

/// <summary>One source that reached its destination.</summary>
/// <param name="SourcePath">Original path (or host file name).</param>
/// <param name="DestinationPath">Where it is now.</param>
public sealed record FileTransferItemResult(string SourcePath, string DestinationPath);

/// <summary>Outcome of a whole paste, drop, import or trash operation: an error on one source never stops the others.</summary>
/// <param name="Completed">Sources transferred, in order.</param>
/// <param name="Skipped">Sources left alone: answered "Skip", or cut and pasted into their own folder.</param>
/// <param name="Failures">Sources that failed, with the reason.</param>
/// <param name="Cancelled">Whether the operation stopped early (a stop request of the app, ADR 0069): the sources not done are among <paramref name="Failures"/>.</param>
public sealed record FileTransferResult(
    IReadOnlyList<FileTransferItemResult> Completed,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<FileTransferFailure> Failures,
    bool Cancelled = false)
{
    /// <summary>Gets whether no source failed.</summary>
    public bool Succeeded => Failures.Count == 0;

    /// <summary>Builds the one-paragraph summary of the failures, or <see langword="null"/> when there are none.</summary>
    /// <param name="verb">Verb of the operation, e.g. <c>"copy"</c>.</param>
    public string? DescribeFailures(string verb)
    {
        if (Failures.Count == 0)
        {
            return null;
        }

        string items = string.Join("; ", Failures.Select(failure => $"'{FileTransfer.GetName(failure.Path)}': {failure.Reason}"));
        return Failures.Count == 1
            ? $"Could not {verb} {items}."
            : $"Could not {verb} {Failures.Count} items: {items}.";
    }
}

/// <summary>
/// A file dropped from the host system (the user's real desktop), read through the browser. Its
/// content is only read when the transfer reaches it.
/// </summary>
/// <param name="Name">File name as the host gave it.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="ContentType">MIME type the browser guessed, possibly empty.</param>
/// <param name="OpenReadStreamAsync">Opens the content; the argument caps the bytes read.</param>
public sealed record FileTransferHostFile(
    string Name,
    long Size,
    string? ContentType,
    Func<long, CancellationToken, Task<Stream>> OpenReadStreamAsync);
