using HackerOs.AppSdk.IO;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.AppSdk.FileView.Transfers;

/// <summary>
/// Copy, move, import and trash operations behind cut/copy/paste and drag-and-drop (ADR 0058,
/// section 1), shaped on Nautilus: a copy whose name is taken becomes <c>name (copy).ext</c>, then
/// <c>name (copy 2).ext</c>…; a move whose name is taken asks Replace, Keep both or Skip. A folder
/// is never copied or moved into itself; an error on one source never stops the others.
/// </summary>
/// <remarks>
/// Every member runs as the ambient app through the public <c>Hack*</c> API (ADR 0043), so the app's
/// capabilities and the file permissions apply to each step. Call it from an app event handler (a
/// <c>HackComponentBase</c> handler is in scope) or inside <see cref="HackExecutionContext.Enter"/>.
/// </remarks>
public static class FileTransfer
{
    /// <summary>Largest host file an import reads (256 MiB); a bigger one is reported as a failure.</summary>
    public const long MaxHostFileBytes = 256L * 1024 * 1024;

    /// <summary>Copies sources into a directory; a taken name gets a <c>(copy)</c> suffix, without asking.</summary>
    /// <param name="sources">Absolute paths of files and directories (directories are copied recursively).</param>
    /// <param name="destinationDirectory">Existing target directory.</param>
    /// <param name="cancellationToken">Stops before the next source.</param>
    public static Task<FileTransferResult> CopyAsync(
        IReadOnlyList<string> sources, string destinationDirectory, CancellationToken cancellationToken = default) =>
        TransferAsync(sources, destinationDirectory, FileTransferMode.Copy, resolveConflict: null, cancellationToken);

    /// <summary>
    /// Moves sources into a directory. A source already in that directory is skipped (cut then pasted
    /// in place does nothing); a taken name asks <paramref name="resolveConflict"/>, or is skipped when
    /// none is supplied.
    /// </summary>
    /// <param name="sources">Absolute paths of files and directories.</param>
    /// <param name="destinationDirectory">Existing target directory.</param>
    /// <param name="resolveConflict">Asks the user; <see langword="null"/> skips every conflict.</param>
    /// <param name="cancellationToken">Stops before the next source.</param>
    public static Task<FileTransferResult> MoveAsync(
        IReadOnlyList<string> sources,
        string destinationDirectory,
        FileConflictResolver? resolveConflict,
        CancellationToken cancellationToken = default) =>
        TransferAsync(sources, destinationDirectory, FileTransferMode.Move, resolveConflict, cancellationToken);

    /// <summary>Copies or moves sources into a directory; see <see cref="CopyAsync"/> and <see cref="MoveAsync"/>.</summary>
    /// <param name="sources">Absolute paths of files and directories.</param>
    /// <param name="destinationDirectory">Existing target directory.</param>
    /// <param name="mode">Copy or move.</param>
    /// <param name="resolveConflict">Used by a move only.</param>
    /// <param name="cancellationToken">Stops before the next source.</param>
    public static async Task<FileTransferResult> TransferAsync(
        IReadOnlyList<string> sources,
        string destinationDirectory,
        FileTransferMode mode,
        FileConflictResolver? resolveConflict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        string destination = HackPath.GetFullPath(destinationDirectory);
        Outcome outcome = new();

        HashSet<string> taken;
        try
        {
            taken = await ListNamesAsync(destination, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            foreach (string source in sources)
            {
                outcome.Fail(source, $"cannot open '{destination}': {Describe(exception)}");
            }

            return outcome.ToResult();
        }

        // Validation pass before any write: a folder never goes into itself or a descendant.
        List<string> valid = [];
        foreach (string rawSource in sources)
        {
            string source = HackPath.GetFullPath(rawSource);
            if (destination == source || destination.StartsWith(source + "/", StringComparison.Ordinal))
            {
                outcome.Fail(source, mode == FileTransferMode.Copy
                    ? "cannot copy a folder into itself"
                    : "cannot move a folder into itself");
            }
            else
            {
                valid.Add(source);
            }
        }

        FileConflictResolution? appliedToAll = null;
        foreach (string source in valid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                bool sourceIsDirectory = await HackDirectory.ExistsAsync(source, cancellationToken).ConfigureAwait(false);
                if (!sourceIsDirectory && !await HackFile.ExistsAsync(source, cancellationToken).ConfigureAwait(false))
                {
                    outcome.Fail(source, "no longer exists");
                    continue;
                }

                string name = GetName(source);
                string? parent = HackPath.GetDirectoryName(source);
                if (mode == FileTransferMode.Move && parent == destination)
                {
                    outcome.Skipped.Add(source);
                    continue;
                }

                string target = Combine(destination, name);
                bool replace = false;
                if (taken.Contains(name))
                {
                    if (mode == FileTransferMode.Copy)
                    {
                        target = Combine(destination, GetCopyName(name, sourceIsDirectory, taken.Contains));
                    }
                    else
                    {
                        bool destinationIsDirectory = await HackDirectory.ExistsAsync(target, cancellationToken).ConfigureAwait(false);
                        FileConflictResolution resolution = appliedToAll
                            ?? (resolveConflict is null
                                ? new FileConflictResolution(FileConflictChoice.Skip)
                                : await resolveConflict(new FileTransferConflict(source, target, sourceIsDirectory, destinationIsDirectory)).ConfigureAwait(false));
                        if (resolution.ApplyToAll)
                        {
                            appliedToAll = resolution;
                        }

                        switch (resolution.Choice)
                        {
                            case FileConflictChoice.Skip:
                                outcome.Skipped.Add(source);
                                continue;
                            case FileConflictChoice.KeepBoth:
                                target = Combine(destination, GetCopyName(name, sourceIsDirectory, taken.Contains));
                                break;
                            default:
                                replace = true;
                                break;
                        }
                    }
                }

                if (replace)
                {
                    await DeleteAsync(target, cancellationToken).ConfigureAwait(false);
                }

                await TransferOneAsync(source, target, sourceIsDirectory, mode, cancellationToken).ConfigureAwait(false);
                taken.Add(GetName(target));
                outcome.Completed.Add(new FileTransferItemResult(source, target));
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                outcome.Fail(source, Describe(exception));
            }
        }

        return outcome.ToResult();
    }

