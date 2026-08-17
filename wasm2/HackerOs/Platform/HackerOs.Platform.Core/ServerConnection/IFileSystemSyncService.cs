using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.Server.Contracts.Sync;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.ServerConnection;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Simulation.Abstractions.Sync;

namespace HackerOs.Platform.Core.ServerConnection;

/// <summary>
/// Pushes/pulls filesystem entries under the active user's <c>/home/{userId}</c> to/from the
/// optional server's FileSystem sync domain (ADR 0030). A no-op when this device isn't connected
/// or no session is active — unlike <see cref="ISettingsSyncService"/>, this domain has no
/// meaningful OS-level context to act under without a logged-in user's own home directory.
/// </summary>
public interface IFileSystemSyncService
{
    /// <summary>Pushes every changed filesystem entry under the home directory since its last successful push.</summary>
    Task PushAsync(CancellationToken cancellationToken = default);

    /// <summary>Pulls and applies every remote FileSystem change since the last pull.</summary>
    Task PullAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Relative paths (under the home directory) left unresolved by the most recent
    /// <see cref="PushAsync"/> because the server reported a concurrent edit (ADR 0030 Decision 5:
    /// neither copy is auto-applied). Cleared and repopulated by each push; not persisted.
    /// </summary>
    IReadOnlyList<string> UnresolvedConflicts { get; }
}

