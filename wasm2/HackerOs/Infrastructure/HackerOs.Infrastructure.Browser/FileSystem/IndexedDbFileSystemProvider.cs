using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;
using Microsoft.JSInterop;

namespace HackerOs.Infrastructure.Browser.FileSystem;

/// <summary>Provides persistent browser filesystem operations backed by IndexedDB.</summary>
public sealed class IndexedDbFileSystemProvider : IFileSystemProvider, IAsyncDisposable
{
    private readonly IndexedDbFileSystemReader _reader;
    private readonly IndexedDbFileSystemWriter _writer;
    private readonly IndexedDbFileContentRepository _contentRepository;
    private readonly TimeProvider _timeProvider;
    private readonly Func<FileSystemEntryId> _entryIdFactory;
    private readonly Func<Guid> _transactionIdFactory;

    /// <summary>Initializes the persistent browser filesystem provider.</summary>
    public IndexedDbFileSystemProvider(IJSRuntime runtime)
        : this(runtime, TimeProvider.System, null, null)
    {
    }

    internal IndexedDbFileSystemProvider(
        IJSRuntime runtime,
        TimeProvider timeProvider,
        Func<FileSystemEntryId>? entryIdFactory,
        Func<Guid>? transactionIdFactory)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _reader = new IndexedDbFileSystemReader(runtime);
        _writer = new IndexedDbFileSystemWriter(runtime);
        _contentRepository = new IndexedDbFileContentRepository(runtime, timeProvider: timeProvider);
        _timeProvider = timeProvider;
        _entryIdFactory = entryIdFactory ?? (() => FileSystemEntryId.FromGuid(Guid.NewGuid()));
        _transactionIdFactory = transactionIdFactory ?? Guid.NewGuid;
    }

    /// <inheritdoc />
    public string ProviderId => "indexeddb";

    /// <inheritdoc />
    public async ValueTask<FileSystemResult<FileSystemContentReadHandle>> ReadAsync(
        FileSystemReadRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (cancellationToken.IsCancellationRequested)
        {
            return FileSystemResult<FileSystemContentReadHandle>.Failure(
                Error(FileSystemOperation.Read, FileSystemErrorCode.Cancelled, request.Path));
        }

        try
        {
            IndexedDbFileSystemEntryRecord? entry =
                await _reader.ResolveAsync(request.Path, cancellationToken).ConfigureAwait(false);
            if (entry is null)
            {
                return FileSystemResult<FileSystemContentReadHandle>.Failure(
                    Error(FileSystemOperation.Read, FileSystemErrorCode.NotFound, request.Path));
            }

            if (entry.Kind != (int)FileSystemEntryKind.File)
            {
                return FileSystemResult<FileSystemContentReadHandle>.Failure(
                    Error(FileSystemOperation.Read, FileSystemErrorCode.NotFile, request.Path));
            }

            if (entry.ContentHash is null)
            {
                return FileSystemResult<FileSystemContentReadHandle>.Failure(
                    Error(FileSystemOperation.Read, FileSystemErrorCode.ProviderFailure, request.Path));
            }

            Stream content = await _contentRepository.ReadAsync(
                entry.ContentHash,
                entry.Length,
                cancellationToken).ConfigureAwait(false);
            FileSystemContentDescriptor descriptor = (FileSystemContentKind)entry.ContentKind switch
            {
                FileSystemContentKind.Binary => FileSystemContentDescriptor.Binary(entry.MediaType),
                FileSystemContentKind.Text => FileSystemContentDescriptor.Text(
                    entry.MediaType,
                    entry.EncodingName ?? throw new InvalidDataException("Persisted text content has no encoding.")),
                _ => throw new InvalidDataException("Persisted file content kind is unknown.")
            };
            return FileSystemResult<FileSystemContentReadHandle>.Success(new FileSystemContentReadHandle(
                new FileSystemEntrySnapshot(request.Path, entry.ToMetadata()),
                descriptor,
                content));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return FileSystemResult<FileSystemContentReadHandle>.Failure(
                Error(FileSystemOperation.Read, FileSystemErrorCode.Cancelled, request.Path));
        }
        catch (InvalidDataException)
        {
            return FileSystemResult<FileSystemContentReadHandle>.Failure(
                Error(FileSystemOperation.Read, FileSystemErrorCode.ProviderFailure, request.Path));
        }
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemResult<FileSystemDirectorySnapshot>> EnumerateAsync(
        FileSystemEnumerateRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (cancellationToken.IsCancellationRequested)
        {
            return FileSystemResult<FileSystemDirectorySnapshot>.Failure(
                Error(FileSystemOperation.Enumerate, FileSystemErrorCode.Cancelled, request.Path));
        }

        try
        {
            IndexedDbFileSystemDirectoryRead? read =
                await _reader.ReadDirectoryAsync(request.Path, cancellationToken).ConfigureAwait(false);
            if (read is null)
            {
                return FileSystemResult<FileSystemDirectorySnapshot>.Failure(
                    Error(FileSystemOperation.Enumerate, FileSystemErrorCode.NotFound, request.Path));
            }

            FileSystemDirectoryItem[] items =
                [.. read.Children.Select(child => new FileSystemDirectoryItem(child.Name, child.Entry.ToMetadata()))];
            return FileSystemResult<FileSystemDirectorySnapshot>.Success(
                new FileSystemDirectorySnapshot(request.Path, read.Directory.Revision, items));
        }
        catch (InvalidDataException)
        {
            return FileSystemResult<FileSystemDirectorySnapshot>.Failure(
                Error(FileSystemOperation.Enumerate, FileSystemErrorCode.ProviderFailure, request.Path));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return FileSystemResult<FileSystemDirectorySnapshot>.Failure(
                Error(FileSystemOperation.Enumerate, FileSystemErrorCode.Cancelled, request.Path));
        }
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> CreateAsync(
        FileSystemCreateRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (cancellationToken.IsCancellationRequested)
        {
            return MutationFailure(FileSystemOperation.Create, FileSystemErrorCode.Cancelled, request.Path);
        }

        if (request.Path.Value == "/")
        {
            return MutationFailure(FileSystemOperation.Create, FileSystemErrorCode.ProtectedEntry, request.Path);
        }

        try
        {
            if (await _reader.ResolveAsync(request.Path, cancellationToken).ConfigureAwait(false) is not null)
            {
                return MutationFailure(FileSystemOperation.Create, FileSystemErrorCode.AlreadyExists, request.Path);
            }

            VirtualPath parentPath = ParentOf(request.Path);
            IndexedDbFileSystemEntryRecord? parent =
                await _reader.ResolveAsync(parentPath, cancellationToken).ConfigureAwait(false);
            if (parent is null)
            {
                return MutationFailure(FileSystemOperation.Create, FileSystemErrorCode.NotFound, parentPath);
            }

            if (parent.Kind != (int)FileSystemEntryKind.Directory)
            {
                return MutationFailure(FileSystemOperation.Create, FileSystemErrorCode.NotDirectory, parentPath);
            }

            if (request.ExpectedParentRevision is { } expectedParentRevision && parent.Revision != expectedParentRevision)
            {
                return MutationFailure(FileSystemOperation.Create, FileSystemErrorCode.RevisionConflict, parentPath);
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            FileSystemEntryMetadata created = CreateMetadata(request, context, parent, now);
            await _writer.CreateAsync(
                parent,
                created,
                FileSystemEntryName.Parse(NameOf(request.Path)),
                now,
                cancellationToken).ConfigureAwait(false);
            return new FileSystemMutationResult(
                FileSystemTransactionResult.Committed(_transactionIdFactory(), [created.Id]),
                new FileSystemEntrySnapshot(request.Path, created));
        }
        catch (JSException exception) when (
            exception.Message.Contains("filesystem.revision-conflict", StringComparison.Ordinal))
        {
            return MutationFailure(FileSystemOperation.Create, FileSystemErrorCode.RevisionConflict, ParentOf(request.Path));
        }
        catch (JSException exception) when (
            exception.Message.Contains("ConstraintError", StringComparison.Ordinal))
        {
            return MutationFailure(FileSystemOperation.Create, FileSystemErrorCode.AlreadyExists, request.Path);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return MutationFailure(FileSystemOperation.Create, FileSystemErrorCode.Cancelled, request.Path);
        }
        catch (InvalidDataException)
        {
            return MutationFailure(FileSystemOperation.Create, FileSystemErrorCode.ProviderFailure, request.Path);
        }
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> WriteAsync(
        FileSystemWriteRequest request,
        IFileSystemContentSource content,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(context);
        if (cancellationToken.IsCancellationRequested)
        {
            return MutationFailure(FileSystemOperation.Write, FileSystemErrorCode.Cancelled, request.Path);
        }

        try
        {
            IndexedDbFileSystemEntryRecord? entry =
                await _reader.ResolveAsync(request.Path, cancellationToken).ConfigureAwait(false);
            if (entry is null)
            {
                return MutationFailure(FileSystemOperation.Write, FileSystemErrorCode.NotFound, request.Path);
            }

            if (entry.Kind != (int)FileSystemEntryKind.File)
            {
                return MutationFailure(FileSystemOperation.Write, FileSystemErrorCode.NotFile, request.Path);
            }

            if (request.ExpectedRevision is { } expectedRevision && entry.Revision != expectedRevision)
            {
                return MutationFailure(FileSystemOperation.Write, FileSystemErrorCode.RevisionConflict, request.Path);
            }

            await using Stream source = await content.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            IndexedDbFileContentWrite persisted =
                await _contentRepository.WriteAsync(source, cancellationToken).ConfigureAwait(false);
            DateTimeOffset now = _timeProvider.GetUtcNow();
            await _writer.WriteAsync(
                entry,
                persisted.ContentHash,
                persisted.Length,
                content.Descriptor,
                now,
                cancellationToken).ConfigureAwait(false);
            IndexedDbFileSystemEntryRecord updated = entry with
            {
                ContentModifiedUtcMs = now.ToUnixTimeMilliseconds(),
                MetadataChangedUtcMs = now.ToUnixTimeMilliseconds(),
                Revision = checked(entry.Revision + 1),
                Length = persisted.Length,
                ContentHash = persisted.ContentHash,
                ContentKind = (int)content.Descriptor.Kind,
                MediaType = content.Descriptor.MediaType,
                EncodingName = content.Descriptor.EncodingName
            };
            return new FileSystemMutationResult(
                FileSystemTransactionResult.Committed(
                    _transactionIdFactory(),
                    [FileSystemEntryId.Parse(updated.Id)]),
                new FileSystemEntrySnapshot(request.Path, updated.ToMetadata()));
        }
        catch (JSException exception) when (
            exception.Message.Contains("filesystem.revision-conflict", StringComparison.Ordinal))
        {
            return MutationFailure(FileSystemOperation.Write, FileSystemErrorCode.RevisionConflict, request.Path);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return MutationFailure(FileSystemOperation.Write, FileSystemErrorCode.Cancelled, request.Path);
        }
        catch (InvalidDataException)
        {
            return MutationFailure(FileSystemOperation.Write, FileSystemErrorCode.ProviderFailure, request.Path);
        }
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> MoveAsync(
        FileSystemMoveRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (cancellationToken.IsCancellationRequested)
        {
            return MutationFailure(
                FileSystemOperation.Move,
                FileSystemErrorCode.Cancelled,
                request.SourcePath,
                request.DestinationPath);
        }

        if (request.SourcePath.Value == "/")
        {
            return MutationFailure(
                FileSystemOperation.Move,
                FileSystemErrorCode.ProtectedEntry,
                request.SourcePath,
                request.DestinationPath);
        }

        try
        {
            IndexedDbFileSystemEntryRecord? source =
                await _reader.ResolveAsync(request.SourcePath, cancellationToken).ConfigureAwait(false);
            if (source is null)
            {
                return MutationFailure(
                    FileSystemOperation.Move,
                    FileSystemErrorCode.NotFound,
                    request.SourcePath,
                    request.DestinationPath);
            }

            if (await _reader.ResolveAsync(request.DestinationPath, cancellationToken).ConfigureAwait(false) is not null)
            {
                return MutationFailure(
                    FileSystemOperation.Move,
                    FileSystemErrorCode.AlreadyExists,
                    request.SourcePath,
                    request.DestinationPath);
            }

            if (source.Kind == (int)FileSystemEntryKind.Directory
                && IsWithin(request.DestinationPath, request.SourcePath))
            {
                return MutationFailure(
                    FileSystemOperation.Move,
                    FileSystemErrorCode.InvalidPath,
                    request.SourcePath,
                    request.DestinationPath);
            }

            VirtualPath sourceParentPath = ParentOf(request.SourcePath);
            VirtualPath destinationParentPath = ParentOf(request.DestinationPath);
            IndexedDbFileSystemEntryRecord? sourceParent =
                await _reader.ResolveAsync(sourceParentPath, cancellationToken).ConfigureAwait(false);
            IndexedDbFileSystemEntryRecord? destinationParent = sourceParentPath == destinationParentPath
                ? sourceParent
                : await _reader.ResolveAsync(destinationParentPath, cancellationToken).ConfigureAwait(false);
            if (sourceParent is null || destinationParent is null)
            {
                return MutationFailure(
                    FileSystemOperation.Move,
                    FileSystemErrorCode.NotFound,
                    destinationParent is null ? destinationParentPath : sourceParentPath,
                    request.DestinationPath);
            }

            if (sourceParent.Kind != (int)FileSystemEntryKind.Directory
                || destinationParent.Kind != (int)FileSystemEntryKind.Directory)
            {
                return MutationFailure(
                    FileSystemOperation.Move,
                    FileSystemErrorCode.NotDirectory,
                    destinationParent.Kind != (int)FileSystemEntryKind.Directory
                        ? destinationParentPath
                        : sourceParentPath,
                    request.DestinationPath);
            }

            if (source.Revision != request.ExpectedEntryRevision
                || sourceParent.Revision != request.ExpectedSourceParentRevision
                || destinationParent.Revision != request.ExpectedDestinationParentRevision)
            {
                return MutationFailure(
                    FileSystemOperation.Move,
                    FileSystemErrorCode.RevisionConflict,
                    request.SourcePath,
                    request.DestinationPath);
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            await _writer.MoveAsync(
                source,
                sourceParent,
                FileSystemEntryName.Parse(NameOf(request.SourcePath)),
                destinationParent,
                FileSystemEntryName.Parse(NameOf(request.DestinationPath)),
                now,
                cancellationToken).ConfigureAwait(false);
            IndexedDbFileSystemEntryRecord updated = source with
            {
                MetadataChangedUtcMs = now.ToUnixTimeMilliseconds(),
                Revision = checked(source.Revision + 1)
            };
            return new FileSystemMutationResult(
                FileSystemTransactionResult.Committed(
                    _transactionIdFactory(),
                    [FileSystemEntryId.Parse(updated.Id)]),
                new FileSystemEntrySnapshot(request.DestinationPath, updated.ToMetadata()));
        }
        catch (JSException exception) when (
            exception.Message.Contains("filesystem.revision-conflict", StringComparison.Ordinal))
        {
            return MutationFailure(
                FileSystemOperation.Move,
                FileSystemErrorCode.RevisionConflict,
                request.SourcePath,
                request.DestinationPath);
        }
        catch (JSException exception) when (
            exception.Message.Contains("ConstraintError", StringComparison.Ordinal))
        {
            return MutationFailure(
                FileSystemOperation.Move,
                FileSystemErrorCode.AlreadyExists,
                request.SourcePath,
                request.DestinationPath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return MutationFailure(
                FileSystemOperation.Move,
                FileSystemErrorCode.Cancelled,
                request.SourcePath,
                request.DestinationPath);
        }
        catch (InvalidDataException)
        {
            return MutationFailure(
                FileSystemOperation.Move,
                FileSystemErrorCode.ProviderFailure,
                request.SourcePath,
                request.DestinationPath);
        }
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> CopyAsync(
        FileSystemCopyRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (cancellationToken.IsCancellationRequested)
        {
            return MutationFailure(
                FileSystemOperation.Copy,
                FileSystemErrorCode.Cancelled,
                request.SourcePath,
                request.DestinationPath);
        }

        try
        {
            IndexedDbFileSystemEntryRecord? source =
                await _reader.ResolveAsync(request.SourcePath, cancellationToken).ConfigureAwait(false);
            if (source is null)
            {
                return MutationFailure(FileSystemOperation.Copy, FileSystemErrorCode.NotFound, request.SourcePath);
            }

            if (source.Revision != request.ExpectedEntryRevision)
            {
                return MutationFailure(
                    FileSystemOperation.Copy,
                    FileSystemErrorCode.RevisionConflict,
                    request.SourcePath,
                    request.DestinationPath);
            }

            if (await _reader.ResolveAsync(request.DestinationPath, cancellationToken).ConfigureAwait(false) is not null)
            {
                return MutationFailure(
                    FileSystemOperation.Copy,
                    FileSystemErrorCode.AlreadyExists,
                    request.SourcePath,
                    request.DestinationPath);
            }

            if (source.Kind == (int)FileSystemEntryKind.Directory
                && IsWithin(request.DestinationPath, request.SourcePath))
            {
                return MutationFailure(
                    FileSystemOperation.Copy,
                    FileSystemErrorCode.InvalidPath,
                    request.SourcePath,
                    request.DestinationPath);
            }

            VirtualPath destinationParentPath = ParentOf(request.DestinationPath);
            IndexedDbFileSystemEntryRecord? destinationParent =
                await _reader.ResolveAsync(destinationParentPath, cancellationToken).ConfigureAwait(false);
            if (destinationParent is null)
            {
                return MutationFailure(FileSystemOperation.Copy, FileSystemErrorCode.NotFound, destinationParentPath);
            }

            if (destinationParent.Kind != (int)FileSystemEntryKind.Directory)
            {
                return MutationFailure(FileSystemOperation.Copy, FileSystemErrorCode.NotDirectory, destinationParentPath);
            }

            if (destinationParent.Revision != request.ExpectedDestinationParentRevision)
            {
                return MutationFailure(
                    FileSystemOperation.Copy,
                    FileSystemErrorCode.RevisionConflict,
                    destinationParentPath,
                    request.DestinationPath);
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            List<IndexedDbFileSystemCopyEntry> copies = [];
            IndexedDbFileSystemEntryRecord copiedRoot = CopyRecord(source, now);
            copies.Add(new IndexedDbFileSystemCopyEntry(
                source,
                copiedRoot,
                destinationParent.Id,
                FileSystemEntryName.Parse(NameOf(request.DestinationPath))));
            if (source.Kind == (int)FileSystemEntryKind.Directory)
            {
                await CollectCopiesAsync(source, copiedRoot.Id, copies, now, cancellationToken).ConfigureAwait(false);
            }

            await _writer.CopyAsync(destinationParent, copies, now, cancellationToken).ConfigureAwait(false);
            return new FileSystemMutationResult(
                FileSystemTransactionResult.Committed(
                    _transactionIdFactory(),
                    copies.Select(copy => FileSystemEntryId.Parse(copy.Copy.Id))),
                new FileSystemEntrySnapshot(request.DestinationPath, copiedRoot.ToMetadata()));
        }
        catch (JSException exception) when (
            exception.Message.Contains("filesystem.revision-conflict", StringComparison.Ordinal))
        {
            return MutationFailure(
                FileSystemOperation.Copy,
                FileSystemErrorCode.RevisionConflict,
                request.SourcePath,
                request.DestinationPath);
        }
        catch (JSException exception) when (
            exception.Message.Contains("ConstraintError", StringComparison.Ordinal))
        {
            return MutationFailure(
                FileSystemOperation.Copy,
                FileSystemErrorCode.AlreadyExists,
                request.SourcePath,
                request.DestinationPath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return MutationFailure(
                FileSystemOperation.Copy,
                FileSystemErrorCode.Cancelled,
                request.SourcePath,
                request.DestinationPath);
        }
        catch (InvalidDataException)
        {
            return MutationFailure(
                FileSystemOperation.Copy,
                FileSystemErrorCode.ProviderFailure,
                request.SourcePath,
                request.DestinationPath);
        }
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> DeleteAsync(
        FileSystemDeleteRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (cancellationToken.IsCancellationRequested)
        {
            return MutationFailure(FileSystemOperation.Delete, FileSystemErrorCode.Cancelled, request.Path);
        }

        if (request.Path.Value == "/")
        {
            return MutationFailure(FileSystemOperation.Delete, FileSystemErrorCode.ProtectedEntry, request.Path);
        }

        try
        {
            IndexedDbFileSystemEntryRecord? target =
                await _reader.ResolveAsync(request.Path, cancellationToken).ConfigureAwait(false);
            if (target is null)
            {
                return MutationFailure(FileSystemOperation.Delete, FileSystemErrorCode.NotFound, request.Path);
            }

            VirtualPath parentPath = ParentOf(request.Path);
            IndexedDbFileSystemEntryRecord? parent =
                await _reader.ResolveAsync(parentPath, cancellationToken).ConfigureAwait(false);
            if (parent is null)
            {
                return MutationFailure(FileSystemOperation.Delete, FileSystemErrorCode.NotFound, parentPath);
            }

            if (target.Revision != request.ExpectedEntryRevision
                || parent.Revision != request.ExpectedParentRevision)
            {
                return MutationFailure(FileSystemOperation.Delete, FileSystemErrorCode.RevisionConflict, request.Path);
            }

            List<IndexedDbFileSystemDeletionEntry> removals =
            [
                new(parent.Id, FileSystemEntryName.Parse(NameOf(request.Path)), target)
            ];
            if (target.Kind == (int)FileSystemEntryKind.Directory)
            {
                IndexedDbFileSystemDirectoryRead directory =
                    await _reader.ReadChildrenAsync(target, cancellationToken).ConfigureAwait(false);
                if (directory.Children.Count > 0 && !request.Recursive)
                {
                    return MutationFailure(
                        FileSystemOperation.Delete,
                        FileSystemErrorCode.DirectoryNotEmpty,
                        request.Path);
                }

                if (request.Recursive)
                {
                    await CollectDescendantsAsync(
                        directory,
                        removals,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            await _writer.DeleteAsync(
                parent,
                removals,
                _timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
            return new FileSystemMutationResult(FileSystemTransactionResult.Committed(
                _transactionIdFactory(),
                removals.Select(removal => FileSystemEntryId.Parse(removal.Entry.Id))));
        }
        catch (JSException exception) when (
            exception.Message.Contains("filesystem.revision-conflict", StringComparison.Ordinal))
        {
            return MutationFailure(FileSystemOperation.Delete, FileSystemErrorCode.RevisionConflict, request.Path);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return MutationFailure(FileSystemOperation.Delete, FileSystemErrorCode.Cancelled, request.Path);
        }
        catch (InvalidDataException)
        {
            return MutationFailure(FileSystemOperation.Delete, FileSystemErrorCode.ProviderFailure, request.Path);
        }
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemResult<FileSystemEntrySnapshot>> StatAsync(
        FileSystemStatRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (cancellationToken.IsCancellationRequested)
        {
            return FileSystemResult<FileSystemEntrySnapshot>.Failure(
                Error(FileSystemOperation.Stat, FileSystemErrorCode.Cancelled, request.Path));
        }

        try
        {
            IndexedDbFileSystemEntryRecord? entry =
                await _reader.ResolveAsync(request.Path, cancellationToken).ConfigureAwait(false);
            return entry is null
                ? FileSystemResult<FileSystemEntrySnapshot>.Failure(
                    Error(FileSystemOperation.Stat, FileSystemErrorCode.NotFound, request.Path))
                : FileSystemResult<FileSystemEntrySnapshot>.Success(
                    new FileSystemEntrySnapshot(request.Path, entry.ToMetadata()));
        }
        catch (InvalidDataException)
        {
            return FileSystemResult<FileSystemEntrySnapshot>.Failure(
                Error(FileSystemOperation.Stat, FileSystemErrorCode.ProviderFailure, request.Path));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return FileSystemResult<FileSystemEntrySnapshot>.Failure(
                Error(FileSystemOperation.Stat, FileSystemErrorCode.Cancelled, request.Path));
        }
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> SetPermissionsAsync(
        FileSystemSetPermissionsRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (cancellationToken.IsCancellationRequested)
        {
            return MutationFailure(FileSystemOperation.SetPermissions, FileSystemErrorCode.Cancelled, request.Path);
        }

        try
        {
            IndexedDbFileSystemEntryRecord? entry =
                await _reader.ResolveAsync(request.Path, cancellationToken).ConfigureAwait(false);
            if (entry is null)
            {
                return MutationFailure(FileSystemOperation.SetPermissions, FileSystemErrorCode.NotFound, request.Path);
            }

            if (entry.Revision != request.ExpectedRevision)
            {
                return MutationFailure(
                    FileSystemOperation.SetPermissions,
                    FileSystemErrorCode.RevisionConflict,
                    request.Path);
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            await _writer.SetPermissionsAsync(
                entry,
                request.Permissions,
                now,
                cancellationToken).ConfigureAwait(false);
            IndexedDbFileSystemEntryRecord updated = entry with
            {
                PermissionsMode = request.Permissions.Mode,
                MetadataChangedUtcMs = now.ToUnixTimeMilliseconds(),
                Revision = checked(entry.Revision + 1)
            };
            return new FileSystemMutationResult(
                FileSystemTransactionResult.Committed(
                    _transactionIdFactory(),
                    [FileSystemEntryId.Parse(updated.Id)]),
                new FileSystemEntrySnapshot(request.Path, updated.ToMetadata()));
        }
        catch (JSException exception) when (
            exception.Message.Contains("filesystem.revision-conflict", StringComparison.Ordinal))
        {
            return MutationFailure(
                FileSystemOperation.SetPermissions,
                FileSystemErrorCode.RevisionConflict,
                request.Path);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return MutationFailure(FileSystemOperation.SetPermissions, FileSystemErrorCode.Cancelled, request.Path);
        }
        catch (InvalidDataException)
        {
            return MutationFailure(FileSystemOperation.SetPermissions, FileSystemErrorCode.ProviderFailure, request.Path);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _reader.DisposeAsync().ConfigureAwait(false);
        await _writer.DisposeAsync().ConfigureAwait(false);
        await _contentRepository.DisposeAsync().ConfigureAwait(false);
    }

    private FileSystemMutationResult MutationFailure(
        FileSystemOperation operation,
        FileSystemErrorCode code,
        VirtualPath path,
        VirtualPath? relatedPath = null)
    {
        FileSystemError error = new(operation, code, path, relatedPath);
        FileSystemTransactionResult transaction = code == FileSystemErrorCode.Cancelled
            ? FileSystemTransactionResult.Cancelled(_transactionIdFactory(), error)
            : FileSystemTransactionResult.Rejected(_transactionIdFactory(), error);
        return new FileSystemMutationResult(transaction);
    }

    private FileSystemEntryMetadata CreateMetadata(
        FileSystemCreateRequest request,
        FileSystemAuthorizationContext context,
        IndexedDbFileSystemEntryRecord parent,
        DateTimeOffset now)
    {
        FileSystemEntryId id = _entryIdFactory();
        FileSystemTimestamps timestamps = new(now, now, now);
        string ownerId = context.OperationContext.UserId;
        string groupId = parent.GroupId;
        return request.Kind switch
        {
            FileSystemEntryKind.File => new FileMetadata(id, ownerId, groupId, request.Permissions, timestamps, 1, 0),
            FileSystemEntryKind.Directory => new DirectoryMetadata(id, ownerId, groupId, request.Permissions, timestamps, 1),
            FileSystemEntryKind.SymbolicLink => new SymbolicLinkMetadata(
                id, ownerId, groupId, request.Permissions, timestamps, 1, request.SymbolicLinkTarget!),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
    }

    private async ValueTask CollectDescendantsAsync(
        IndexedDbFileSystemDirectoryRead root,
        List<IndexedDbFileSystemDeletionEntry> removals,
        CancellationToken cancellationToken)
    {
        Queue<IndexedDbFileSystemDirectoryRead> pending = new();
        pending.Enqueue(root);
        while (pending.TryDequeue(out IndexedDbFileSystemDirectoryRead? directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach ((FileSystemEntryName name, IndexedDbFileSystemEntryRecord entry) in directory.Children)
            {
                removals.Add(new IndexedDbFileSystemDeletionEntry(directory.Directory.Id, name, entry));
                if (entry.Kind == (int)FileSystemEntryKind.Directory)
                {
                    pending.Enqueue(await _reader.ReadChildrenAsync(entry, cancellationToken).ConfigureAwait(false));
                }
            }
        }
    }

    private async ValueTask CollectCopiesAsync(
        IndexedDbFileSystemEntryRecord sourceRoot,
        string copiedRootId,
        List<IndexedDbFileSystemCopyEntry> copies,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Queue<(IndexedDbFileSystemEntryRecord Source, string CopyId)> pending = new();
        pending.Enqueue((sourceRoot, copiedRootId));
        while (pending.TryDequeue(out (IndexedDbFileSystemEntryRecord Source, string CopyId) directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            IndexedDbFileSystemDirectoryRead read =
                await _reader.ReadChildrenAsync(directory.Source, cancellationToken).ConfigureAwait(false);
            foreach ((FileSystemEntryName name, IndexedDbFileSystemEntryRecord source) in read.Children)
            {
                IndexedDbFileSystemEntryRecord copy = CopyRecord(source, now);
                copies.Add(new IndexedDbFileSystemCopyEntry(source, copy, directory.CopyId, name));
                if (source.Kind == (int)FileSystemEntryKind.Directory)
                {
                    pending.Enqueue((source, copy.Id));
                }
            }
        }
    }

    private IndexedDbFileSystemEntryRecord CopyRecord(
        IndexedDbFileSystemEntryRecord source,
        DateTimeOffset now) => source with
        {
            Id = _entryIdFactory().ToString(),
            CreatedUtcMs = now.ToUnixTimeMilliseconds(),
            ContentModifiedUtcMs = now.ToUnixTimeMilliseconds(),
            MetadataChangedUtcMs = now.ToUnixTimeMilliseconds(),
            Revision = 1
        };

    private static VirtualPath ParentOf(VirtualPath path)
    {
        int separator = path.Value.LastIndexOf('/');
        return separator <= 0 ? VirtualPath.Parse("/") : VirtualPath.Parse(path.Value[..separator]);
    }

    private static string NameOf(VirtualPath path) => path.Value[(path.Value.LastIndexOf('/') + 1)..];

    private static bool IsWithin(VirtualPath candidate, VirtualPath root) =>
        candidate == root || candidate.Value.StartsWith($"{root.Value}/", StringComparison.Ordinal);

    private static FileSystemMutationResult UnsupportedMutation(
        FileSystemOperation operation,
        VirtualPath path,
        CancellationToken cancellationToken)
    {
        FileSystemError error = Error(operation, UnsupportedOrCancelled(cancellationToken), path);
        FileSystemTransactionResult transaction = cancellationToken.IsCancellationRequested
            ? FileSystemTransactionResult.Cancelled(Guid.NewGuid(), error)
            : FileSystemTransactionResult.Rejected(Guid.NewGuid(), error);
        return new FileSystemMutationResult(transaction);
    }

    private static FileSystemErrorCode UnsupportedOrCancelled(CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested
            ? FileSystemErrorCode.Cancelled
            : FileSystemErrorCode.UnsupportedOperation;

    private static FileSystemError Error(
        FileSystemOperation operation,
        FileSystemErrorCode code,
        VirtualPath path) => new(operation, code, path);
}
