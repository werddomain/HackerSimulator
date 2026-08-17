using System.Security.Cryptography;
using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Intents;
using HackerOs.Server.Contracts.Sync;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.ServerConnection;
using HackerOs.Simulation.Abstractions.Settings;
using HackerOs.Simulation.Abstractions.Sync;

namespace HackerOs.Platform.Core.ServerConnection;

/// <summary>
/// Pushes/pulls the single <see cref="FileAssociationSettingsDocuments"/> document to/from the
/// optional server's FileAssociations sync domain (ADR 0033). A narrow sibling of
/// <see cref="ISettingsSyncService"/> rather than a generalization of it: <c>SyncDomain.FileAssociations</c>
/// is a distinct, separately-partitioned domain server-side from <c>SyncDomain.Settings</c>, so routing
/// this one document through <see cref="SettingsSyncService"/> (which hard-codes <c>SyncDomain.Settings</c>)
/// would silently sync it into the wrong domain. A no-op when this device isn't connected.
/// </summary>
public interface IFileAssociationsSyncService
{
    /// <summary>Pushes the file-association document if it changed since the last successful push.</summary>
    Task PushAsync(CancellationToken cancellationToken = default);

    /// <summary>Pulls and applies the remote file-association document if it changed since the last pull.</summary>
    Task PullAsync(CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IFileAssociationsSyncService"/> implementation.</summary>
public sealed class FileAssociationsSyncService(
    ISettingsDocumentService settings,
    IServerConnectionService connection,
    ISyncClient syncClient,
    ISyncCursorRepository cursors,
    ISyncRecordStateRepository recordState) : IFileAssociationsSyncService
{
    // Same fixed system operation context ADR 0029's SettingsSyncService uses — an OS-level
    // background concern, not any one user's.
    private static readonly AppOperationContext SystemContext = new()
    {
        AppId = "org.hackeros.sync",
        UserId = "system",
        UserAuthority = AppAuthority.System,
        GrantedCapabilities = new HashSet<string>(AppCapabilities.All, StringComparer.Ordinal),
        IsSystemOperation = true
    };

    private static readonly Guid RecordId = ComputeRecordId(FileAssociationSettingsDocuments.Key);

    public async Task PushAsync(CancellationToken cancellationToken = default)
    {
        ServerConnectionState? state = await connection.GetStateAsync(cancellationToken).ConfigureAwait(false);
        string? accessToken = state is null ? null : await connection.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (state is null || accessToken is null)
        {
            return;
        }

        SettingsReadResult read = await settings.ReadAsync(
            FileAssociationSettingsDocuments.Path, SystemContext, cancellationToken).ConfigureAwait(false);
        if (read.Status != SettingsReadStatus.Success || read.Document is null)
        {
            return;
        }

        string contentHash = ComputeHash(read.Document.Content);
        SyncRecordTrackingState? tracked = await recordState.GetAsync(
            SyncDomain.FileAssociations, RecordId, cancellationToken).ConfigureAwait(false);
        if (tracked is not null && string.Equals(tracked.ContentHash, contentHash, StringComparison.Ordinal))
        {
            return; // Unchanged since the last successful push/pull.
        }

        long nextRevision = (tracked?.Revision ?? 0) + 1;
        SyncRecordEnvelope envelope = new(
            RecordId,
            SyncDomain.FileAssociations,
            SchemaVersion: 1,
            state.AccountId,
            state.DeviceId,
            nextRevision,
            DateTimeOffset.UtcNow,
            ServerReceivedUtc: null,
            contentHash,
            IsTombstone: false,
            read.Document.Content);

        PushResponse response = await syncClient.PushAsync(
            new Uri(state.ServerBaseUrl),
            accessToken,
            new PushRequest(SyncDomain.FileAssociations, [envelope], Guid.NewGuid()),
            cancellationToken).ConfigureAwait(false);

        if (response.Conflicts.Count > 0)
        {
            // Same as Settings (ADR 0029 Decision 6): a conflicted push is resolved by pulling and
            // applying the server's current copy instead — handled by PullAsync, not retried here.
            return;
        }

        await recordState.SetAsync(
            SyncDomain.FileAssociations, RecordId, envelope.Revision, envelope.ContentHash, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task PullAsync(CancellationToken cancellationToken = default)
    {
        ServerConnectionState? state = await connection.GetStateAsync(cancellationToken).ConfigureAwait(false);
        string? accessToken = state is null ? null : await connection.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (state is null || accessToken is null)
        {
            return;
        }

        Uri serverBaseUrl = new(state.ServerBaseUrl);
        string? cursor = await cursors.GetCursorAsync(SyncDomain.FileAssociations, cancellationToken).ConfigureAwait(false);

        while (true)
        {
            PullResponse response = await syncClient.PullAsync(
                serverBaseUrl, accessToken, new PullRequest(SyncDomain.FileAssociations, cursor, MaxRecords: 100), cancellationToken)
                .ConfigureAwait(false);

            foreach (SyncRecordEnvelope envelope in response.Records)
            {
                if (envelope.PayloadJson is null || envelope.RecordId != RecordId)
                {
                    continue; // Tombstoned, or not our one document (shouldn't happen for this domain).
                }

                SettingsReadResult current = await settings.ReadAsync(
                    FileAssociationSettingsDocuments.Path, SystemContext, cancellationToken).ConfigureAwait(false);
                long expectedRevision = current.Document?.Revision ?? 0;
                await settings.WriteAsync(
                    new SettingsWriteRequest(FileAssociationSettingsDocuments.Path, envelope.PayloadJson, expectedRevision),
                    SystemContext, cancellationToken).ConfigureAwait(false);
                // A Conflict here (a concurrent local write mid-pull) is not retried — the next pull
                // cycle re-applies the same envelope, matching ADR 0029's Settings adapter.

                await recordState.SetAsync(
                    SyncDomain.FileAssociations, envelope.RecordId, envelope.Revision, envelope.ContentHash, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (response.NextCursor is not null)
            {
                cursor = response.NextCursor;
                await cursors.SetCursorAsync(SyncDomain.FileAssociations, cursor, cancellationToken).ConfigureAwait(false);
            }

            if (!response.HasMore)
            {
                break;
            }
        }
    }

    private static Guid ComputeRecordId(SettingsDocumentKey key)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToString()));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static string ComputeHash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
