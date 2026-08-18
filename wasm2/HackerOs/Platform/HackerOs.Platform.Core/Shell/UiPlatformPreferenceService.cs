using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;

namespace HackerOs.Platform.Core.Shell;

/// <summary>Whether the active platform follows automatic detection or an explicit user choice.</summary>
public enum UiPlatformSelectionSource
{
    /// <summary>Follows <see cref="IPlatformEnvironmentProbe"/>.</summary>
    Auto,

    /// <summary>Follows the persisted <see cref="UiPlatformPreferenceState.ExplicitPlatformId"/>.</summary>
    Explicit
}

/// <summary>Live UI-platform preference state.</summary>
/// <param name="SelectionSource">Whether the choice is automatic or explicit.</param>
/// <param name="ExplicitPlatformId">The explicit platform, when <see cref="SelectionSource"/> is <see cref="UiPlatformSelectionSource.Explicit"/>.</param>
/// <param name="ActivePlatform">
/// The platform effective right now: <see cref="ExplicitPlatformId"/> when explicit, otherwise the
/// environment probe's current suggestion.
/// </param>
public sealed record UiPlatformPreferenceState(
    UiPlatformSelectionSource SelectionSource,
    AppPlatformId? ExplicitPlatformId,
    AppPlatformId ActivePlatform);

/// <summary>
/// Owns the device-local <c>UiPlatformPreference</c> settings document (see
/// <see cref="UiPlatformPreferenceSettingsDocuments"/>) and exposes it live to the shell.
/// </summary>
/// <remarks>
/// <para>
/// This is a first Phase-0 slice of docs/mobile-interface-platform-plan.md §6 — it persists the
/// Auto/Desktop/Mobile choice and recomputes <see cref="UiPlatformPreferenceState.ActivePlatform"/>
/// live, but does not itself change shell rendering. The full controlled 9-step switch sequence
/// (§6.3: warn dirty apps, stop UI instances with a typed <c>PlatformChanged</c> reason, rebuild the
/// shell, etc.) is a later phase — a future consumer subscribes to <see cref="Changed"/> and drives
/// that sequence without this service needing to change.
/// </para>
/// <para>
/// This service reads/writes the settings document directly through <see cref="ISettingsDocumentService"/>
/// rather than through an app-scoped <c>IAppSettingsGateway</c> (compare the Appearance app's
/// persistence service): shell chrome is not a launched app instance, so it has no app-scoped
/// gateway to use. It instead builds its own trusted, audited <see cref="AppOperationContext"/>
/// with <see cref="AppOperationContext.IsSystemOperation"/> set — the same pattern
/// <c>SettingsSyncService</c> already uses for OS-level background settings access.
/// </para>
/// </remarks>
public sealed class UiPlatformPreferenceService : IDisposable
{
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
    private readonly IPlatformEnvironmentProbe _probe;

    private UiPlatformSelectionSource _selectionSource = UiPlatformSelectionSource.Auto;
    private AppPlatformId? _explicitPlatformId;
    private long _revision;

