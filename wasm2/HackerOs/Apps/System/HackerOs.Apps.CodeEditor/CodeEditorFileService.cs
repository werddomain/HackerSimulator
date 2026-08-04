using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Apps.CodeEditor;

/// <summary>Identifies typed, recoverable Code Editor file-operation outcomes.</summary>
public enum CodeEditorFileStatus
{
    /// <summary>The operation completed.</summary>
    Success,
    /// <summary>The scoped gateway denied the operation.</summary>
    Denied,
    /// <summary>The virtual path does not exist.</summary>
    NotFound,
    /// <summary>The selected entry is not editable text.</summary>
    UnsupportedContent,
    /// <summary>The text exceeds the editor's bounded buffer.</summary>
    TooLarge,
    /// <summary>The optimistic VFS revision is stale.</summary>
    Conflict,
    /// <summary>The operation failed for another stable reason.</summary>
    Failed
}

/// <summary>Contains a typed VFS load result.</summary>
public sealed record CodeEditorLoadResult(
    CodeEditorFileStatus Status,
    VirtualPath Path,
    string? Content = null,
    long? Revision = null,
    string? Error = null)
{
    /// <summary>Gets whether a document can be created from this result.</summary>
    public bool Succeeded => Status == CodeEditorFileStatus.Success;
}

/// <summary>Contains a typed VFS save result.</summary>
public sealed record CodeEditorSaveResult(
    CodeEditorFileStatus Status,
    VirtualPath Path,
    long? Revision = null,
    string? Error = null)
{
    /// <summary>Gets whether the document was committed.</summary>
    public bool Succeeded => Status == CodeEditorFileStatus.Success;
}

/// <summary>
/// Performs bounded text reads and atomic optimistic writes through one app-scoped
/// filesystem gateway. It never exposes an unrestricted repository.
/// </summary>
public sealed class CodeEditorFileService
{
    private static readonly FileSystemPermissions DefaultFilePermissions =
        FileSystemPermissions.FromMode(0b110_100_100);

    private readonly IAppFileSystemGateway _fileSystem;

    /// <summary>Creates a file service bound to the running app instance.</summary>
    public CodeEditorFileService(IAppFileSystemGateway fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <summary>Loads bounded UTF-8 text and returns stable failures to the UI.</summary>
    public async ValueTask<CodeEditorLoadResult> LoadAsync(
        VirtualPath path,
        CancellationToken cancellationToken = default)
    {
        FileSystemResult<FileSystemContentReadHandle> result = await _fileSystem.ReadAsync(
            new FileSystemReadRequest(path), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return LoadFailure(path, result.Error?.Code);
        }

        await using FileSystemContentReadHandle handle = result.Value;
        if (handle.Descriptor.Kind == FileSystemContentKind.Binary)
        {
            return new CodeEditorLoadResult(
                CodeEditorFileStatus.UnsupportedContent, path, Error: "Binary files cannot be edited as source text.");
        }

        if (handle.Entry.Metadata is FileMetadata metadata
            && metadata.Length > CodeEditorDocument.MaxDocumentBytes)
        {
            return TooLargeLoad(path);
        }

        using StreamReader reader = new(handle.Content, Encoding.UTF8, true, leaveOpen: true);
        string content = await reader.ReadToEndAsync(cancellationToken);
        if (Encoding.UTF8.GetByteCount(content) > CodeEditorDocument.MaxDocumentBytes)
        {
            return TooLargeLoad(path);
        }

        return new CodeEditorLoadResult(
            CodeEditorFileStatus.Success,
            path,
            content,
            handle.Entry.Metadata.Revision);
    }

    /// <summary>
    /// Atomically replaces an existing file or creates a new file and then performs
    /// an atomic content replacement using the resulting revision.
    /// </summary>
    public async ValueTask<CodeEditorSaveResult> SaveAsync(
        CodeEditorDocument document,
        VirtualPath path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Utf8ByteCount > CodeEditorDocument.MaxDocumentBytes)
        {
            return new CodeEditorSaveResult(
                CodeEditorFileStatus.TooLarge, path, Error: "Document exceeds the editor size limit.");
        }

        long? revision = document.Path == path ? document.Revision : null;
        if (revision is null)
        {
            FileSystemResult<FileSystemEntrySnapshot> target = await _fileSystem.StatAsync(
                new FileSystemStatRequest(path), cancellationToken);
            if (target.Succeeded && target.Value is not null)
            {
                revision = target.Value.Metadata.Revision;
            }
            else if (target.Error?.Code == FileSystemErrorCode.NotFound)
            {
                CodeEditorSaveResult create = await CreateFileAsync(path, cancellationToken);
                if (!create.Succeeded)
                {
                    return create;
                }

                revision = create.Revision;
            }
            else
            {
                return SaveFailure(path, target.Error?.Code);
            }
        }

        if (revision is not long expectedRevision)
        {
            return new CodeEditorSaveResult(
                CodeEditorFileStatus.Failed, path, Error: "No writable file revision is available.");
        }

        FileSystemMutationResult write = await _fileSystem.WriteAsync(
            new FileSystemWriteRequest(path, expectedRevision),
            new Utf8TextSource(document.Content, MediaTypeFor(document.SyntaxMode)),
            cancellationToken);
        if (!write.Succeeded || write.Entry is null)
        {
            return SaveFailure(path, write.Transaction.Error?.Code);
        }

        document.CompleteSave(path, write.Entry.Metadata.Revision);
        return new CodeEditorSaveResult(
            CodeEditorFileStatus.Success, path, write.Entry.Metadata.Revision);
    }

