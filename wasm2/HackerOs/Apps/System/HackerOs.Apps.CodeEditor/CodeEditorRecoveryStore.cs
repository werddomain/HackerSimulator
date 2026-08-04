using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Apps.CodeEditor;

/// <summary>Identifies the typed outcome of loading, saving, or clearing editor recovery data.</summary>
public enum CodeEditorRecoveryStatus
{
    /// <summary>The operation completed successfully.</summary>
    Success,
    /// <summary>No recovery snapshot exists.</summary>
    Missing,
    /// <summary>The scoped VFS gateway denied access.</summary>
    Denied,
    /// <summary>The recovery data is malformed or belongs to another user.</summary>
    Invalid,
    /// <summary>The snapshot exceeds its bounded recovery size.</summary>
    TooLarge,
    /// <summary>An optimistic write conflict occurred.</summary>
    Conflict,
    /// <summary>An unexpected stable VFS failure occurred.</summary>
    Failed
}

/// <summary>Returns an optional C#-owned editor session recovery snapshot.</summary>
public sealed record CodeEditorRecoveryResult(
    CodeEditorRecoveryStatus Status,
    CodeEditorSessionRecovery? Session = null,
    string? Error = null)
{
    /// <summary>Gets whether a valid session was recovered.</summary>
    public bool HasSession => Status == CodeEditorRecoveryStatus.Success && Session is not null;
}

/// <summary>
/// Persists unsaved Code Editor state through the app-scoped VFS. The store never
/// uses browser local/session storage, and every serialized field remains owned by
/// the C# session model.
/// </summary>
public sealed class CodeEditorRecoveryStore
{
    /// <summary>Maximum UTF-8 serialized recovery size.</summary>
    public const int MaxRecoveryBytes = 8 * 1024 * 1024;

    private const int SchemaVersion = 1;
    private static readonly FileSystemPermissions DirectoryPermissions =
        FileSystemPermissions.FromMode(0b111_000_000);
    private static readonly FileSystemPermissions FilePermissions =
        FileSystemPermissions.FromMode(0b110_000_000);

    private readonly IAppFileSystemGateway _fileSystem;
    private readonly string _userId;

    /// <summary>Creates a recovery store for the current authenticated user.</summary>
    public CodeEditorRecoveryStore(IAppFileSystemGateway fileSystem, string userId)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (userId.Contains('/', StringComparison.Ordinal) || userId.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("User IDs used in recovery paths cannot contain path separators.", nameof(userId));
        }