    /// <summary>
    /// Writes files dropped from the host system into a directory, like a copy: a taken name gets a
    /// <c>(copy)</c> suffix.
    /// </summary>
    /// <param name="files">Host files.</param>
    /// <param name="destinationDirectory">Existing target directory.</param>
    /// <param name="cancellationToken">Stops before the next file.</param>
    public static async Task<FileTransferResult> ImportAsync(
        IReadOnlyList<FileTransferHostFile> files, string destinationDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        string destination = HackPath.GetFullPath(destinationDirectory);
        Outcome outcome = new();

        HashSet<string> taken;
        try
        {
            taken = await ListNamesAsync(destination, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            foreach (FileTransferHostFile file in files)
            {
                outcome.Fail(file.Name, $"cannot open '{destination}': {Describe(exception)}");
            }

            return outcome.ToResult();
        }

        foreach (FileTransferHostFile file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(file.Name) || file.Name.Contains('/', StringComparison.Ordinal) || file.Name is "." or "..")
            {
                outcome.Fail(file.Name, "invalid file name");
                continue;
            }

            if (file.Size > MaxHostFileBytes)
            {
                outcome.Fail(file.Name, $"larger than {MaxHostFileBytes / (1024 * 1024)} MiB");
                continue;
            }

            string name = taken.Contains(file.Name) ? GetCopyName(file.Name, isDirectory: false, taken.Contains) : file.Name;
            string target = Combine(destination, name);
            try
            {
                using MemoryStream buffer = new();
                await using (Stream content = await file.OpenReadStreamAsync(MaxHostFileBytes, cancellationToken).ConfigureAwait(false))
                {
                    await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                }

                await HackFile.WriteAllBytesAsync(target, buffer.ToArray(), cancellationToken).ConfigureAwait(false);
                taken.Add(name);
                outcome.Completed.Add(new FileTransferItemResult(file.Name, target));
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                outcome.Fail(file.Name, Describe(exception));
            }
        }

        return outcome.ToResult();
    }

    /// <summary>Moves sources to the trash (ADR 0052), as a drop on the Trash view does.</summary>
    /// <param name="sources">Absolute paths.</param>
    /// <param name="cancellationToken">Stops before the next source.</param>
    public static async Task<FileTransferResult> MoveToTrashAsync(IReadOnlyList<string> sources, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        Outcome outcome = new();
        foreach (string rawSource in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string source = HackPath.GetFullPath(rawSource);
            try
            {
                HackTrashItem item = await HackTrash.MoveToTrashAsync(source, cancellationToken).ConfigureAwait(false);
                outcome.Completed.Add(new FileTransferItemResult(source, HackPath.Combine(HackTrash.TrashDirectory, "files", item.Name)));
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                outcome.Fail(source, Describe(exception));
            }
        }

        return outcome.ToResult();
    }

