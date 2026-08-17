using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Server.Contracts.Sync;
using HackerOs.Simulation.Abstractions.ServerConnection;
using HackerOs.Simulation.Abstractions.Sync;

namespace HackerOs.Platform.Core.ServerConnection;

/// <summary>
/// Pushes/pulls per-app enablement flags to/from the optional server's AppCatalog sync domain
/// (ADR 0033). Unlike Grants (ADR 0031), this domain has a real local write path
/// (<see cref="IPersistentAppCatalogRepository.SetEnabledAsync"/>, driven by ADR 0032's Installed
/// Apps UI), so it syncs both directions. Only the enablement flag syncs, never the manifest — that's
/// a build artifact, not user data. A no-op when this device isn't connected.
/// </summary>
public interface IAppCatalogSyncService
{
    /// <summary>Pushes every app whose enablement flag changed since its last successful push.</summary>
    Task PushAsync(CancellationToken cancellationToken = default);

    /// <summary>Pulls and applies every remote enablement change since the last pull.</summary>
    Task PullAsync(CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IAppCatalogSyncService"/> implementation.</summary>
public sealed class AppCatalogSyncService(
    IPersistentAppCatalogRepository catalog,
    AppEnablementRegistry enablement,
    IServerConnectionService connection,
    ISyncClient syncClient,
    ISyncCursorRepository cursors,
    ISyncRecordStateRepository recordState) : IAppCatalogSyncService
{
    public async Task PushAsync(CancellationToken cancellationToken = default)
    {
        ServerConnectionState? state = await connection.GetStateAsync(cancellationToken).ConfigureAwait(false);
        string? accessToken = state is null ? null : await connection.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (state is null || accessToken is null)
        {
            return;
        }

        List<SyncRecordEnvelope> envelopes = [];
        foreach (PersistedAppCatalogEntry entry in await catalog.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            Guid recordId = ComputeRecordId(entry.Manifest.Id);
            AppCatalogSyncPayload payload = new(entry.Manifest.Id, entry.IsEnabled);
            string payloadJson = JsonSerializer.Serialize(payload, AppCatalogSyncContractsJsonContext.Default.AppCatalogSyncPayload);
            string contentHash = ComputeHash(payloadJson);

            SyncRecordTrackingState? tracked = await recordState.GetAsync(
                SyncDomain.AppCatalog, recordId, cancellationToken).ConfigureAwait(false);
            if (tracked is not null && string.Equals(tracked.ContentHash, contentHash, StringComparison.Ordinal))
            {
                continue; // Unchanged since the last successful push/pull.
            }

            long nextRevision = (tracked?.Revision ?? 0) + 1;
            envelopes.Add(new SyncRecordEnvelope(
                recordId, SyncDomain.AppCatalog, SchemaVersion: 1, state.AccountId, state.DeviceId,
                nextRevision, DateTimeOffset.UtcNow, ServerReceivedUtc: null, contentHash, IsTombstone: false, payloadJson));
        }

        if (envelopes.Count == 0)
        {
            return;
        }

        PushResponse response = await syncClient.PushAsync(
            new Uri(state.ServerBaseUrl), accessToken,
            new PushRequest(SyncDomain.AppCatalog, envelopes, Guid.NewGuid()), cancellationToken)
            .ConfigureAwait(false);

        HashSet<Guid> conflicted = [.. response.Conflicts.Select(conflict => conflict.RecordId)];
        foreach (SyncRecordEnvelope envelope in envelopes)
        {
            if (conflicted.Contains(envelope.RecordId))
            {
                // Same as Settings (ADR 0029 Decision 6): resolved by pulling and applying the
                // server's current copy instead, not retried here. ADR 0025 named ClientWins as the
                // preferred policy for this domain, but nothing could produce a conflict before this
                // pass existed — see ADR 0033 for why the untested proven pattern was reused instead.
                continue;
            }

            await recordState.SetAsync(
                SyncDomain.AppCatalog, envelope.RecordId, envelope.Revision, envelope.ContentHash, cancellationToken)
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

        Uri serverBaseUrl = new(state.ServerBaseUrl);
        string? cursor = await cursors.GetCursorAsync(SyncDomain.AppCatalog, cancellationToken).ConfigureAwait(false);

        while (true)
        {
            PullResponse response = await syncClient.PullAsync(
                serverBaseUrl, accessToken, new PullRequest(SyncDomain.AppCatalog, cursor, MaxRecords: 100), cancellationToken)
                .ConfigureAwait(false);

            foreach (SyncRecordEnvelope envelope in response.Records)
            {
                if (envelope.PayloadJson is null)
                {
                    continue; // Tombstone; not expected for this domain, skip defensively.
                }

                AppCatalogSyncPayload payload = JsonSerializer.Deserialize(
                    envelope.PayloadJson, AppCatalogSyncContractsJsonContext.Default.AppCatalogSyncPayload)!;

                // False means this device's own build doesn't have this app at all — expected across
                // devices with different app selections, not an error.
                bool applied = await catalog.SetEnabledAsync(payload.AppId, payload.IsEnabled, cancellationToken)
                    .ConfigureAwait(false);
                if (applied)
                {
                    // Take effect immediately rather than waiting for the next boot's hydration (ADR
                    // 0032 Decision 2) — a raw registry update, same as boot hydration, not a full
                    // AppLifecycleOrchestrator.DisableAsync (so a currently-running instance of a
                    // newly-disabled app is not stopped mid-session by a pull; it will simply fail to
                    // relaunch, consistent with boot-time hydration's own scope).
                    if (payload.IsEnabled)
                    {
                        enablement.MarkEnabled(payload.AppId);
                    }
                    else
                    {
                        enablement.MarkDisabled([payload.AppId]);
                    }
                }

                await recordState.SetAsync(
                    SyncDomain.AppCatalog, envelope.RecordId, envelope.Revision, envelope.ContentHash, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (response.NextCursor is not null)
            {
                cursor = response.NextCursor;
                await cursors.SetCursorAsync(SyncDomain.AppCatalog, cursor, cancellationToken).ConfigureAwait(false);
            }

            if (!response.HasMore)
            {
                break;
            }
        }
    }

    private static Guid ComputeRecordId(string appId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(appId));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static string ComputeHash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
