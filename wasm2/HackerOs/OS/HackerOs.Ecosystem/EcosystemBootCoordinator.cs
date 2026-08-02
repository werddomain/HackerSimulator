using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Infrastructure.Browser.FileSystem;
using HackerOs.Infrastructure.Browser.Settings;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Ecosystem;

/// <summary>Executes the deterministic persistent boot sequence before authentication is offered.</summary>
public sealed class EcosystemBootCoordinator
{
    private readonly IndexedDbFileSystemBootstrapper _fileSystemBootstrapper;
    private readonly IndexedDbSettingsDocumentService _settings;
    private readonly IPersistentCapabilityGrantRepository _grants;
    private readonly IPersistentAppCatalogRepository _catalog;
    private readonly ILocalGroupRepository _groups;
    private readonly ILocalUserRepository _users;

    /// <summary>Initializes the coordinator with every boot-critical persistent boundary.</summary>
    public EcosystemBootCoordinator(
        IndexedDbFileSystemBootstrapper fileSystemBootstrapper,
        IndexedDbSettingsDocumentService settings,
        IPersistentCapabilityGrantRepository grants,
        IPersistentAppCatalogRepository catalog,
        ILocalGroupRepository groups,
        ILocalUserRepository users)
    {
        _fileSystemBootstrapper = fileSystemBootstrapper
            ?? throw new ArgumentNullException(nameof(fileSystemBootstrapper));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _users = users ?? throw new ArgumentNullException(nameof(users));
    }

    /// <summary>
    /// Validates every critical persistent subsystem in dependency order and returns enabled users.
    /// No volatile session or app lifecycle state is started by this operation.
    /// </summary>
    public async ValueTask<EcosystemBootResult> BootAsync(CancellationToken cancellationToken = default)
    {
        await _fileSystemBootstrapper.EnsureRootAsync(cancellationToken).ConfigureAwait(false);
        await _settings.InitializeAsync(cancellationToken).ConfigureAwait(false);
        long policyRevision = await _grants.GetCurrentPolicyRevisionAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<PersistedAppCatalogEntry> catalog = await _catalog.ReconcileAsync(
            selectedManifests: [],
            cancellationToken).ConfigureAwait(false);
        await _groups.FindByNameAsync(LocalLoginName.Parse("administrators"), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LocalUser> users = await _users.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return new EcosystemBootResult(users, policyRevision, catalog.Count);
    }
}

/// <summary>Contains persistent readiness facts produced by one successful boot.</summary>
public sealed record EcosystemBootResult(
    IReadOnlyList<LocalUser> Users,
    long PolicyRevision,
    int CatalogEntryCount);