    private async ValueTask<CodeEditorSaveResult> CreateFileAsync(
        VirtualPath path,
        CancellationToken cancellationToken)
    {
        VirtualPath parent = ParentOf(path);
        FileSystemResult<FileSystemEntrySnapshot> parentResult = await _fileSystem.StatAsync(
            new FileSystemStatRequest(parent), cancellationToken);
        if (!parentResult.Succeeded || parentResult.Value is null)
        {
            return SaveFailure(path, parentResult.Error?.Code);
        }

        FileSystemMutationResult create = await _fileSystem.CreateAsync(
            new FileSystemCreateRequest(
                path,
                FileSystemEntryKind.File,
                DefaultFilePermissions,
                parentResult.Value.Metadata.Revision),
            cancellationToken);
        return create.Succeeded && create.Entry is not null
            ? new CodeEditorSaveResult(
                CodeEditorFileStatus.Success, path, create.Entry.Metadata.Revision)
            : SaveFailure(path, create.Transaction.Error?.Code);
    }

    private static CodeEditorLoadResult LoadFailure(VirtualPath path, FileSystemErrorCode? error) =>
        error switch
        {
            FileSystemErrorCode.PermissionDenied or
            FileSystemErrorCode.CapabilityDenied or
            FileSystemErrorCode.AuthorityDenied =>
                new CodeEditorLoadResult(CodeEditorFileStatus.Denied, path, Error: "Access denied."),
            FileSystemErrorCode.NotFound =>
                new CodeEditorLoadResult(CodeEditorFileStatus.NotFound, path, Error: "File not found."),
            FileSystemErrorCode.NotFile =>
                new CodeEditorLoadResult(
                    CodeEditorFileStatus.UnsupportedContent, path, Error: "Path is not a regular file."),
            _ => new CodeEditorLoadResult(
                CodeEditorFileStatus.Failed, path, Error: error?.ToString() ?? "Read failed.")
        };

    private static CodeEditorSaveResult SaveFailure(VirtualPath path, FileSystemErrorCode? error) =>
        error switch
        {
            FileSystemErrorCode.PermissionDenied or
            FileSystemErrorCode.CapabilityDenied or
            FileSystemErrorCode.AuthorityDenied =>
                new CodeEditorSaveResult(CodeEditorFileStatus.Denied, path, Error: "Access denied."),
            FileSystemErrorCode.NotFound =>
                new CodeEditorSaveResult(CodeEditorFileStatus.NotFound, path, Error: "Destination not found."),
            FileSystemErrorCode.RevisionConflict =>
                new CodeEditorSaveResult(CodeEditorFileStatus.Conflict, path, Error: "File changed on disk."),
            _ => new CodeEditorSaveResult(
                CodeEditorFileStatus.Failed, path, Error: error?.ToString() ?? "Write failed.")
        };

    private static CodeEditorLoadResult TooLargeLoad(VirtualPath path) => new(
        CodeEditorFileStatus.TooLarge,
        path,
        Error: $"File exceeds the {CodeEditorDocument.MaxDocumentBytes / 1024} KiB editor limit.");

    private static VirtualPath ParentOf(VirtualPath path)
    {
        int separator = path.Value.LastIndexOf('/');
        return separator <= 0 ? VirtualPath.Parse("/") : VirtualPath.Parse(path.Value[..separator]);
    }

    private static string MediaTypeFor(CodeEditorSyntaxMode mode) => mode switch
    {
        CodeEditorSyntaxMode.CSharp => "text/x-csharp",
        CodeEditorSyntaxMode.JavaScript => "text/javascript",
        CodeEditorSyntaxMode.TypeScript => "text/typescript",
        CodeEditorSyntaxMode.Html => "text/html",
        CodeEditorSyntaxMode.Css => "text/css",
        CodeEditorSyntaxMode.Json => "application/json",
        CodeEditorSyntaxMode.Markdown => "text/markdown",
        _ => "text/plain"
    };

    private sealed class Utf8TextSource(string content, string mediaType) : IFileSystemContentSource
    {
        private readonly byte[] _bytes = Encoding.UTF8.GetBytes(content);

        public FileSystemContentDescriptor Descriptor { get; } =
            FileSystemContentDescriptor.Text(mediaType, "utf-8");

        public long? Length => _bytes.LongLength;

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<Stream>(new MemoryStream(_bytes, writable: false));
        }
    }
}
