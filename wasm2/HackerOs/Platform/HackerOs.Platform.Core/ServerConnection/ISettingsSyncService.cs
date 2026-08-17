using System.Security.Cryptography;
using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.Server.Contracts.Sync;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.ServerConnection;
using HackerOs.Simulation.Abstractions.Settings;
using HackerOs.Simulation.Abstractions.Sync;

namespace HackerOs.Platform.Core.ServerConnection;

/// <summary>
/// Pushes/pulls every <see cref="SettingsDocumentDefinition"/> with <c>SyncEligible: true</c>
/// to/from the optional server's Settings sync domain (ADR 0029). A no-op when this device isn't
/// connected (<see cref="IServerConnectionService.GetStateAsync"/> returns <see langword="null"/>).
/// </summary>
public interface ISettingsSyncService
{
    /// <summary>Pushes every changed sync-eligible document since its last successful push.</summary>
    Task PushAsync(CancellationToken cancellationToken = default);

    /// <summary>Pulls and applies every remote change for the Settings domain since the last pull.</summary>
    Task PullAsync(CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="ISettingsSyncService"/> implementation.</summary>
public sealed class SettingsSyncService(
    IEnumerable<SettingsDocumentDefinition> definitions,
    ISettingsDocumentService settings,
    IServerConnectionService connection,
    ISyncClient syncClient,
    ISyncCursorRepository cursors,
    ISyncRecordStateRepository recordState) : ISettingsSyncService
{
    // Per ADR 0029: a fixed system operation context, not any one user's — settings sync is an OS-level
    // background concern, matching the same IsSystemOperation pattern FileSystemSeeder already uses.
    private static readonly AppOperationContext SystemContext = new()
    {
        AppId = "org.hackeros.sync",
        UserId = "system",
        UserAuthority = AppAuthority.System,
        GrantedCapabilities = new HashSet<string>(AppCapabilities.All, StringComparer.Ordinal),
        IsSystemOperation = true
    };

    public async Task PushAsync(CancellationToken cancellationToken = default)
    {
        ServerConnectionState? state = await connection.GetStateAsync(cancellationToken).ConfigureAwait(false);
        string? accessToken = state is null ? null : await connection.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (state is null || accessToken is null)
        {
            return;
        }

        List<SyncRecordEnvelope> envelopes = [];
        foreach (SettingsDocumentDefinition definition in definitions.Where(definition => definition.SyncEligible))
        {
            SettingsReadResult read = await settings.ReadAsync(definition.Path, SystemContext, cancellationToken).ConfigureAwait(false);
            if (read.Status != SettingsReadStatus.Success || read.Document is null)
            {
                continue;
            }

            Guid recordId = ComputeRecordId(definition.Key);
            string contentHash = ComputeHash(read.Document.Content);
            SyncRecordTrackingState? tracked = await recordState.GetAsync(
                SyncDomain.Settings, recordId, cancellationToken).ConfigureAwait(false);
            if (tracked is not null && string.Equals(tracked.ContentHash, contentHash, StringComparison.Ordinal))
            {
                continue; // Unchanged since the last successful push/pull — nothing to send.
            }

            long nextRevision = (tracked?.Revision ?? 0) + 1;
            envelopes.Add(new SyncRecordEnvelope(
                recordId,
                SyncDomain.Settings,
                SchemaVersion: 1,
                state.AccountId,
                state.DeviceId,
                nextRevision,
                DateTimeOffset.UtcNow,
                ServerReceivedUtc: null,
                contentHash,
                IsTombstone: false,
                read.Document.Content));
        }

        if (envelopes.Count == 0)
        {
            return;
        }

        PushResponse response = await syncClient.PushAsync(
            new Uri(state.ServerBaseUrl),
            accessToken,
            new PushRequest(SyncDomain.Settings, envelopes, Guid.NewGuid()),
            cancellationToken).ConfigureAwait(false);

        HashSet<Guid> conflicted = [.. response.Conflicts.Select(conflict => conflict.RecordId)];
        foreach (SyncRecordEnvelope envelope in envelopes)
        {
            if (conflicted.Contains(envelope.RecordId))
            {
                // Per ADR 0029 Decision 6: a conflicted push is resolved by pulling and applying the
                // server's current copy instead — handled by PullAsync, not retried here.
                continue;
            }

            await recordState.SetAsync(
                SyncDomain.Settings, envelope.RecordId, envelope.Revision, envelope.ContentHash, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task PullAsync(CancellationToken cancellationToken = default)
    {
        ServerConnectionState? state = await connection.GetStateAsync(cancellationToken).ConfigureAwait(false);
        string? accessToken = state is null ? null : await connection.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (state is null || accessToken is null)
        {
            return;
        }

        Dictionary<Guid, SettingsDocumentDefinition> byRecordId = definitions
            .Where(definition => definition.SyncEligible)
            .ToDictionary(ComputeRecordId, definition => definition);
        if (byRecordId.Count == 0)
        {
            return;
        }

        Uri serverBaseUrl = new(state.ServerBaseUrl);
        string? cursor = await cursors.GetCursorAsync(SyncDomain.Settings, cancellationToken).ConfigureAwait(false);

        while (true)
        {
            PullResponse response = await syncClient.PullAsync(
                serverBaseUrl, accessToken, new PullRequest(SyncDomain.Settings, cursor, MaxRecords: 100), cancellationToken)
                .ConfigureAwait(false);

            foreach (SyncRecordEnvelope envelope in response.Records)
            {
                if (envelope.PayloadJson is null || !byRecordId.TryGetValue(envelope.RecordId, out SettingsDocumentDefinition? definition))
                {
                    continue; // Tombstoned or not one of our sync-eligible documents.
                }

                SettingsReadResult current = await settings.ReadAsync(definition.Path, SystemContext, cancellationToken).ConfigureAwait(false);
                long expectedRevision = current.Document?.Revision ?? 0;
                await settings.WriteAsync(
                    new SettingsWriteRequest(definition.Path, envelope.PayloadJson, expectedRevision), SystemContext, cancellationToken)
                    .ConfigureAwait(false);
                // A Conflict here (a concurrent local write mid-pull) is not retried in this pass — the
                // next pull cycle re-applies the same envelope, since the cursor only advances past it
                // once processed, per the loop below.

                await recordState.SetAsync(
                    SyncDomain.Settings, envelope.RecordId, envelope.Revision, envelope.ContentHash, cancellationToken)
                    .ConfigureAwait(false);
            }

            // The server only returns NextCursor when HasMore is true (an intentional at-least-once
            // design: the final page's records are re-delivered on the next pull rather than the cursor
            // silently skipping past them) — only persist a cursor advance when one is actually given.
            if (response.NextCursor is not null)
            {
                cursor = response.NextCursor;
                await cursors.SetCursorAsync(SyncDomain.Settings, cursor, cancellationToken).ConfigureAwait(false);
            }

            if (!response.HasMore)
            {
                break;
            }
        }
    }

    /// <summary>Deterministically derives a sync RecordId from a document's composite key (ADR 0029).</summary>
    private static Guid ComputeRecordId(SettingsDocumentDefinition definition) => ComputeRecordId(definition.Key);

    private static Guid ComputeRecordId(SettingsDocumentKey key)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToString()));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static string ComputeHash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
