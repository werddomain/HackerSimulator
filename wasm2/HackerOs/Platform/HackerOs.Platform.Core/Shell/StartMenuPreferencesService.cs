using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Shell;

/// <summary>
/// Maintains and atomically persists ordered quick-launch pins for every local user. The service
/// deliberately knows nothing about the live app catalog: syntactically valid unavailable IDs are
/// retained so temporarily disabled or unmounted applications regain their pins when available.
/// </summary>
public sealed class StartMenuPreferencesService : IDisposable
{
    private static readonly IReadOnlyList<string> EmptyPins = Array.AsReadOnly(Array.Empty<string>());

    private static readonly AppOperationContext SystemContext = new()
    {
        AppId = "org.hackeros.shell",
        UserId = "system",
        UserAuthority = AppAuthority.System,
        GrantedCapabilities = new HashSet<string>(
            [AppCapabilities.SettingsSystemRead, AppCapabilities.SettingsSystemWrite],
            StringComparer.Ordinal),
        IsSystemOperation = true
    };

    private readonly ISettingsDocumentService _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<LocalUserId, IReadOnlyList<string>> _profiles = [];
    private long _revision;
    private bool _initialized;

    /// <summary>Creates a scoped start-menu preference service over the canonical settings service.</summary>
    /// <param name="settings">Authorized settings document service used for atomic reads and writes.</param>
    public StartMenuPreferencesService(ISettingsDocumentService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>Raised whenever an optimistic update or reconciliation changes any user's pins.</summary>
    public event Action? Changed;

    /// <summary>Gets an immutable snapshot of one user's ordered pinned app IDs.</summary>
    /// <param name="userId">Opaque local user whose isolated profile is requested.</param>
    /// <returns>The ordered pins, or an empty list when that user has no stored profile.</returns>
    public IReadOnlyList<string> GetPinnedAppIds(LocalUserId userId)
    {
        ValidateUserId(userId);
        return _profiles.TryGetValue(userId, out IReadOnlyList<string>? pins) ? pins : EmptyPins;
    }

    /// <summary>Loads all persisted user profiles without emitting an initial change notification.</summary>
    /// <param name="cancellationToken">Signals cancellation of the settings read.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RefreshCoreAsync(raiseEvent: false, cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Appends a syntactically valid app ID to one user's quick-launch section.</summary>
    /// <param name="userId">Owner of the isolated pin profile.</param>
    /// <param name="appId">Manifest-compatible app ID; catalog presence is intentionally not required.</param>
    /// <param name="cancellationToken">Signals cancellation of persistence.</param>
    /// <returns><see langword="true"/> when a new pin was committed.</returns>
    public Task<bool> PinAsync(
        LocalUserId userId,
        string appId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateAppId(appId);
        return MutateAsync(userId, pins =>
        {
            if (pins.Contains(appId, StringComparer.Ordinal)
                || pins.Count >= StartMenuSettingsDocuments.MaximumPinnedAppCount)
            {
                return false;
            }

            pins.Add(appId);
            return true;
        }, cancellationToken);
    }

    /// <summary>Removes an app ID from one user's quick-launch section, including unavailable apps.</summary>
    /// <param name="userId">Owner of the isolated pin profile.</param>
    /// <param name="appId">Exact persisted app ID to remove.</param>
    /// <param name="cancellationToken">Signals cancellation of persistence.</param>
    /// <returns><see langword="true"/> when an existing pin was committed as removed.</returns>
    public Task<bool> UnpinAsync(
        LocalUserId userId,
        string appId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateAppId(appId);
        return MutateAsync(userId, pins => pins.Remove(appId), cancellationToken);
    }

    /// <summary>Toggles one syntactically valid app ID while preserving every other ordered pin.</summary>
    /// <param name="userId">Owner of the isolated pin profile.</param>
    /// <param name="appId">Exact manifest-compatible app ID to toggle.</param>
    /// <param name="cancellationToken">Signals cancellation of persistence.</param>
    /// <returns><see langword="true"/> when the toggle was committed.</returns>
    public Task<bool> ToggleAsync(
        LocalUserId userId,
        string appId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateAppId(appId);
        return MutateAsync(userId, pins =>
        {
            int existingIndex = pins.FindIndex(pin => string.Equals(pin, appId, StringComparison.Ordinal));
            if (existingIndex >= 0)
            {
                pins.RemoveAt(existingIndex);
                return true;
            }

            if (pins.Count >= StartMenuSettingsDocuments.MaximumPinnedAppCount)
            {
                return false;
            }

            pins.Add(appId);
            return true;
        }, cancellationToken);
    }

    /// <summary>Moves one persisted pin to a zero-based target index without disturbing other profiles.</summary>
    /// <param name="userId">Owner of the isolated pin profile.</param>
    /// <param name="appId">Exact persisted app ID to move.</param>
    /// <param name="targetIndex">Zero-based destination within the existing list.</param>
    /// <param name="cancellationToken">Signals cancellation of persistence.</param>
    /// <returns><see langword="true"/> when an order change was committed; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The destination is negative or outside an existing list.</exception>
    public Task<bool> MoveAsync(
        LocalUserId userId,
        string appId,
        int targetIndex,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateAppId(appId);
        ArgumentOutOfRangeException.ThrowIfNegative(targetIndex);

        return MutateAsync(userId, pins =>
        {
            int currentIndex = pins.FindIndex(pin => string.Equals(pin, appId, StringComparison.Ordinal));
            if (currentIndex < 0)
            {
                return false;
            }

            if (targetIndex >= pins.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(targetIndex));
            }

            if (currentIndex == targetIndex)
            {
                return false;
            }

            pins.RemoveAt(currentIndex);
            pins.Insert(targetIndex, appId);
            return true;
        }, cancellationToken);
    }

