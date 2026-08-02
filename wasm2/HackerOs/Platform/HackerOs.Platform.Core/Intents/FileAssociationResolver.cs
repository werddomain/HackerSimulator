using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Simulation.Abstractions;

namespace HackerOs.Platform.Core.Intents;

/// <summary>Identifies the stable outcome of resolving a file-open request to a handler app.</summary>
public enum FileHandlerResolutionStatus
{
    /// <summary>The intent's explicit preferred app was valid and will be used.</summary>
    ExplicitTarget,

    /// <summary>An administrator-configured default from the association document will be used.</summary>
    ConfiguredDefault,

    /// <summary>Exactly one enabled app declares handling this file; it will be used.</summary>
    SoleCandidate,

    /// <summary>Multiple enabled apps can handle this file; the caller must choose.</summary>
    ChooserRequired,

    /// <summary>No enabled app declares handling this file.</summary>
    NoHandler,

    /// <summary>The intent's explicit preferred app cannot handle this file or is disabled/unknown.</summary>
    TargetInvalid
}

/// <summary>Contains the result of resolving one <see cref="OpenFileIntent"/>, per `P1-APP-008`.</summary>
/// <param name="Status">Stable resolution outcome.</param>
/// <param name="AppId">Resolved target app ID, when resolution produced exactly one.</param>
/// <param name="CandidateAppIds">Every candidate app ID considered, ordinal-sorted for determinism.</param>
public sealed record FileHandlerResolution(
    FileHandlerResolutionStatus Status,
    string? AppId,
    IReadOnlyList<string> CandidateAppIds);

/// <summary>
/// Resolves which app should open or edit a file using the documented precedence order, per
/// `P1-APP-008` and `P1-APP-009`: an explicit request target first, then an administrator-configured
/// default, then any manifest-declared candidates. Never guesses or silently assigns a new default.
/// </summary>
public sealed class FileAssociationResolver
{
    private readonly AppCatalog _catalog;
    private readonly IAppEnablementRegistry _enablement;
    private readonly ISettingsDocumentService _settings;

    /// <summary>Initializes the resolver over a catalog, enablement registry, and settings service.</summary>
    public FileAssociationResolver(AppCatalog catalog, IAppEnablementRegistry enablement, ISettingsDocumentService settings)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _enablement = enablement ?? throw new ArgumentNullException(nameof(enablement));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>Resolves an <see cref="OpenFileIntent"/> to a target app, or to a chooser/failure outcome.</summary>
    /// <param name="intent">Intent describing the target file, action, and any explicit preference.</param>
    /// <param name="readContext">Trusted operation context used to read the association document.</param>
    public async ValueTask<FileHandlerResolution> ResolveAsync(OpenFileIntent intent, AppOperationContext readContext)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(readContext);

        string? extension = GetExtension(intent.Path);
        string action = intent.Action == FileIntentAction.Edit ? "edit" : "open";

        if (intent.PreferredAppId is { } preferredAppId)
        {
            bool valid = _enablement.IsEnabled(preferredAppId)
                && _catalog.Manifests.TryGetValue(preferredAppId, out AppManifest? preferredManifest)
                && HandlesFile(preferredManifest, extension, intent.MediaType, action);
            return valid
                ? new FileHandlerResolution(FileHandlerResolutionStatus.ExplicitTarget, preferredAppId, [preferredAppId])
                : new FileHandlerResolution(FileHandlerResolutionStatus.TargetInvalid, null, []);
        }

        SettingsReadResult read = await _settings.ReadAsync(FileAssociationSettingsDocuments.Path, readContext);
        if (read.Status == SettingsReadStatus.Success && read.Document is not null)
        {
            FileAssociationIndex index = FileAssociationIndex.Build(read.Document.Content);
            if (index.TryGetDefault(extension, intent.MediaType, action, out FileAssociationEntry entry)
                && _enablement.IsEnabled(entry.AppId)
                && _catalog.Manifests.ContainsKey(entry.AppId))
            {
                return new FileHandlerResolution(FileHandlerResolutionStatus.ConfiguredDefault, entry.AppId, [entry.AppId]);
            }
        }

        string[] candidates = [.. _catalog.Manifests.Values
            .Where(manifest => manifest.Kind == AppKind.Window
                && _enablement.IsEnabled(manifest.Id)
                && HandlesFile(manifest, extension, intent.MediaType, action))
            .Select(manifest => manifest.Id)
            .OrderBy(id => id, StringComparer.Ordinal)];

        return candidates.Length switch
        {
            0 => new FileHandlerResolution(FileHandlerResolutionStatus.NoHandler, null, []),
            1 => new FileHandlerResolution(FileHandlerResolutionStatus.SoleCandidate, candidates[0], candidates),
            _ => new FileHandlerResolution(FileHandlerResolutionStatus.ChooserRequired, null, candidates)
        };
    }

    private static bool HandlesFile(AppManifest manifest, string? extension, string? mediaType, string action) =>
        manifest.FileHandlers.Any(handler =>
            MatchesTarget(handler, extension, mediaType)
            && handler.Actions.Any(a => string.Equals(a, action, StringComparison.OrdinalIgnoreCase)));

    private static bool MatchesTarget(FileHandlerManifest handler, string? extension, string? mediaType) =>
        (extension is not null && handler.Extensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase)))
        || (mediaType is not null && string.Equals(handler.MediaType, mediaType, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Extracts a file's extension (including the leading dot) from its virtual path. There is no
    /// shared <c>VirtualPath</c> filename helper, so the last path segment and its last dot are
    /// located manually. A dotfile with no further dot (e.g. <c>.bashrc</c>) has no extension.
    /// </summary>
    private static string? GetExtension(VirtualPath path)
    {
        string value = path.Value;
        int lastSlash = value.LastIndexOf('/');
        string fileName = lastSlash >= 0 ? value[(lastSlash + 1)..] : value;
        int lastDot = fileName.LastIndexOf('.');
        return lastDot > 0 ? fileName[lastDot..] : null;
    }
}