/// <summary>Default <see cref="IFileSystemSyncService"/> implementation.</summary>
public sealed class FileSystemSyncService(
    IFileSystemService fileSystem,
    ISessionService session,
    IServerConnectionService connection,
    ISyncClient syncClient,
    IContentTransferClient contentClient,
    ISyncCursorRepository cursors,
    ISyncRecordStateRepository recordState) : IFileSystemSyncService
{
    private List<string> _unresolvedConflicts = [];
    private HashSet<Guid> _conflictedRecordIds = [];

    public IReadOnlyList<string> UnresolvedConflicts => _unresolvedConflicts;

    public async Task PushAsync(CancellationToken cancellationToken = default)
    {
        AuthenticatedPrincipal? principal = session.CurrentPrincipal;
        ServerConnectionState? state = await connection.GetStateAsync(cancellationToken).ConfigureAwait(false);
        string? accessToken = state is null ? null : await connection.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (principal is null || state is null || accessToken is null)
        {
            return;
        }

        Uri serverBaseUrl = new(state.ServerBaseUrl);
        VirtualPath homePath = VirtualPath.Parse(principal.HomePath);
        FileSystemAuthorizationContext authContext = CreateAuthorizationContext(principal);

        List<(VirtualPath Path, FileSystemEntryMetadata Metadata)> walked =
            await WalkAsync(homePath, authContext, cancellationToken).ConfigureAwait(false);

        List<SyncRecordEnvelope> envelopes = [];
        Dictionary<Guid, (VirtualPath Path, string ContentHash)> pendingContentUploads = [];

        foreach ((VirtualPath path, FileSystemEntryMetadata metadata) in walked)
        {
            string relativePath = GetRelativePath(homePath, path);
            Guid recordId = metadata.Id.Value;

            string? contentHash = null;
            long? length = null;
            string? symlinkTarget = null;

            if (metadata is FileMetadata fileMetadata)
            {
                byte[] content = await ReadAllBytesAsync(path, authContext, cancellationToken).ConfigureAwait(false);
                contentHash = ComputeHash(content);
                length = fileMetadata.Length;
            }
            else if (metadata is SymbolicLinkMetadata linkMetadata)
            {
                symlinkTarget = linkMetadata.Target;
            }

            FileSystemSyncPayload payload = new(
                relativePath,
                metadata.Kind.ToString(),
                metadata.OwnerId,
                metadata.GroupId,
                metadata.Permissions.Mode,
                metadata.Timestamps.CreatedAtUtc,
                metadata.Timestamps.ContentModifiedAtUtc,
                metadata.Timestamps.MetadataChangedAtUtc,
                contentHash,
                length,
                symlinkTarget);

            string payloadJson = JsonSerializer.Serialize(payload, FileSystemSyncContractsJsonContext.Default.FileSystemSyncPayload);
            string metadataHash = ComputeHash(Encoding.UTF8.GetBytes(payloadJson));

            SyncRecordTrackingState? tracked = await recordState.GetAsync(
                SyncDomain.FileSystem, recordId, cancellationToken).ConfigureAwait(false);
            if (tracked is not null && string.Equals(tracked.ContentHash, metadataHash, StringComparison.Ordinal))
            {
                continue; // Unchanged since the last successful push/pull.
            }

            long nextRevision = (tracked?.Revision ?? 0) + 1;
            envelopes.Add(new SyncRecordEnvelope(
                recordId, SyncDomain.FileSystem, SchemaVersion: 1, state.AccountId, state.DeviceId,
                nextRevision, DateTimeOffset.UtcNow, ServerReceivedUtc: null, metadataHash, IsTombstone: false, payloadJson));

            if (contentHash is not null)
            {
                pendingContentUploads[recordId] = (path, contentHash);
            }
        }

        if (envelopes.Count == 0)
        {
            return;
        }

        // Upload changed file content before pushing metadata, so a pushed record never points at
        // content the server doesn't have yet.
        foreach ((VirtualPath path, string contentHash) in pendingContentUploads.Values)
        {
            byte[] content = await ReadAllBytesAsync(path, authContext, cancellationToken).ConfigureAwait(false);
            await UploadContentAsync(serverBaseUrl, accessToken, contentHash, content, state.AccountId, cancellationToken)
                .ConfigureAwait(false);
        }

        PushResponse response = await syncClient.PushAsync(
            serverBaseUrl, accessToken, new PushRequest(SyncDomain.FileSystem, envelopes, Guid.NewGuid()), cancellationToken)
            .ConfigureAwait(false);

        HashSet<Guid> conflicted = [.. response.Conflicts.Select(conflict => conflict.RecordId)];
        List<string> conflicts = [];
        foreach (SyncRecordEnvelope envelope in envelopes)
        {
            if (conflicted.Contains(envelope.RecordId))
            {
                // ADR 0030 Decision 5: never auto-apply either copy on a push conflict for this
                // domain — leave both as-is and surface it, rather than reusing ADR 0029's
                // server-wins default (acceptable for a preference, not for file content).
                FileSystemSyncPayload payload = JsonSerializer.Deserialize(
                    envelope.PayloadJson!, FileSystemSyncContractsJsonContext.Default.FileSystemSyncPayload)!;
                conflicts.Add(payload.RelativePath);
                continue;
            }

            await recordState.SetAsync(
                SyncDomain.FileSystem, envelope.RecordId, envelope.Revision, envelope.ContentHash, cancellationToken)
                .ConfigureAwait(false);
        }

        _unresolvedConflicts = conflicts;
        _conflictedRecordIds = conflicted;
    }

    public async Task PullAsync(CancellationToken cancellationToken = default)
    {
        AuthenticatedPrincipal? principal = session.CurrentPrincipal;
        ServerConnectionState? state = await connection.GetStateAsync(cancellationToken).ConfigureAwait(false);
        string? accessToken = state is null ? null : await connection.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (principal is null || state is null || accessToken is null)
        {
            return;
        }

        Uri serverBaseUrl = new(state.ServerBaseUrl);
        VirtualPath homePath = VirtualPath.Parse(principal.HomePath);
        FileSystemAuthorizationContext authContext = CreateAuthorizationContext(principal);

        string? cursor = await cursors.GetCursorAsync(SyncDomain.FileSystem, cancellationToken).ConfigureAwait(false);

        while (true)
        {
            PullResponse response = await syncClient.PullAsync(
                serverBaseUrl, accessToken, new PullRequest(SyncDomain.FileSystem, cursor, MaxRecords: 100), cancellationToken)
                .ConfigureAwait(false);

            // Applied shallowest-path-first within the page so a directory record is always created
            // before any file/symlink record nested under it (ADR 0030's ordering simplification —
            // does not guarantee correctness across page boundaries for very deep trees, but is
            // sufficient for the bounded home-directory scope this pass covers).
            var applicable = response.Records
                .Where(envelope => envelope.PayloadJson is not null)
                // A record this device just failed to push due to a concurrent edit (ADR 0030
                // Decision 5) must not be silently overwritten by whatever the server hands back for
                // it here — that would apply the server's copy through the back door. Left unresolved
                // until a future pass adds manual resolution.
                .Where(envelope => !_conflictedRecordIds.Contains(envelope.RecordId))
                .Select(envelope => (
                    Envelope: envelope,
                    Payload: JsonSerializer.Deserialize(
                        envelope.PayloadJson!, FileSystemSyncContractsJsonContext.Default.FileSystemSyncPayload)!))
                .OrderBy(pair => pair.Payload.RelativePath.Count(c => c == '/'));

            foreach ((SyncRecordEnvelope envelope, FileSystemSyncPayload payload) in applicable)
            {
                await ApplyPayloadAsync(homePath, authContext, serverBaseUrl, accessToken, payload, cancellationToken)
                    .ConfigureAwait(false);
                await recordState.SetAsync(
                    SyncDomain.FileSystem, envelope.RecordId, envelope.Revision, envelope.ContentHash, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (response.NextCursor is not null)
            {
                cursor = response.NextCursor;
                await cursors.SetCursorAsync(SyncDomain.FileSystem, cursor, cancellationToken).ConfigureAwait(false);
            }

            if (!response.HasMore)
            {
                break;
            }
        }
    }

    private async Task ApplyPayloadAsync(
        VirtualPath homePath,
        FileSystemAuthorizationContext authContext,
        Uri serverBaseUrl,
        string accessToken,
        FileSystemSyncPayload payload,
        CancellationToken cancellationToken)
    {
        VirtualPath path = VirtualPath.Parse($"{homePath.Value}/{payload.RelativePath}");
        FileSystemPermissions permissions = FileSystemPermissions.FromMode(payload.PermissionMode);

        if (string.Equals(payload.Kind, nameof(FileSystemEntryKind.Directory), StringComparison.Ordinal))
        {
            // AlreadyExists is expected and fine — the directory may already be present locally.
            await fileSystem.CreateAsync(
                new FileSystemCreateRequest(path, FileSystemEntryKind.Directory, permissions), authContext, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (string.Equals(payload.Kind, nameof(FileSystemEntryKind.SymbolicLink), StringComparison.Ordinal)
            && payload.SymlinkTarget is not null)
        {
            await fileSystem.CreateAsync(
                new FileSystemCreateRequest(
                    path, FileSystemEntryKind.SymbolicLink, permissions, symbolicLinkTarget: payload.SymlinkTarget),
                authContext, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (string.Equals(payload.Kind, nameof(FileSystemEntryKind.File), StringComparison.Ordinal)
            && payload.ContentHash is not null)
        {
            await fileSystem.CreateAsync(
                new FileSystemCreateRequest(path, FileSystemEntryKind.File, permissions), authContext, cancellationToken)
                .ConfigureAwait(false);

            byte[] content = await DownloadContentAsync(serverBaseUrl, accessToken, payload.ContentHash, cancellationToken)
                .ConfigureAwait(false);
            await fileSystem.WriteAsync(
                new FileSystemWriteRequest(path), new ByteContentSource(content), authContext, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<List<(VirtualPath Path, FileSystemEntryMetadata Metadata)>> WalkAsync(
        VirtualPath root, FileSystemAuthorizationContext authContext, CancellationToken cancellationToken)
    {
        List<(VirtualPath, FileSystemEntryMetadata)> results = [];
        Queue<VirtualPath> directories = new();
        directories.Enqueue(root);

        while (directories.Count > 0)
        {
            VirtualPath directory = directories.Dequeue();
            FileSystemResult<FileSystemDirectorySnapshot> enumerated = await fileSystem.EnumerateAsync(
                new FileSystemEnumerateRequest(directory), authContext, cancellationToken).ConfigureAwait(false);
            if (!enumerated.Succeeded)
            {
                continue; // e.g. the home directory hasn't been seeded on this device yet.
            }

            foreach (FileSystemDirectoryItem item in enumerated.Value!.Entries)
            {
                VirtualPath childPath = VirtualPath.Parse($"{directory.Value}/{item.Name.Value}");
                results.Add((childPath, item.Metadata));
                if (item.Metadata.Kind == FileSystemEntryKind.Directory)
                {
                    directories.Enqueue(childPath);
                }
            }
        }

        return results;
    }

    private async Task<byte[]> ReadAllBytesAsync(
        VirtualPath path, FileSystemAuthorizationContext authContext, CancellationToken cancellationToken)
    {
        FileSystemResult<FileSystemContentReadHandle> result = await fileSystem.ReadAsync(
            new FileSystemReadRequest(path), authContext, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return [];
        }

        await using FileSystemContentReadHandle handle = result.Value!;
        using MemoryStream buffer = new();
        await handle.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private async Task UploadContentAsync(
        Uri serverBaseUrl, string accessToken, string contentHash, byte[] content, Guid accountId, CancellationToken cancellationToken)
    {
        InitiateContentUploadResponse initiate = await contentClient.InitiateUploadAsync(
            serverBaseUrl, accessToken, new InitiateContentUploadRequest(contentHash, content.Length, accountId), cancellationToken)
            .ConfigureAwait(false);
        if (initiate.AlreadyExists)
        {
            return; // Dedup short-circuit — the server already has this content.
        }

        for (int index = 0; index < initiate.TotalChunks; index++)
        {
            int start = index * initiate.ChunkSizeBytes;
            int length = Math.Min(initiate.ChunkSizeBytes, content.Length - start);
            byte[] chunk = content[start..(start + length)];
            await contentClient.UploadChunkAsync(serverBaseUrl, accessToken, initiate.UploadSessionId, index, chunk, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<byte[]> DownloadContentAsync(
        Uri serverBaseUrl, string accessToken, string contentHash, CancellationToken cancellationToken)
    {
        InitiateContentDownloadResponse initiate = await contentClient.InitiateDownloadAsync(
            serverBaseUrl, accessToken, new InitiateContentDownloadRequest(contentHash, StartByteOffset: 0), cancellationToken)
            .ConfigureAwait(false);

        using MemoryStream buffer = new();
        int totalChunks = initiate.TotalBytes == 0
            ? 0
            : (int)Math.Ceiling((double)initiate.TotalBytes / initiate.ChunkSizeBytes);
        for (int index = 0; index < totalChunks; index++)
        {
            byte[] chunk = await contentClient.DownloadChunkAsync(serverBaseUrl, accessToken, contentHash, index, cancellationToken)
                .ConfigureAwait(false);
            buffer.Write(chunk);
        }

        return buffer.ToArray();
    }

    private static FileSystemAuthorizationContext CreateAuthorizationContext(AuthenticatedPrincipal principal)
    {
        // Acts as the real logged-in user, not a system-elevated context: FileSystem sync only ever
        // touches that user's own home directory, which they already own outright (per
        // FileSystemAuthorizer's owner-match rule), so no elevated authority is needed or wanted.
        AppOperationContext operation = new()
        {
            AppId = "org.hackeros.sync",
            UserId = principal.LoginName.Value,
            UserAuthority = principal.Authority,
            GrantedCapabilities = new HashSet<string>(AppCapabilities.All, StringComparer.Ordinal),
            IsSystemOperation = false
        };
        return new FileSystemAuthorizationContext(
            operation,
            principal.GroupIds.Select(group => group.ToString()),
            DateTimeOffset.UtcNow);
    }

    private static string GetRelativePath(VirtualPath homePath, VirtualPath path) =>
        path.Value[(homePath.Value.Length + 1)..];

    private static string ComputeHash(byte[] content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));

    private sealed class ByteContentSource(byte[] bytes) : IFileSystemContentSource
    {
        public FileSystemContentDescriptor Descriptor { get; } = FileSystemContentDescriptor.Binary();

        public long? Length => bytes.Length;

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }
}
