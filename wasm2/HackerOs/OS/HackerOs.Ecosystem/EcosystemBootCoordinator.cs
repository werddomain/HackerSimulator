using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Infrastructure.Browser.FileSystem;
using HackerOs.Infrastructure.Browser.Settings;
using HackerOs.Platform.Core;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Ecosystem;

/// <summary>Executes the deterministic persistent boot sequence before authentication is offered.</summary>
public sealed class EcosystemBootCoordinator
{
    private readonly IndexedDbFileSystemBootstrapper _fileSystemBootstrapper;
    private readonly IndexedDbSettingsDocumentService _settings;
    private readonly IPersistentCapabilityGrantRepository _grants;
    private readonly IPersistentAppCatalogRepository _catalog;
    private readonly AppCatalog _selectedCatalog;
    private readonly ILocalGroupRepository _groups;
    private readonly ILocalUserRepository _users;

    /// <summary>Initializes the coordinator with every boot-critical persistent boundary.</summary>
    public EcosystemBootCoordinator(
        IndexedDbFileSystemBootstrapper fileSystemBootstrapper,
        IndexedDbSettingsDocumentService settings,
        IPersistentCapabilityGrantRepository grants,
        IPersistentAppCatalogRepository catalog,
        AppCatalog selectedCatalog,
        ILocalGroupRepository groups,
        ILocalUserRepository users)
    {
        _fileSystemBootstrapper = fileSystemBootstrapper
            ?? throw new ArgumentNullException(nameof(fileSystemBootstrapper));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _selectedCatalog = selectedCatalog ?? throw new ArgumentNullException(nameof(selectedCatalog));
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _users = users ?? throw new ArgumentNullException(nameof(users));
    }

    /// <summary>
    /// Validates every critical persistent subsystem in dependency order and returns enabled users.
    /// No volatile session or app lifecycle state is started by this operation.
    /// Throws if storage, policy, session, or catalog validation fails.
    /// </summary>
    public async ValueTask<EcosystemBootResult> BootAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Step 1: Storage Root Validation
        await _fileSystemBootstrapper.EnsureRootAsync(cancellationToken).ConfigureAwait(false);

        // After the canonical database/root is ready, these subsystem checks use
        // independent object-store transactions and can safely progress together.
        Task settingsTask = _settings.InitializeAsync(cancellationToken).AsTask();
        Task<long> policyRevisionTask = _grants.GetCurrentPolicyRevisionAsync(cancellationToken).AsTask();
        Task<IReadOnlyList<PersistedAppCatalogEntry>> catalogTask = _catalog.ReconcileAsync(
            _selectedCatalog.Manifests.Values,
            cancellationToken).AsTask();
        Task<LocalGroup?> administratorGroupTask = _groups.FindByNameAsync(
            LocalLoginName.Parse("administrators"),
            cancellationToken).AsTask();
        Task<IReadOnlyList<LocalUser>> usersTask = _users.GetAllAsync(cancellationToken).AsTask();

        await Task.WhenAll(
            settingsTask,
            policyRevisionTask,
            catalogTask,
            administratorGroupTask,
            usersTask).ConfigureAwait(false);

        long policyRevision = await policyRevisionTask.ConfigureAwait(false);
        if (policyRevision < 0)
        {
            throw new InvalidOperationException($"Invalid persistent policy revision ({policyRevision}). Storage may be corrupted.");
        }

        IReadOnlyList<PersistedAppCatalogEntry> catalog = await catalogTask.ConfigureAwait(false);
        IReadOnlyList<LocalUser> users = await usersTask.ConfigureAwait(false);

        return new EcosystemBootResult(users, policyRevision, catalog.Count);
    }
}

/// <summary>Contains persistent readiness facts produced by one successful boot.</summary>
public sealed record EcosystemBootResult(
    IReadOnlyList<LocalUser> Users,
    long PolicyRevision,
    int CatalogEntryCount);
