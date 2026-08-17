using System.Security.Cryptography;
using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Platform.Core.FileSystem;

/// <summary>Projects canonical settings documents through the filesystem provider contract.</summary>
public sealed class SettingsFileSystemProvider : IFileSystemProvider
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly ISettingsFileProjection _projection;
    private readonly IReadOnlyDictionary<string, SettingsDocumentDefinition> _definitions;
    private readonly VirtualPath _mountPath;
    private readonly Func<Guid> _transactionIdFactory;
    private readonly DateTimeOffset _createdAtUtc;

    /// <summary>Initializes a provider over canonical settings document definitions.</summary>
    public SettingsFileSystemProvider(
        ISettingsFileProjection projection,
        IEnumerable<SettingsDocumentDefinition> definitions,
        VirtualPath mountPath,
        Func<Guid>? transactionIdFactory = null,
        TimeProvider? timeProvider = null)
    {
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        ArgumentNullException.ThrowIfNull(definitions);
        _mountPath = mountPath;
        _transactionIdFactory = transactionIdFactory ?? Guid.NewGuid;
        _createdAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().ToUniversalTime();

        SettingsDocumentDefinition[] copiedDefinitions = definitions.ToArray();
        if (copiedDefinitions.Any(definition => !IsWithin(definition.Path, mountPath)))
        {
            throw new ArgumentException("Every settings document must be inside the provider mount.", nameof(definitions));
        }

        _definitions = copiedDefinitions.ToDictionary(
            static definition => definition.Path.Value,
            StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public string ProviderId => "settings-projection";

    /// <inheritdoc />
    public async ValueTask<FileSystemResult<FileSystemContentReadHandle>> ReadAsync(
        FileSystemReadRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        SettingsReadResult read = await _projection.ReadFileAsync(
            request.Path,
            context.OperationContext,
            cancellationToken);
        if (read.Status != SettingsReadStatus.Success)
        {
            return FileSystemResult<FileSystemContentReadHandle>.Failure(
                Error(FileSystemOperation.Read, Map(read.Status), request.Path));
        }

        SettingsDocumentSnapshot document = read.Document!;
        byte[] content = StrictUtf8.GetBytes(document.Content);
        FileSystemEntrySnapshot entry = FileSnapshot(document, content.LongLength);
        return FileSystemResult<FileSystemContentReadHandle>.Success(
            new FileSystemContentReadHandle(
                entry,
                FileSystemContentDescriptor.Text(document.MediaType, "utf-8"),
                new MemoryStream(content, writable: false)));
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemResult<FileSystemDirectorySnapshot>> EnumerateAsync(
        FileSystemEnumerateRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        if (!IsSyntheticDirectory(request.Path))
        {
            return FileSystemResult<FileSystemDirectorySnapshot>.Failure(
                Error(FileSystemOperation.Enumerate, FileSystemErrorCode.NotDirectory, request.Path));
        }

        string prefix = request.Path == _mountPath ? $"{_mountPath.Value}/" : $"{request.Path.Value}/";
        List<FileSystemDirectoryItem> items = [];
        foreach (IGrouping<string, SettingsDocumentDefinition> group in _definitions.Values
            .Where(definition => definition.Path.Value.StartsWith(prefix, StringComparison.Ordinal))
            .GroupBy(definition => definition.Path.Value[prefix.Length..].Split('/')[0], StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            SettingsDocumentDefinition? direct = group.FirstOrDefault(
                definition => definition.Path.Value == $"{prefix}{group.Key}");
            FileSystemEntryMetadata metadata;
            if (direct is null)
            {
                metadata = DirectoryMetadataFor(VirtualPath.Parse($"{prefix}{group.Key}"));
            }
            else
            {
                SettingsReadResult read = await _projection.ReadFileAsync(
                    direct.Path,
                    context.OperationContext,
                    cancellationToken);
                if (read.Status != SettingsReadStatus.Success)
                {
                    return FileSystemResult<FileSystemDirectorySnapshot>.Failure(
                        Error(FileSystemOperation.Enumerate, Map(read.Status), direct.Path));
                }

                metadata = FileSnapshot(
                    read.Document!,
                    StrictUtf8.GetByteCount(read.Document!.Content)).Metadata;
            }

            items.Add(new FileSystemDirectoryItem(FileSystemEntryName.Parse(group.Key), metadata));
        }

        return FileSystemResult<FileSystemDirectorySnapshot>.Success(
            new FileSystemDirectorySnapshot(request.Path, 1, items));
    }

    /// <inheritdoc />
    public ValueTask<FileSystemMutationResult> CreateAsync(
        FileSystemCreateRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Rejected(
            FileSystemOperation.Create,
            FileSystemErrorCode.ProtectedEntry,
            request.Path));

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> WriteAsync(
        FileSystemWriteRequest request,
        IFileSystemContentSource content,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        string candidate;
        try
        {
            await using Stream stream = await content.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            using StreamReader reader = new(stream, StrictUtf8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            candidate = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DecoderFallbackException)
        {
            return Rejected(FileSystemOperation.Write, FileSystemErrorCode.InvalidContent, request.Path);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Cancelled(FileSystemOperation.Write, request.Path);
        }

        long expectedRevision = request.ExpectedRevision ?? await ResolveCurrentRevisionAsync(request.Path, context, cancellationToken);
        SettingsWriteResult write = await _projection.WriteFileAsync(
            new SettingsWriteRequest(request.Path, candidate, expectedRevision),
            context.OperationContext,
            cancellationToken);
        if (write.Status != SettingsWriteStatus.Success)
        {
            return Rejected(FileSystemOperation.Write, Map(write.Status), request.Path);
        }

        SettingsDocumentSnapshot document = write.Document!;
        FileSystemEntrySnapshot entry = FileSnapshot(document, StrictUtf8.GetByteCount(document.Content));
        FileSystemTransactionResult transaction = FileSystemTransactionResult.Committed(
            NextTransactionId(),
            [entry.Metadata.Id]);
        return new FileSystemMutationResult(transaction, entry);
    }

    /// <summary>Reads a settings document's current revision for an unconditional write.</summary>
    private async Task<long> ResolveCurrentRevisionAsync(
        VirtualPath path, FileSystemAuthorizationContext context, CancellationToken cancellationToken)
    {
        SettingsReadResult read = await _projection.ReadFileAsync(path, context.OperationContext, cancellationToken);
        return read.Document?.Revision ?? 1;
    }

    /// <inheritdoc />
    public ValueTask<FileSystemMutationResult> MoveAsync(FileSystemMoveRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Rejected(FileSystemOperation.Move, FileSystemErrorCode.ProtectedEntry, request.SourcePath, request.DestinationPath));

    /// <inheritdoc />
    public ValueTask<FileSystemMutationResult> CopyAsync(FileSystemCopyRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Rejected(FileSystemOperation.Copy, FileSystemErrorCode.ProtectedEntry, request.SourcePath, request.DestinationPath));

    /// <inheritdoc />
    public ValueTask<FileSystemMutationResult> DeleteAsync(FileSystemDeleteRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Rejected(FileSystemOperation.Delete, FileSystemErrorCode.ProtectedEntry, request.Path));

    /// <inheritdoc />
    public async ValueTask<FileSystemResult<FileSystemEntrySnapshot>> StatAsync(
        FileSystemStatRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        if (IsSyntheticDirectory(request.Path))
        {
            return FileSystemResult<FileSystemEntrySnapshot>.Success(
                new FileSystemEntrySnapshot(request.Path, DirectoryMetadataFor(request.Path)));
        }

        if (!_definitions.ContainsKey(request.Path.Value))
        {
            return FileSystemResult<FileSystemEntrySnapshot>.Failure(
                Error(FileSystemOperation.Stat, FileSystemErrorCode.NotFound, request.Path));
        }

        SettingsReadResult read = await _projection.ReadFileAsync(
            request.Path,
            context.OperationContext,
            cancellationToken);
        return read.Status == SettingsReadStatus.Success
            ? FileSystemResult<FileSystemEntrySnapshot>.Success(
                FileSnapshot(read.Document!, StrictUtf8.GetByteCount(read.Document!.Content)))
            : FileSystemResult<FileSystemEntrySnapshot>.Failure(
                Error(FileSystemOperation.Stat, Map(read.Status), request.Path));
    }

    /// <inheritdoc />
    public ValueTask<FileSystemMutationResult> SetPermissionsAsync(FileSystemSetPermissionsRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Rejected(FileSystemOperation.SetPermissions, FileSystemErrorCode.ProtectedEntry, request.Path));

    private FileSystemEntrySnapshot FileSnapshot(SettingsDocumentSnapshot document, long length)
    {
        FileSystemTimestamps timestamps = new(_createdAtUtc, _createdAtUtc, _createdAtUtc);
        FileMetadata metadata = new(
            StableId(document.Path),
            "system",
            "administrators",
            FileSystemPermissions.FromMode(0x01B4),
            timestamps,
            document.Revision,
            length,
            document.MediaType);
        return new FileSystemEntrySnapshot(document.Path, metadata);
    }

    private DirectoryMetadata DirectoryMetadataFor(VirtualPath path)
    {
        FileSystemTimestamps timestamps = new(_createdAtUtc, _createdAtUtc, _createdAtUtc);
        return new DirectoryMetadata(
            StableId(path),
            "system",
            "administrators",
            FileSystemPermissions.FromMode(0x01FD),
            timestamps,
            1);
    }

    private bool IsSyntheticDirectory(VirtualPath path) =>
        path == _mountPath
        || _definitions.Keys.Any(candidate => candidate.StartsWith($"{path.Value}/", StringComparison.Ordinal));

    private static bool IsWithin(VirtualPath path, VirtualPath root) =>
        path == root || path.Value.StartsWith($"{root.Value}/", StringComparison.Ordinal);

    private static FileSystemEntryId StableId(VirtualPath path)
    {
        byte[] hash = SHA256.HashData(StrictUtf8.GetBytes(path.Value));
        return FileSystemEntryId.FromGuid(new Guid(hash.AsSpan(0, 16)));
    }

    private FileSystemMutationResult Rejected(
        FileSystemOperation operation,
        FileSystemErrorCode code,
        VirtualPath path,
        VirtualPath? relatedPath = null)
    {
        FileSystemError error = Error(operation, code, path, relatedPath);
        return new FileSystemMutationResult(FileSystemTransactionResult.Rejected(NextTransactionId(), error));
    }

    private FileSystemMutationResult Cancelled(FileSystemOperation operation, VirtualPath path)
    {
        FileSystemError error = Error(operation, FileSystemErrorCode.Cancelled, path);
        return new FileSystemMutationResult(FileSystemTransactionResult.Cancelled(NextTransactionId(), error));
    }

    private static FileSystemError Error(FileSystemOperation operation, FileSystemErrorCode code, VirtualPath path, VirtualPath? relatedPath = null) =>
        new(operation, code, path, relatedPath);

    private Guid NextTransactionId()
    {
        Guid id = _transactionIdFactory();
        return id == Guid.Empty
            ? throw new InvalidOperationException("The transaction ID factory returned an empty ID.")
            : id;
    }

    private static FileSystemErrorCode Map(SettingsReadStatus status) => status switch
    {
        SettingsReadStatus.NotFound => FileSystemErrorCode.NotFound,
        SettingsReadStatus.Denied => FileSystemErrorCode.CapabilityDenied,
        _ => FileSystemErrorCode.ProviderFailure
    };

    private static FileSystemErrorCode Map(SettingsWriteStatus status) => status switch
    {
        SettingsWriteStatus.NotFound => FileSystemErrorCode.NotFound,
        SettingsWriteStatus.Denied => FileSystemErrorCode.CapabilityDenied,
        SettingsWriteStatus.Invalid => FileSystemErrorCode.InvalidContent,
        SettingsWriteStatus.Conflict => FileSystemErrorCode.RevisionConflict,
        _ => FileSystemErrorCode.ProviderFailure
    };
}