    /// <summary>
    /// Gets the first free Nautilus-style copy name: <c>report.txt</c> → <c>report (copy).txt</c>, then
    /// <c>report (copy 2).txt</c>… A name that already ends with such a suffix continues the series
    /// (<c>report (copy).txt</c> → <c>report (copy 2).txt</c>). The extension of a directory or a dot
    /// file (<c>.bashrc</c>) is not split off.
    /// </summary>
    /// <param name="name">Name that is taken.</param>
    /// <param name="isDirectory">Whether the entry is a directory.</param>
    /// <param name="isTaken">Tells whether a candidate name is already used in the destination.</param>
    public static string GetCopyName(string name, bool isDirectory, Func<string, bool> isTaken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(isTaken);

        int dot = isDirectory ? -1 : name.LastIndexOf('.');
        string stem = dot > 0 ? name[..dot] : name;
        string extension = dot > 0 ? name[dot..] : string.Empty;

        int next = 1;
        System.Text.RegularExpressions.Match suffix = System.Text.RegularExpressions.Regex.Match(stem, @"^(?<base>.+) \(copy(?: (?<n>\d+))?\)$");
        if (suffix.Success)
        {
            stem = suffix.Groups["base"].Value;
            next = suffix.Groups["n"].Success && int.TryParse(suffix.Groups["n"].Value, out int n) ? n + 1 : 2;
        }

        for (int index = next; ; index++)
        {
            string candidate = index == 1 ? $"{stem} (copy){extension}" : $"{stem} (copy {index}){extension}";
            if (!isTaken(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>Gets the last segment of a path (the name of <c>/</c> is <c>/</c>).</summary>
    /// <param name="path">Path or bare name.</param>
    public static string GetName(string path)
    {
        string trimmed = path.Length > 1 ? path.TrimEnd('/') : path;
        int slash = trimmed.LastIndexOf('/');
        return slash < 0 || trimmed.Length == 1 ? trimmed : trimmed[(slash + 1)..];
    }

    private static async Task TransferOneAsync(
        string source, string target, bool sourceIsDirectory, FileTransferMode mode, CancellationToken cancellationToken)
    {
        switch (mode, sourceIsDirectory)
        {
            case (FileTransferMode.Copy, true):
                await HackDirectory.CopyAsync(source, target, cancellationToken).ConfigureAwait(false);
                break;
            case (FileTransferMode.Copy, false):
                await HackFile.CopyAsync(source, target, overwrite: false, cancellationToken).ConfigureAwait(false);
                break;
            case (_, true):
                await HackDirectory.MoveAsync(source, target, cancellationToken).ConfigureAwait(false);
                break;
            default:
                await HackFile.MoveAsync(source, target, overwrite: false, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private static async Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        if (await HackDirectory.ExistsAsync(path, cancellationToken).ConfigureAwait(false))
        {
            await HackDirectory.DeleteAsync(path, recursive: true, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await HackFile.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<HashSet<string>> ListNamesAsync(string directory, CancellationToken cancellationToken)
    {
        string[] entries = await HackDirectory.GetFileSystemEntriesAsync(directory, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new HashSet<string>(entries.Select(GetName), StringComparer.Ordinal);
    }

    private static string Combine(string directory, string name) => directory == "/" ? "/" + name : $"{directory}/{name}";

    private static bool IsFileError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    /// <summary>Turns a file error into the short reason of the summary.</summary>
    private static string Describe(Exception exception)
    {
        FileSystemErrorCode? code = exception switch
        {
            HackUnauthorizedAccessException denied => denied.ErrorCode,
            HackIOException io => io.ErrorCode,
            _ => null
        };

        return code switch
        {
            FileSystemErrorCode.PermissionDenied or FileSystemErrorCode.AuthorityDenied => "permission denied",
            FileSystemErrorCode.CapabilityDenied => "this app is not allowed to write there",
            FileSystemErrorCode.UnsupportedOperation => "read-only file system",
            FileSystemErrorCode.ProtectedEntry => "protected system entry",
            FileSystemErrorCode.NotFound => "no longer exists",
            FileSystemErrorCode.AlreadyExists => "the name is already taken",
            _ when exception is UnauthorizedAccessException => "permission denied",
            _ => exception.Message
        };
    }

    /// <summary>Accumulates one operation's outcome.</summary>
    private sealed class Outcome
    {
        public List<FileTransferItemResult> Completed { get; } = [];

        public List<string> Skipped { get; } = [];

        private List<FileTransferFailure> Failures { get; } = [];

        public void Fail(string path, string reason) => Failures.Add(new FileTransferFailure(path, reason));

        public FileTransferResult ToResult() => new(Completed, Skipped, Failures);
    }
}
