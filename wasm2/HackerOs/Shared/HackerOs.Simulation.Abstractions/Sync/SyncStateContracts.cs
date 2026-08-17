namespace HackerOs.Simulation.Abstractions.Sync;

/// <summary>
/// The last-synced sync-<c>Revision</c> and <c>ContentHash</c> tracked for one record (ADR 0029).
/// Independent of any document's own local optimistic-concurrency revision.
/// </summary>
public sealed record SyncRecordTrackingState(long Revision, string ContentHash);

/// <summary>
/// Persists one opaque pull cursor per sync domain (ADR 0029). Domain-agnostic — shared by every
/// sync-domain pass, not specific to any one domain.
/// </summary>
public interface ISyncCursorRepository
{
    /// <summary>Gets the stored cursor for <paramref name="domain"/>, or <see langword="null"/> if never pulled.</summary>
    ValueTask<string?> GetCursorAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>Persists the cursor for <paramref name="domain"/>, replacing any prior value.</summary>
    ValueTask SetCursorAsync(string domain, string? cursor, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists <see cref="SyncRecordTrackingState"/> per <c>(domain, recordId)</c> pair (ADR 0029).
/// Domain-agnostic like <see cref="ISyncCursorRepository"/>.
/// </summary>
public interface ISyncRecordStateRepository
{
    /// <summary>Gets the tracked state for one record, or <see langword="null"/> if never synced.</summary>
    ValueTask<SyncRecordTrackingState?> GetAsync(string domain, Guid recordId, CancellationToken cancellationToken = default);

    /// <summary>Persists the tracked state for one record, replacing any prior value.</summary>
    ValueTask SetAsync(string domain, Guid recordId, long revision, string contentHash, CancellationToken cancellationToken = default);
}