        _userId = userId;
    }

    /// <summary>Gets the app-private VFS recovery file path for the current user.</summary>
    public VirtualPath RecoveryPath => VirtualPath.Parse(
        $"/home/{_userId}/.local/state/hackeros/code-editor/session.json");

    /// <summary>Loads and validates the most recent recovery snapshot.</summary>
    public async ValueTask<CodeEditorRecoveryResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        FileSystemResult<FileSystemContentReadHandle> read = await _fileSystem.ReadAsync(
            new FileSystemReadRequest(RecoveryPath), cancellationToken);
        if (!read.Succeeded || read.Value is null)
        {
            return read.Error?.Code == FileSystemErrorCode.NotFound
                ? new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.Missing)
                : Failure(read.Error?.Code);
        }

        await using FileSystemContentReadHandle handle = read.Value;
        if (handle.Descriptor.Kind != FileSystemContentKind.Text
            || handle.Entry.Metadata is not FileMetadata)
        {
            return new CodeEditorRecoveryResult(
                CodeEditorRecoveryStatus.Invalid,
                Error: "Recovery data is not bounded UTF-8 text.");
        }

        FileMetadata metadata = (FileMetadata)handle.Entry.Metadata;
        if (metadata.Length > MaxRecoveryBytes)
        {
            return new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.TooLarge);
        }

        try
        {
            using MemoryStream copy = new();
            await handle.Content.CopyToAsync(copy, cancellationToken);
            if (copy.Length > MaxRecoveryBytes)
            {
                return new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.TooLarge);
            }

            CodeEditorRecoveryEnvelope? envelope = JsonSerializer.Deserialize(
                copy.GetBuffer().AsSpan(0, checked((int)copy.Length)),
                CodeEditorRecoveryJsonContext.Default.CodeEditorRecoveryEnvelope);
            if (envelope is null
                || envelope.SchemaVersion != SchemaVersion
                || !string.Equals(envelope.UserId, _userId, StringComparison.Ordinal))
            {
                return new CodeEditorRecoveryResult(
                    CodeEditorRecoveryStatus.Invalid,
                    Error: "Recovery data is incompatible with this user or schema.");
            }

            _ = CodeEditorSession.Restore(envelope.Session);
            return new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.Success, envelope.Session);
        }
        catch (JsonException)
        {
            return new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.Invalid, Error: "Recovery JSON is malformed.");
        }
        catch (ArgumentException)
        {
            return new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.Invalid, Error: "Recovery content is invalid.");
        }
    }

    /// <summary>Atomically replaces the recovery file with the current session snapshot.</summary>
    public async ValueTask<CodeEditorRecoveryResult> SaveAsync(
        CodeEditorSessionRecovery session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            new CodeEditorRecoveryEnvelope(SchemaVersion, _userId, session),
            CodeEditorRecoveryJsonContext.Default.CodeEditorRecoveryEnvelope);
        if (bytes.Length > MaxRecoveryBytes)
        {
            return new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.TooLarge);
        }

        CodeEditorRecoveryResult directory = await EnsureParentDirectoriesAsync(cancellationToken);
        if (directory.Status != CodeEditorRecoveryStatus.Success)
        {
            return directory;
        }

        FileSystemResult<FileSystemEntrySnapshot> existing = await _fileSystem.StatAsync(
            new FileSystemStatRequest(RecoveryPath), cancellationToken);
        long revision;
        if (existing.Succeeded && existing.Value is not null)
        {
            revision = existing.Value.Metadata.Revision;
        }
        else if (existing.Error?.Code == FileSystemErrorCode.NotFound)
        {
            FileSystemResult<FileSystemEntrySnapshot> parent = await _fileSystem.StatAsync(
                new FileSystemStatRequest(ParentOf(RecoveryPath)), cancellationToken);
            if (!parent.Succeeded || parent.Value is null)
            {
                return Failure(parent.Error?.Code);
            }

            FileSystemMutationResult create = await _fileSystem.CreateAsync(
                new FileSystemCreateRequest(
                    RecoveryPath,
                    FileSystemEntryKind.File,
                    FilePermissions,
                    parent.Value.Metadata.Revision),
                cancellationToken);
            if (!create.Succeeded || create.Entry is null)
            {
                return Failure(create.Transaction.Error?.Code);
            }

            revision = create.Entry.Metadata.Revision;
        }
        else
        {
            return Failure(existing.Error?.Code);
        }

        FileSystemMutationResult write = await _fileSystem.WriteAsync(
            new FileSystemWriteRequest(RecoveryPath, revision),
            new RecoveryContentSource(bytes),
            cancellationToken);
        return write.Succeeded
            ? new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.Success)
            : Failure(write.Transaction.Error?.Code);
    }

    /// <summary>Deletes the exact app-private snapshot after the user knowingly closes the editor.</summary>
    public async ValueTask<CodeEditorRecoveryResult> ClearAsync(CancellationToken cancellationToken = default)
    {
        FileSystemResult<FileSystemEntrySnapshot> existing = await _fileSystem.StatAsync(
            new FileSystemStatRequest(RecoveryPath), cancellationToken);
        if (!existing.Succeeded || existing.Value is null)
        {
            return existing.Error?.Code == FileSystemErrorCode.NotFound
                ? new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.Success)
                : Failure(existing.Error?.Code);
        }

        VirtualPath parent = ParentOf(RecoveryPath);
        FileSystemResult<FileSystemEntrySnapshot> parentResult = await _fileSystem.StatAsync(
            new FileSystemStatRequest(parent), cancellationToken);
        if (!parentResult.Succeeded || parentResult.Value is null)
        {
            return Failure(parentResult.Error?.Code);
        }

        FileSystemMutationResult deleted = await _fileSystem.DeleteAsync(
            new FileSystemDeleteRequest(
                RecoveryPath,
                existing.Value.Metadata.Revision,
                parentResult.Value.Metadata.Revision),
            cancellationToken);
        return deleted.Succeeded
            ? new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.Success)
            : Failure(deleted.Transaction.Error?.Code);
    }

    private async ValueTask<CodeEditorRecoveryResult> EnsureParentDirectoriesAsync(CancellationToken cancellationToken)
    {
        string[] segments = [".local", "state", "hackeros", "code-editor"];
        VirtualPath current = VirtualPath.Parse($"/home/{_userId}");
        foreach (string segment in segments)
        {
            VirtualPath next = VirtualPath.Parse($"{current.Value}/{segment}");
            FileSystemResult<FileSystemEntrySnapshot> state = await _fileSystem.StatAsync(
                new FileSystemStatRequest(next), cancellationToken);
            if (state.Succeeded)
            {
                if (state.Value?.Metadata is not DirectoryMetadata)
                {
                    return new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.Invalid, Error: "Recovery path is not a directory.");
                }

                current = next;
                continue;
            }

            if (state.Error?.Code != FileSystemErrorCode.NotFound)
            {
                return Failure(state.Error?.Code);
            }

            FileSystemResult<FileSystemEntrySnapshot> parent = await _fileSystem.StatAsync(
                new FileSystemStatRequest(current), cancellationToken);
            if (!parent.Succeeded || parent.Value is null)
            {
                return Failure(parent.Error?.Code);
            }

            FileSystemMutationResult create = await _fileSystem.CreateAsync(
                new FileSystemCreateRequest(next, FileSystemEntryKind.Directory, DirectoryPermissions,
                    parent.Value.Metadata.Revision), cancellationToken);
            if (!create.Succeeded && create.Transaction.Error?.Code != FileSystemErrorCode.AlreadyExists)
            {
                return Failure(create.Transaction.Error?.Code);
            }

            current = next;
        }

        return new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.Success);
    }

    private static CodeEditorRecoveryResult Failure(FileSystemErrorCode? code) => code switch
    {
        FileSystemErrorCode.PermissionDenied or FileSystemErrorCode.CapabilityDenied or FileSystemErrorCode.AuthorityDenied =>
            new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.Denied, Error: "Recovery access denied."),
        FileSystemErrorCode.RevisionConflict =>
            new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.Conflict, Error: "Recovery changed concurrently."),
        _ => new CodeEditorRecoveryResult(CodeEditorRecoveryStatus.Failed, Error: code?.ToString() ?? "Recovery operation failed.")
    };

    private static VirtualPath ParentOf(VirtualPath path)
    {
        int slash = path.Value.LastIndexOf('/');
        return slash <= 0 ? VirtualPath.Parse("/") : VirtualPath.Parse(path.Value[..slash]);
    }

    private sealed class RecoveryContentSource(byte[] bytes) : IFileSystemContentSource
    {
        public FileSystemContentDescriptor Descriptor { get; } =
            FileSystemContentDescriptor.Text("application/json", "utf-8");

        public long? Length => bytes.LongLength;

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }
    }
}

internal sealed record CodeEditorRecoveryEnvelope(
    int SchemaVersion,
    string UserId,
    CodeEditorSessionRecovery Session);

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(CodeEditorRecoveryEnvelope))]
internal partial class CodeEditorRecoveryJsonContext : JsonSerializerContext;