    private async Task<bool> MutateAsync(
        LocalUserId userId,
        Func<List<string>, bool> mutation,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                await RefreshCoreAsync(raiseEvent: false, cancellationToken).ConfigureAwait(false);
                _initialized = true;
            }

            Dictionary<LocalUserId, IReadOnlyList<string>> persistedProfiles = _profiles;
            long persistedRevision = _revision;
            Dictionary<LocalUserId, IReadOnlyList<string>> candidate = CloneProfiles(_profiles);
            List<string> pins = candidate.TryGetValue(userId, out IReadOnlyList<string>? existing)
                ? [.. existing]
                : [];

            if (!mutation(pins))
            {
                return false;
            }

            if (pins.Count == 0)
            {
                candidate.Remove(userId);
            }
            else
            {
                candidate[userId] = Array.AsReadOnly(pins.ToArray());
            }

            // Apply before I/O for an immediately responsive shell. A rejected optimistic write is
            // reconciled from the canonical document below, including a visible rollback if needed.
            ApplyProfiles(candidate, persistedRevision, raiseEvent: true);
            SettingsWriteResult write;
            try
            {
                write = await _settings.WriteAsync(
                    new SettingsWriteRequest(
                        StartMenuSettingsDocuments.Path,
                        StartMenuSettingsCodec.Encode(candidate),
                        persistedRevision),
                    SystemContext,
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Cancellation and transport/storage failures must not leave the scoped projection
                // claiming an uncommitted pin order.
                ApplyProfiles(persistedProfiles, persistedRevision, raiseEvent: true);
                throw;
            }

            if (write is { Status: SettingsWriteStatus.Success, Document: { } committed }
                && StartMenuSettingsCodec.TryDecode(committed.Content, out Dictionary<LocalUserId, IReadOnlyList<string>> committedProfiles))
            {
                ApplyProfiles(committedProfiles, committed.Revision, raiseEvent: true);
                return true;
            }

            if (!await RefreshCoreAsync(raiseEvent: true, cancellationToken).ConfigureAwait(false))
            {
                ApplyProfiles(persistedProfiles, persistedRevision, raiseEvent: true);
            }

            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> RefreshCoreAsync(bool raiseEvent, CancellationToken cancellationToken)
    {
        SettingsReadResult read = await _settings.ReadAsync(
            StartMenuSettingsDocuments.Path,
            SystemContext,
            cancellationToken).ConfigureAwait(false);

        if (read is not { Status: SettingsReadStatus.Success, Document: { } document }
            || !StartMenuSettingsCodec.TryDecode(
                document.Content,
                out Dictionary<LocalUserId, IReadOnlyList<string>> profiles))
        {
            return false;
        }

        ApplyProfiles(profiles, document.Revision, raiseEvent);
        return true;
    }

    private void ApplyProfiles(
        Dictionary<LocalUserId, IReadOnlyList<string>> profiles,
        long revision,
        bool raiseEvent)
    {
        bool changed = !ProfilesEqual(_profiles, profiles);
        _profiles = profiles;
        _revision = revision;
        if (raiseEvent && changed)
        {
            Changed?.Invoke();
        }
    }

    private static Dictionary<LocalUserId, IReadOnlyList<string>> CloneProfiles(
        IReadOnlyDictionary<LocalUserId, IReadOnlyList<string>> profiles) =>
        profiles.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()));

    private static bool ProfilesEqual(
        IReadOnlyDictionary<LocalUserId, IReadOnlyList<string>> left,
        IReadOnlyDictionary<LocalUserId, IReadOnlyList<string>> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach ((LocalUserId userId, IReadOnlyList<string> leftPins) in left)
        {
            if (!right.TryGetValue(userId, out IReadOnlyList<string>? rightPins)
                || !leftPins.SequenceEqual(rightPins, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateUserId(LocalUserId userId)
    {
        if (userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A non-empty local user ID is required.", nameof(userId));
        }
    }

    private static void ValidateAppId(string appId)
    {
        if (!StartMenuAppIdSyntax.IsValid(appId))
        {
            throw new ArgumentException(
                "A lowercase reverse-domain app ID with at least three segments is required.",
                nameof(appId));
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();
}
