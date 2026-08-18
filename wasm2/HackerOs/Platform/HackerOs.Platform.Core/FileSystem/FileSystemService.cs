using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Events;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Platform.Core.FileSystem;

/// <summary>Coordinates traversal, authorization, mounts, and provider operations.</summary>
/// <remarks>
/// Publishes a <see cref="FileSystemChangeEvent"/> to <see cref="FileSystemTopics.ForDirectory"/> of the
/// affected entry's parent after every successful mutation, per
/// <c>docs/adr/0038-emitter-authorized-topic-messaging.md</c> and
/// <c>docs/Global-FileView-And-MessagingSystem/MessagingSystem.md</c> (<c>MSG-011</c>/<c>MSG-012</c>).
/// The <c>shared/filesystem/changed</c> channel is registered once, in this singleton's constructor,
/// owner-only for publish under <see cref="KernelPublisherIdentity"/> so no app can ever forge a change
/// notification.
/// </remarks>
public sealed class FileSystemService : IFileSystemService
{
    private readonly IFileSystemMountRouter _router;
    private readonly IFileSystemPathResolver _pathResolver;
    private readonly IFileSystemAuthorizer _authorizer;
    private readonly ITopicMessageBus _topicBus;
    private readonly Func<Guid> _transactionIdFactory;
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>Initializes the mounted filesystem service.</summary>
    public FileSystemService(
        IFileSystemMountRouter router,
        IFileSystemPathResolver pathResolver,
        IFileSystemAuthorizer authorizer,
        ITopicMessageBus topicBus,
        Func<Guid>? transactionIdFactory = null,
        Func<DateTimeOffset>? clock = null)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        _topicBus = topicBus ?? throw new ArgumentNullException(nameof(topicBus));
        _transactionIdFactory = transactionIdFactory ?? Guid.NewGuid;
        _clock = clock ?? (static () => DateTimeOffset.UtcNow);

