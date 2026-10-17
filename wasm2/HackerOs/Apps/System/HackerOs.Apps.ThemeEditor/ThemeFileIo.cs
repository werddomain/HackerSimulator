using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Theming.Abstractions;

namespace HackerOs.Apps.ThemeEditor;

/// <summary>
/// Reads and writes <c>.theme</c> files through a filesystem gateway: the app's own (home folder)
/// or one carrying the handle a file dialog issued for a file elsewhere (open or export).
/// </summary>
public static class ThemeFileIo
{
    /// <summary>Reads a theme file as UTF-8 text.</summary>
    /// <param name="fileSystem">Authorized gateway.</param>
    /// <param name="path">File path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The content, or an error message.</returns>
    public static async Task<(string? Content, string? Error)> ReadAsync(
        IAppFileSystemGateway fileSystem, VirtualPath path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        FileSystemResult<FileSystemContentReadHandle> result = await fileSystem
            .ReadAsync(new FileSystemReadRequest(path), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded || result.Value is null)
        {
            return (null, Describe(result.Error?.Code));
        }

        await using FileSystemContentReadHandle handle = result.Value;
        if (handle.Entry.Metadata is FileMetadata metadata && metadata.Length > ThemeFile.MaxFileBytes)
        {
            return (null, $"A theme file is at most {ThemeFile.MaxFileBytes / 1024} KiB.");
        }

        using StreamReader reader = new(handle.Content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return (await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false), null);
    }

    /// <summary>Writes a theme file, creating it (<c>0644</c>) when needed.</summary>
    /// <param name="fileSystem">Authorized gateway.</param>
    /// <param name="path">Target path; its folder must exist.</param>
    /// <param name="content">Theme text.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="null"/> on success, otherwise an error message.</returns>
    public static async Task<string?> WriteAsync(
        IAppFileSystemGateway fileSystem, VirtualPath path, string content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(content);
        FileSystemMutationResult creation = await fileSystem.CreateAsync(
            new FileSystemCreateRequest(path, FileSystemEntryKind.File, FileSystemPermissions.FromMode(0b110_100_100)),
            cancellationToken).ConfigureAwait(false);
        if (!creation.Succeeded && creation.Transaction.Error?.Code != FileSystemErrorCode.AlreadyExists)
        {
            return Describe(creation.Transaction.Error?.Code);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(content);
        FileSystemMutationResult written = await fileSystem.WriteAsync(
            new FileSystemWriteRequest(path, null),
            new TextContentSource(bytes),
            cancellationToken).ConfigureAwait(false);
        return written.Succeeded ? null : Describe(written.Transaction.Error?.Code);
    }

    private static string Describe(FileSystemErrorCode? code) => code switch
    {
        FileSystemErrorCode.PermissionDenied or FileSystemErrorCode.CapabilityDenied or FileSystemErrorCode.AuthorityDenied => "Permission denied.",
        FileSystemErrorCode.NotFound => "File not found.",
        FileSystemErrorCode.NotFile => "The path is not a regular file.",
        null => "Unknown error.",
        _ => code.Value.ToString()
    };

    private sealed class TextContentSource(byte[] content) : IFileSystemContentSource
    {
        public FileSystemContentDescriptor Descriptor { get; } = FileSystemContentDescriptor.Text(ThemeFile.MediaType, "utf-8");

        public long? Length => content.LongLength;

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false));
        }
    }
}
