using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Infrastructure.Browser.FileSystem;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Diagnostics;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Platform.Core.Settings;
using HackerOs.Platform.Core.Time;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Ecosystem;

/// <summary>Executes the deterministic persistent boot sequence before authentication is offered.</summary>
public sealed class EcosystemBootCoordinator
{
    private readonly IndexedDbFileSystemBootstrapper _fileSystemBootstrapper;
    private readonly SettingsStoreToFilesMigration _settingsMigration;
    private readonly IPersistentCapabilityGrantRepository _grants;
    private readonly IPersistentAppCatalogRepository _catalog;
    private readonly AppCatalog _selectedCatalog;
    private readonly AppEnablementRegistry _enablement;
    private readonly ILocalGroupRepository _groups;
    private readonly ILocalUserRepository _users;
    private readonly LogFileWriter? _logWriter;
    private readonly IFileSystemService? _fileSystem;
    private readonly SimulationClockDriver? _clockDriver;

    /// <summary>Initializes the coordinator with every boot-critical persistent boundary.</summary>
    /// <param name="logWriter">
    /// Platform log writer attached to <paramref name="fileSystem"/> once storage is ready, so lines
    /// queued since startup reach <c>/var/log</c> (ADR 0045). Optional for headless tests.
    /// </param>
    /// <param name="fileSystem">Mounted filesystem the log writer is attached to.</param>
    /// <param name="clockDriver">Driver started at boot so simulated time advances (ADR 0012).</param>
    public EcosystemBootCoordinator(
        IndexedDbFileSystemBootstrapper fileSystemBootstrapper,
        SettingsStoreToFilesMigration settingsMigration,
        IPersistentCapabilityGrantRepository grants,
        IPersistentAppCatalogRepository catalog,
        AppCatalog selectedCatalog,
        AppEnablementRegistry enablement,
        ILocalGroupRepository groups,
        ILocalUserRepository users,
        LogFileWriter? logWriter = null,
        IFileSystemService? fileSystem = null,
        SimulationClockDriver? clockDriver = null)
    {
        _fileSystemBootstrapper = fileSystemBootstrapper
            ?? throw new ArgumentNullException(nameof(fileSystemBootstrapper));
        _settingsMigration = settingsMigration ?? throw new ArgumentNullException(nameof(settingsMigration));
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _selectedCatalog = selectedCatalog ?? throw new ArgumentNullException(nameof(selectedCatalog));
        _enablement = enablement ?? throw new ArgumentNullException(nameof(enablement));
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _logWriter = logWriter;
        _fileSystem = fileSystem;
        _clockDriver = clockDriver;
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

        // Simulated time starts flowing once storage exists; scheduled work (log flushes, the
        // taskbar clock, process stop timeouts) depends on it.
        _clockDriver?.Start();
        await AttachLogWriterAsync(cancellationToken).ConfigureAwait(false);

        // After the canonical database/root is ready, these subsystem checks use
        // independent object-store transactions and can safely progress together.
        // Settings are files (ADR 0044): ensure /etc/hackeros exists, copy the retired IndexedDB
        // settings store once, and write the commented installation defaults when absent.
        Task settingsTask = _settingsMigration.MigrateSystemAsync(cancellationToken).AsTask();
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

        // Hydrate live enablement enforcement from durable state (ADR 0032) — without this, a
        // disable from a prior run (or a future sync pull) would only be enforced once someone
        // happened to toggle the app again, not from the first launch attempt after boot.
        _enablement.MarkDisabled(catalog.Where(entry => !entry.IsEnabled).Select(entry => entry.Manifest.Id));

        _logWriter?.Write(
            LogFileTarget.Syslog,
            DiagnosticSeverity.Information,
            "kernel",
            $"boot: storage ready, policy revision {policyRevision}, {catalog.Count} apps in catalog, {users.Count} local users");

        return new EcosystemBootResult(users, policyRevision, catalog.Count);
    }

    /// <summary>
    /// Attaches the log writer to the mounted filesystem. A logging failure never blocks boot: the
    /// writer notes it in the in-memory buffer and lines stay queued.
    /// </summary>
    private async ValueTask AttachLogWriterAsync(CancellationToken cancellationToken)
    {
        if (_logWriter is null || _fileSystem is null)
        {
            return;
        }

        try
        {
            await _logWriter.AttachAsync(_fileSystem, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Deliberately swallowed: see the summary. The memory buffer already has the detail.
        }
    }
}

/// <summary>Contains persistent readiness facts produced by one successful boot.</summary>
public sealed record EcosystemBootResult(
    IReadOnlyList<LocalUser> Users,
    long PolicyRevision,
    int CatalogEntryCount);