        _topicBus.RegisterSharedChannel(
            TopicNames.Shared("filesystem").Segment("changed").Build(),
            new SharedChannelPolicy(SharedChannelAccessMode.OwnerOnly, SharedChannelAccessMode.Open),
            KernelPublisherIdentity.Value);
    }

    /// <inheritdoc />
    public string ProviderId => "mounted-filesystem";

    /// <inheritdoc />
    public async ValueTask<FileSystemResult<FileSystemContentReadHandle>> ReadAsync(
        FileSystemReadRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        FileSystemResult<FileSystemPathResolution> resolution = await ResolveAsync(
            request.Path,
            request.LinkBehavior,
            FileSystemOperation.Read,
            context,
            cancellationToken);
        if (!resolution.Succeeded)
        {
            return FileSystemResult<FileSystemContentReadHandle>.Failure(resolution.Error!);
        }

        VirtualPath path = resolution.Value!.Path;
        FileSystemError? denial = await AuthorizeAsync(
            path,
            FileSystemOperation.Read,
            FileSystemAccess.Read,
            FileSystemHandleAccess.Read,
            isWrite: false,
            context,
            cancellationToken);
        if (denial is not null)
        {
            return FileSystemResult<FileSystemContentReadHandle>.Failure(denial);
        }

        FileSystemMountResolution route = _router.Resolve(path);
        return await route.Mount.Provider.ReadAsync(
            new FileSystemReadRequest(path, FileSystemLinkBehavior.NoFollow),
            context,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemResult<FileSystemDirectorySnapshot>> EnumerateAsync(
        FileSystemEnumerateRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        FileSystemResult<FileSystemPathResolution> resolution = await ResolveAsync(
            request.Path,
            request.LinkBehavior,
            FileSystemOperation.Enumerate,
            context,
            cancellationToken);
        if (!resolution.Succeeded)
        {
            return FileSystemResult<FileSystemDirectorySnapshot>.Failure(resolution.Error!);
        }

        VirtualPath path = resolution.Value!.Path;
        FileSystemError? denial = await AuthorizeAsync(
            path,
            FileSystemOperation.Enumerate,
            FileSystemAccess.Read | FileSystemAccess.Execute,
            FileSystemHandleAccess.Enumerate,
            isWrite: false,
            context,
            cancellationToken);
        if (denial is not null)
        {
            return FileSystemResult<FileSystemDirectorySnapshot>.Failure(denial);
        }

        FileSystemMountResolution route = _router.Resolve(path);
        return await route.Mount.Provider.EnumerateAsync(
            new FileSystemEnumerateRequest(path, FileSystemLinkBehavior.NoFollow),
            context,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> CreateAsync(
        FileSystemCreateRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        VirtualPath requestedParent = GetParent(request.Path);
        FileSystemResult<FileSystemPathResolution> parentResolution = await ResolveAsync(
            requestedParent,
            FileSystemLinkBehavior.Follow,
            FileSystemOperation.Create,
            context,
            cancellationToken);
        if (!parentResolution.Succeeded)
        {
            return Reject(parentResolution.Error!);
        }

        VirtualPath parent = parentResolution.Value!.Path;
        VirtualPath path = Combine(parent, GetName(request.Path));
        FileSystemError? denial = await AuthorizeAsync(
            parent,
            FileSystemOperation.Create,
            FileSystemAccess.Write | FileSystemAccess.Execute,
            FileSystemHandleAccess.Write,
            isWrite: true,
            context,
            cancellationToken,
            authorizationPath: path);
        if (denial is not null)
        {
            return Reject(denial);
        }

        FileSystemMountResolution route = _router.Resolve(path);
        FileSystemMutationResult result = await route.Mount.Provider.CreateAsync(
            new FileSystemCreateRequest(
                path,
                request.Kind,
                request.Permissions,
                request.ExpectedParentRevision,
                request.SymbolicLinkTarget),
            context,
            cancellationToken);
        if (result.Transaction.Status == FileSystemTransactionStatus.Committed && result.Entry is not null)
        {
            PublishChange(path, result.Entry.Metadata.Kind, result.Entry.Metadata.Revision, FileSystemChangeKind.Created);
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> WriteAsync(
        FileSystemWriteRequest request,
        IFileSystemContentSource content,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        FileSystemResult<FileSystemPathResolution> resolution = await ResolveAsync(
            request.Path,
            FileSystemLinkBehavior.Follow,
            FileSystemOperation.Write,
            context,
            cancellationToken);
        if (!resolution.Succeeded)
        {
            return Reject(resolution.Error!);
        }

        VirtualPath path = resolution.Value!.Path;
        FileSystemError? denial = await AuthorizeAsync(
            path,
            FileSystemOperation.Write,
            FileSystemAccess.Write,
            FileSystemHandleAccess.Write,
            isWrite: true,
            context,
            cancellationToken);
        if (denial is not null)
        {
            return Reject(denial);
        }

        FileSystemMutationResult result = await _router.Resolve(path).Mount.Provider.WriteAsync(
            new FileSystemWriteRequest(path, request.ExpectedRevision),
            content,
            context,
            cancellationToken);
        if (result.Transaction.Status == FileSystemTransactionStatus.Committed && result.Entry is not null)
        {
            PublishChange(path, result.Entry.Metadata.Kind, result.Entry.Metadata.Revision, FileSystemChangeKind.ContentModified);
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> MoveAsync(
        FileSystemMoveRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        FileSystemResult<FileSystemPathResolution> sourceResolution = await ResolveAsync(
            request.SourcePath,
            FileSystemLinkBehavior.NoFollow,
            FileSystemOperation.Move,
            context,
            cancellationToken);
        FileSystemResult<FileSystemPathResolution> destinationParentResolution = await ResolveAsync(
            GetParent(request.DestinationPath),
            FileSystemLinkBehavior.Follow,
            FileSystemOperation.Move,
            context,
            cancellationToken);
        if (!sourceResolution.Succeeded || !destinationParentResolution.Succeeded)
        {
            return Reject(sourceResolution.Error ?? destinationParentResolution.Error!);
        }

        VirtualPath source = sourceResolution.Value!.Path;
        VirtualPath sourceParent = GetParent(source);
        VirtualPath destinationParent = destinationParentResolution.Value!.Path;
        VirtualPath destination = Combine(destinationParent, GetName(request.DestinationPath));
        FileSystemMountResolution sourceRoute = _router.Resolve(source);
        FileSystemMountResolution destinationRoute = _router.Resolve(destination);
        if (!ReferenceEquals(sourceRoute.Mount.Provider, destinationRoute.Mount.Provider))
        {
            return Reject(new FileSystemError(
                FileSystemOperation.Move,
                FileSystemErrorCode.CrossMountOperation,
                source,
                destination));
        }

        FileSystemError? sourceDenial = await AuthorizeAsync(
            sourceParent,
            FileSystemOperation.Move,
            FileSystemAccess.Write | FileSystemAccess.Execute,
            FileSystemHandleAccess.Write,
            isWrite: true,
            context,
            cancellationToken,
            authorizationPath: source);
        FileSystemError? destinationDenial = await AuthorizeAsync(
            destinationParent,
            FileSystemOperation.Move,
            FileSystemAccess.Write | FileSystemAccess.Execute,
            FileSystemHandleAccess.Write,
            isWrite: true,
            context,
            cancellationToken,
            authorizationPath: destination);
        if (sourceDenial is not null || destinationDenial is not null)
        {
            return Reject(sourceDenial ?? destinationDenial!);
        }

        FileSystemMutationResult result = await sourceRoute.Mount.Provider.MoveAsync(
            new FileSystemMoveRequest(
                source,
                destination,
                request.ExpectedEntryRevision,
                request.ExpectedSourceParentRevision,
                request.ExpectedDestinationParentRevision),
            context,
            cancellationToken);
        if (result.Transaction.Status == FileSystemTransactionStatus.Committed && result.Entry is not null)
        {
            PublishChange(
                source, result.Entry.Metadata.Kind, result.Entry.Metadata.Revision, FileSystemChangeKind.MovedFrom, destination);
            if (destinationParent != sourceParent)
            {
                PublishChange(destination, result.Entry.Metadata.Kind, result.Entry.Metadata.Revision, FileSystemChangeKind.MovedTo);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> CopyAsync(
        FileSystemCopyRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        FileSystemResult<FileSystemPathResolution> sourceResolution = await ResolveAsync(
            request.SourcePath,
            FileSystemLinkBehavior.NoFollow,
            FileSystemOperation.Copy,
            context,
            cancellationToken);
        FileSystemResult<FileSystemPathResolution> destinationParentResolution = await ResolveAsync(
            GetParent(request.DestinationPath),
            FileSystemLinkBehavior.Follow,
            FileSystemOperation.Copy,
            context,
            cancellationToken);
        if (!sourceResolution.Succeeded || !destinationParentResolution.Succeeded)
        {
            return Reject(sourceResolution.Error ?? destinationParentResolution.Error!);
        }

        VirtualPath source = sourceResolution.Value!.Path;
        VirtualPath destinationParent = destinationParentResolution.Value!.Path;
        VirtualPath destination = Combine(destinationParent, GetName(request.DestinationPath));
        FileSystemMountResolution sourceRoute = _router.Resolve(source);
        FileSystemMountResolution destinationRoute = _router.Resolve(destination);
        if (!ReferenceEquals(sourceRoute.Mount.Provider, destinationRoute.Mount.Provider))
        {
            return Reject(new FileSystemError(
                FileSystemOperation.Copy,
                FileSystemErrorCode.CrossMountOperation,
                source,
                destination));
        }

        FileSystemError? sourceDenial = await AuthorizeAsync(
            source,
            FileSystemOperation.Copy,
            FileSystemAccess.Read,
            FileSystemHandleAccess.Read,
            isWrite: false,
            context,
            cancellationToken);
        FileSystemError? destinationDenial = await AuthorizeAsync(
            destinationParent,
            FileSystemOperation.Copy,
            FileSystemAccess.Write | FileSystemAccess.Execute,
            FileSystemHandleAccess.Write,
            isWrite: true,
            context,
            cancellationToken,
            authorizationPath: destination);
        if (sourceDenial is not null || destinationDenial is not null)
        {
            return Reject(sourceDenial ?? destinationDenial!);
        }

        FileSystemMutationResult result = await sourceRoute.Mount.Provider.CopyAsync(
            new FileSystemCopyRequest(
                source,
                destination,
                request.ExpectedEntryRevision,
                request.ExpectedDestinationParentRevision),
            context,
            cancellationToken);
        if (result.Transaction.Status == FileSystemTransactionStatus.Committed && result.Entry is not null)
        {
            PublishChange(destination, result.Entry.Metadata.Kind, result.Entry.Metadata.Revision, FileSystemChangeKind.Created);
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> DeleteAsync(
        FileSystemDeleteRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        FileSystemResult<FileSystemPathResolution> resolution = await ResolveAsync(
            request.Path,
            FileSystemLinkBehavior.NoFollow,
            FileSystemOperation.Delete,
            context,
            cancellationToken);
        if (!resolution.Succeeded)
        {
            return Reject(resolution.Error!);
        }

        VirtualPath path = resolution.Value!.Path;
        FileSystemMountResolution route = _router.Resolve(path);
        if (path == route.Mount.Path && route.Mount.IsProtectedRoot)
        {
            return Reject(new FileSystemError(
                FileSystemOperation.Delete,
                FileSystemErrorCode.ProtectedEntry,
                path));
        }

        FileSystemError? denial = await AuthorizeAsync(
            GetParent(path),
            FileSystemOperation.Delete,
            FileSystemAccess.Write | FileSystemAccess.Execute,
            FileSystemHandleAccess.Write,
            isWrite: true,
            context,
            cancellationToken,
            authorizationPath: path);
        if (denial is not null)
        {
            return Reject(denial);
        }

        FileSystemResult<FileSystemEntrySnapshot> preDeleteStat = await route.Mount.Provider.StatAsync(
            new FileSystemStatRequest(path, FileSystemLinkBehavior.NoFollow), context, cancellationToken);

        FileSystemMutationResult result = await route.Mount.Provider.DeleteAsync(
            new FileSystemDeleteRequest(
                path,
                request.ExpectedEntryRevision,
                request.ExpectedParentRevision,
                request.Recursive),
            context,
            cancellationToken);
        if (result.Transaction.Status == FileSystemTransactionStatus.Committed && preDeleteStat.Succeeded)
        {
            VirtualPath parent = GetParent(path);
            FileSystemResult<FileSystemEntrySnapshot> parentStat = await route.Mount.Provider.StatAsync(
                new FileSystemStatRequest(parent, FileSystemLinkBehavior.NoFollow), context, cancellationToken);
            long revision = parentStat.Succeeded ? parentStat.Value!.Metadata.Revision : preDeleteStat.Value!.Metadata.Revision;
            PublishChange(path, preDeleteStat.Value!.Metadata.Kind, revision, FileSystemChangeKind.Deleted);
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemResult<FileSystemEntrySnapshot>> StatAsync(
        FileSystemStatRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        FileSystemResult<FileSystemPathResolution> resolution = await ResolveAsync(
            request.Path,
            request.LinkBehavior,
            FileSystemOperation.Stat,
            context,
            cancellationToken);
        if (!resolution.Succeeded)
        {
            return FileSystemResult<FileSystemEntrySnapshot>.Failure(resolution.Error!);
        }

        VirtualPath path = resolution.Value!.Path;
        FileSystemError? denial = await AuthorizeAsync(
            path,
            FileSystemOperation.Stat,
            FileSystemAccess.Read,
            FileSystemHandleAccess.Metadata,
            isWrite: false,
            context,
            cancellationToken);
        if (denial is not null)
        {
            return FileSystemResult<FileSystemEntrySnapshot>.Failure(denial);
        }

        return await _router.Resolve(path).Mount.Provider.StatAsync(
            new FileSystemStatRequest(path, FileSystemLinkBehavior.NoFollow),
            context,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> SetPermissionsAsync(
        FileSystemSetPermissionsRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        FileSystemResult<FileSystemPathResolution> resolution = await ResolveAsync(
            request.Path,
            request.LinkBehavior,
            FileSystemOperation.SetPermissions,
            context,
            cancellationToken);
        if (!resolution.Succeeded)
        {
            return Reject(resolution.Error!);
        }

        VirtualPath path = resolution.Value!.Path;
        FileSystemError? denial = await AuthorizeAsync(
            path,
            FileSystemOperation.SetPermissions,
            FileSystemAccess.Write,
            FileSystemHandleAccess.Write,
            isWrite: true,
            context,
            cancellationToken);
        if (denial is not null)
        {
            return Reject(denial);
        }

        FileSystemMutationResult result = await _router.Resolve(path).Mount.Provider.SetPermissionsAsync(
            new FileSystemSetPermissionsRequest(
                path,
                request.Permissions,
                request.ExpectedRevision,
                FileSystemLinkBehavior.NoFollow),
            context,
            cancellationToken);
        if (result.Transaction.Status == FileSystemTransactionStatus.Committed && result.Entry is not null)
        {
            PublishChange(path, result.Entry.Metadata.Kind, result.Entry.Metadata.Revision, FileSystemChangeKind.MetadataModified);
        }

        return result;
    }

    private ValueTask<FileSystemResult<FileSystemPathResolution>> ResolveAsync(
        VirtualPath path,
        FileSystemLinkBehavior linkBehavior,
        FileSystemOperation operation,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken) =>
        _pathResolver.ResolveAsync(path, linkBehavior, operation, context, cancellationToken);

    private async ValueTask<FileSystemError?> AuthorizeAsync(
        VirtualPath metadataPath,
        FileSystemOperation operation,
        FileSystemAccess requiredAccess,
        FileSystemHandleAccess handleAccess,
        bool isWrite,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken,
        VirtualPath? authorizationPath = null)
    {
        FileSystemResult<FileSystemEntrySnapshot> stat = await _router.Resolve(metadataPath).Mount.Provider.StatAsync(
            new FileSystemStatRequest(metadataPath, FileSystemLinkBehavior.NoFollow),
            context,
            cancellationToken);
        if (!stat.Succeeded)
        {
            return new FileSystemError(
                operation,
                stat.Error?.Code ?? FileSystemErrorCode.ProviderFailure,
                authorizationPath ?? metadataPath);
        }

        VirtualPath policyPath = authorizationPath ?? metadataPath;
        FileSystemAuthorizationResult authorization = _authorizer.Authorize(
            new FileSystemAuthorizationRequest(
                operation,
                policyPath,
                stat.Value!.Metadata,
                requiredAccess,
                ResolveCapability(policyPath, context, isWrite),
                handleAccess,
                isWrite && IsSystemPath(policyPath)
                    ? AppAuthority.Administrator
                    : AppAuthority.User,
                context));
        return authorization.Error;
    }

    private static string ResolveCapability(
        VirtualPath path,
        FileSystemAuthorizationContext context,
        bool isWrite)
    {
        string userId = context.OperationContext.UserId;
        string? username = context.OperationContext.UserName;

        if (!string.IsNullOrWhiteSpace(username) && IsWithin(path, $"/home/{username}/.config/apps/{context.OperationContext.AppId}"))
        {
            return isWrite ? AppCapabilities.FileSystemPrivateWrite : AppCapabilities.FileSystemPrivateRead;
        }

        if (IsWithin(path, $"/home/{userId}/.config/apps/{context.OperationContext.AppId}"))
        {
            return isWrite ? AppCapabilities.FileSystemPrivateWrite : AppCapabilities.FileSystemPrivateRead;
        }

        if (!string.IsNullOrWhiteSpace(username) && IsWithin(path, $"/home/{username}"))
        {
            return isWrite ? AppCapabilities.FileSystemUserHomeWrite : AppCapabilities.FileSystemUserHomeRead;
        }

        if (IsWithin(path, $"/home/{userId}"))
        {
            return isWrite ? AppCapabilities.FileSystemUserHomeWrite : AppCapabilities.FileSystemUserHomeRead;
        }

        return isWrite ? AppCapabilities.FileSystemSystemWrite : AppCapabilities.FileSystemSystemRead;
    }

    private static bool IsSystemPath(VirtualPath path) =>
        IsWithin(path, "/etc") || IsWithin(path, "/bin") || IsWithin(path, "/var");

    private static bool IsWithin(VirtualPath path, string root) =>
        path.Value == root || path.Value.StartsWith($"{root}/", StringComparison.Ordinal);

    private FileSystemMutationResult Reject(FileSystemError error)
    {
        Guid transactionId = _transactionIdFactory();
        if (transactionId == Guid.Empty)
        {
            throw new InvalidOperationException("The transaction ID factory returned an empty ID.");
        }

        FileSystemTransactionResult transaction = error.Code == FileSystemErrorCode.Cancelled
            ? FileSystemTransactionResult.Cancelled(transactionId, error)
            : FileSystemTransactionResult.Rejected(transactionId, error);
        return new FileSystemMutationResult(transaction);
    }

    private static VirtualPath GetParent(VirtualPath path)
    {
        int separator = path.Value.LastIndexOf('/');
        return separator <= 0 ? VirtualPath.Parse("/") : VirtualPath.Parse(path.Value[..separator]);
    }

    private static FileSystemEntryName GetName(VirtualPath path) =>
        FileSystemEntryName.Parse(path.Value[(path.Value.LastIndexOf('/') + 1)..]);

    private static VirtualPath Combine(VirtualPath parent, FileSystemEntryName name) =>
        VirtualPath.Parse(parent.Value == "/" ? $"/{name.Value}" : $"{parent.Value}/{name.Value}");

    /// <summary>Publishes one change notification for <paramref name="affectedPath"/> to its parent directory's topic.</summary>
    private void PublishChange(
        VirtualPath affectedPath,
        FileSystemEntryKind entryKind,
        long revision,
        FileSystemChangeKind kind,
        VirtualPath? movedToPath = null)
    {
        TopicName topic = FileSystemTopics.ForDirectory(GetParent(affectedPath));
        _topicBus.Publish(
            topic,
            new FileSystemChangeEvent(affectedPath, kind, entryKind, revision, _clock(), movedToPath),
            KernelPublisherIdentity.Value);
    }
}