    /// <summary>Creates the preference service over the canonical settings document service and environment probe.</summary>
    public UiPlatformPreferenceService(ISettingsDocumentService settings, IPlatformEnvironmentProbe probe)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _probe.Changed += OnProbeChanged;
    }

    /// <summary>Gets the current preference state. Reflects the document's clean-profile default until <see cref="InitializeAsync"/> completes.</summary>
    public UiPlatformPreferenceState Current { get; private set; } =
        new(UiPlatformSelectionSource.Auto, null, WellKnownAppPlatforms.Desktop);

    /// <summary>Raised when <see cref="Current"/> changes, whether from a user choice or the environment probe.</summary>
    public event Action? Changed;

    /// <summary>Loads the persisted preference and starts the environment probe.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _probe.InitializeAsync(cancellationToken).ConfigureAwait(false);

        SettingsReadResult read = await _settings.ReadAsync(
            UiPlatformPreferenceSettingsDocuments.Path, SystemContext, cancellationToken).ConfigureAwait(false);
        if (read is { Status: SettingsReadStatus.Success, Document: { } document })
        {
            ApplyDocument(document.Content, document.Revision);
        }

        RecomputeAndNotify(raiseEvent: false);
    }

    /// <summary>Sets an explicit platform choice and persists it.</summary>
    public Task SetExplicitAsync(AppPlatformId platformId, CancellationToken cancellationToken = default) =>
        WriteAsync(UiPlatformSelectionSource.Explicit, platformId, cancellationToken);

    /// <summary>Clears any explicit choice and reverts to automatic detection.</summary>
    public Task ClearToAutoAsync(CancellationToken cancellationToken = default) =>
        WriteAsync(UiPlatformSelectionSource.Auto, null, cancellationToken);

    private async Task WriteAsync(
        UiPlatformSelectionSource selectionSource,
        AppPlatformId? explicitPlatformId,
        CancellationToken cancellationToken)
    {
        string content = Serialize(selectionSource, explicitPlatformId);
        SettingsWriteResult write = await _settings.WriteAsync(
            new SettingsWriteRequest(UiPlatformPreferenceSettingsDocuments.Path, content, _revision),
            SystemContext,
            cancellationToken).ConfigureAwait(false);

        if (write is { Status: SettingsWriteStatus.Success, Document: { } document })
        {
            ApplyDocument(document.Content, document.Revision);
            RecomputeAndNotify(raiseEvent: true);
            return;
        }

        // Optimistic-concurrency conflict or a rejected write: re-read to reconcile with whatever
        // is actually persisted rather than leaving in-memory state pointing at a stale revision.
        SettingsReadResult read = await _settings.ReadAsync(
            UiPlatformPreferenceSettingsDocuments.Path, SystemContext, cancellationToken).ConfigureAwait(false);
        if (read is { Status: SettingsReadStatus.Success, Document: { } current })
        {
            ApplyDocument(current.Content, current.Revision);
            RecomputeAndNotify(raiseEvent: true);
        }
    }

    private void ApplyDocument(string content, long revision)
    {
        _revision = revision;
        using JsonDocument document = JsonDocument.Parse(content);
        JsonElement root = document.RootElement;

        _selectionSource = root.TryGetProperty("selectionSource", out JsonElement sourceElement)
            && sourceElement.GetString() == "explicit"
                ? UiPlatformSelectionSource.Explicit
                : UiPlatformSelectionSource.Auto;

        _explicitPlatformId = _selectionSource == UiPlatformSelectionSource.Explicit
            && root.TryGetProperty("explicitPlatformId", out JsonElement idElement)
            && AppPlatformId.TryParse(idElement.GetString(), out AppPlatformId parsed)
                ? parsed
                : null;
    }

    private static string Serialize(UiPlatformSelectionSource selectionSource, AppPlatformId? explicitPlatformId)
    {
        string sourceText = selectionSource == UiPlatformSelectionSource.Explicit ? "explicit" : "auto";
        string idText = explicitPlatformId is { } id ? $"\"{id.Value}\"" : "null";
        return $"{{\"schemaVersion\":1,\"selectionSource\":\"{sourceText}\",\"explicitPlatformId\":{idText}}}";
    }

    private void OnProbeChanged() => RecomputeAndNotify(raiseEvent: true);

    private void RecomputeAndNotify(bool raiseEvent)
    {
        AppPlatformId active = _selectionSource == UiPlatformSelectionSource.Explicit && _explicitPlatformId is { } explicitId
            ? explicitId
            : _probe.Current.SuggestedPlatform;

        UiPlatformPreferenceState next = new(_selectionSource, _explicitPlatformId, active);
        bool changed = next != Current;
        Current = next;

        if (raiseEvent && changed)
        {
            Changed?.Invoke();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _probe.Changed -= OnProbeChanged;
}
