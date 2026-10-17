using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Infrastructure.Browser.FileSystem;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Apps;
using HackerOs.Platform.Core.Backup;
using HackerOs.Platform.Core.Diagnostics;
using HackerOs.Platform.Core.Execution;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Platform.Core.Processes;
using HackerOs.Platform.Core.Processes.Accounting;
using HackerOs.Platform.Core.Settings;
using HackerOs.Platform.Core.Storage;
using HackerOs.Platform.Core.Time;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Simulation.Abstractions.Workers;

namespace HackerOs.Ecosystem;

/// <summary>Executes the deterministic persistent boot sequence before authentication is offered.</summary>
public sealed class EcosystemBootCoordinator
{
    private readonly IndexedDbFileSystemBootstrapper _fileSystemBootstrapper;
    private readonly SettingsStoreToFilesMigration _settingsMigration;
    private readonly IPersistentCapabilityGrantRepository _grants;
    private readonly IPersistentAppCatalogRepository _catalog;
    private readonly AppCatalog _selectedCatalog;
    private readonly AppEnablementToFilesMigration _appsMigration;
    private readonly ILocalGroupRepository _groups;
    private readonly ILocalUserRepository _users;
    private readonly LogFileWriter? _logWriter;
    private readonly IFileSystemService? _fileSystem;
    private readonly SimulationClockDriver? _clockDriver;
    private readonly AppInstallationService? _installation;
    private readonly ResourceSamplingService? _resourceSampling;
    private readonly ResourceAccountingPlan? _resourceAccounting;
    private readonly SystemStartupSignal? _startup;
    private readonly IRemoteProcessHost? _remoteHost;
    private readonly IWorkerPoolStatus? _workers;
    private readonly ServiceStartupCoordinator? _serviceStartup;
    private readonly StorageSpaceMonitor? _spaceMonitor;
    private readonly SystemBackupService? _backup;

    /// <summary>Initializes the coordinator with every boot-critical persistent boundary.</summary>
    /// <param name="logWriter">
    /// Platform log writer attached to <paramref name="fileSystem"/> once storage is ready, so lines
    /// queued since startup reach <c>/var/log</c> (ADR 0045). Optional for headless tests.
    /// </param>
    /// <param name="fileSystem">Mounted filesystem the log writer is attached to.</param>
    /// <param name="appsMigration">Installs <c>/etc/hackeros/apps.config</c> and copies the retired enablement flags into it (ADR 0048).</param>
    /// <param name="clockDriver">Driver started at boot so simulated time advances (ADR 0012).</param>
    /// <param name="installation">
    /// Installation service started once the machine file exists, so an app uninstalled in
    /// <c>apps.config</c> is blocked from the first launch attempt. Optional for headless tests.
    /// </param>
    /// <param name="resourceSampling">
    /// Scheduler tick started with the clock, so <c>/proc/stat</c>, <c>status</c> and
    /// <c>loadavg</c> move (ADR 0057). Optional for headless tests.
    /// </param>
    /// <param name="resourceAccounting">
    /// Resource accounting decided at composition (ADR 0066); its notices (state of each measurement,
    /// settings ignored because the build left a measurement out) are written to
    /// <c>/var/log/syslog</c> at boot. Optional for headless tests.
    /// </param>
    /// <param name="startup">
    /// The "system started" signal of ADR 0071, section 2, raised by <see cref="MarkSystemStarted"/>.
    /// </param>
    /// <param name="remoteHost">
    /// The out-of-thread process host (ADR 0070), registered only by the WebAssembly hosts. Without it,
    /// the boot notes in <c>/var/log/syslog</c> that <c>AppExecutionHost</c> is ignored (section 6).
    /// </param>
    /// <param name="workers">The worker pool, for the boot notice of its size and ceiling.</param>
    /// <param name="spaceMonitor">
    /// Free-space monitor of ADR 0099 (section 2), started once storage exists: low-space warning
    /// and root reserve. Optional for headless tests.
    /// </param>
    /// <param name="backup">
    /// Backup service whose restore record, kept across the restart, is written to
    /// <c>/var/log/auth.log</c> at boot (ADR 0099, section 1). Optional for headless tests.
    /// </param>
    public EcosystemBootCoordinator(
        IndexedDbFileSystemBootstrapper fileSystemBootstrapper,
        SettingsStoreToFilesMigration settingsMigration,
        IPersistentCapabilityGrantRepository grants,
        IPersistentAppCatalogRepository catalog,
        AppCatalog selectedCatalog,
        AppEnablementToFilesMigration appsMigration,
        ILocalGroupRepository groups,
        ILocalUserRepository users,
        LogFileWriter? logWriter = null,
        IFileSystemService? fileSystem = null,
        SimulationClockDriver? clockDriver = null,
        AppInstallationService? installation = null,
        ResourceSamplingService? resourceSampling = null,
        ResourceAccountingPlan? resourceAccounting = null,
        SystemStartupSignal? startup = null,
        IRemoteProcessHost? remoteHost = null,
        IWorkerPoolStatus? workers = null,
        ServiceStartupCoordinator? serviceStartup = null,
        StorageSpaceMonitor? spaceMonitor = null,
        SystemBackupService? backup = null)
    {
        _fileSystemBootstrapper = fileSystemBootstrapper
            ?? throw new ArgumentNullException(nameof(fileSystemBootstrapper));
        _settingsMigration = settingsMigration ?? throw new ArgumentNullException(nameof(settingsMigration));
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _selectedCatalog = selectedCatalog ?? throw new ArgumentNullException(nameof(selectedCatalog));
        _appsMigration = appsMigration ?? throw new ArgumentNullException(nameof(appsMigration));
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _logWriter = logWriter;
        _fileSystem = fileSystem;
        _clockDriver = clockDriver;
        _installation = installation;
        _resourceSampling = resourceSampling;
        _resourceAccounting = resourceAccounting;
        _startup = startup;
        _remoteHost = remoteHost;
        _workers = workers;
        _serviceStartup = serviceStartup;
        _spaceMonitor = spaceMonitor;
        _backup = backup;
    }

    /// <summary>
    /// Raises the boot's end signal (ADR 0071, section 2): the session is open, the desktop shown and
    /// the startup apps launched. The worker pool then waits for the browser to be idle (at most 2 s)
    /// and warms up its ready worker and its cores. Idempotent; the host calls it once the desktop
    /// has rendered.
    /// </summary>
    public void MarkSystemStarted()
    {
        if (_startup is null || _startup.IsStarted)
        {
            return;
        }

        _startup.MarkStarted();
        _logWriter?.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, "kernel", "boot: system started (desktop shown)");
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
        _resourceSampling?.Start();
        await AttachLogWriterAsync(cancellationToken).ConfigureAwait(false);
        WriteResourceAccountingNotices();
        WriteWorkerNotice();
        _spaceMonitor?.Start();
        await WritePendingRestoreRecordAsync(cancellationToken).ConfigureAwait(false);

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

        // Uninstalls are files since ADR 0048: install /etc/hackeros/apps.config (copying the retired
        // IsEnabled flags once), then enforce it before any login so an uninstalled app is blocked
        // from the first launch attempt. The user layer is applied at each login.
        await _appsMigration.MigrateAsync(catalog, cancellationToken).ConfigureAwait(false);
        if (_installation is not null)
        {
            await _installation.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        _logWriter?.Write(
            LogFileTarget.Syslog,
            DiagnosticSeverity.Information,
            "kernel",
            $"boot: storage ready, policy revision {policyRevision}, {catalog.Count} apps in catalog, {users.Count} local users");

        // ADR 0097 section 4: the system services start before the login screen, as hackeros-svc.
        // A failure is logged and never blocks the boot.
        if (_serviceStartup is not null)
        {
            await _serviceStartup.StartBootServicesAsync().ConfigureAwait(false);
        }

        return new EcosystemBootResult(users, policyRevision, catalog.Count);
    }

    /// <summary>Writes the resource-accounting decisions of ADR 0066 to <c>/var/log/syslog</c>, like boot parameters in <c>dmesg</c>.</summary>
    private void WriteResourceAccountingNotices()
    {
        if (_logWriter is null || _resourceAccounting is null)
        {
            return;
        }

        foreach (string notice in _resourceAccounting.Notices)
        {
            _logWriter.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, ResourceAccountingPlan.LogCategory, notice);
        }
    }

    /// <summary>
    /// Notes in <c>/var/log/syslog</c> where headless processes run (ADR 0070): the worker pool's size
    /// and ceiling, or, on a host without Web Workers (the server, section 6), that the manifests'
    /// <c>execution</c> is ignored.
    /// </summary>
    private void WriteWorkerNotice()
    {
        if (_logWriter is null)
        {
            return;
        }

        string notice = _remoteHost is null
            ? "workers: no Web Worker on this host: AppExecutionHost (pool, dedicated) is ignored, every process runs in the host process (ADR 0070, section 6)"
            : _workers is null
                ? "workers: commands and services run in Web Workers"
                : $"workers: {_workers.PoolSize} pool worker(s), one per virtual core, plus one ready worker once the system is started; ceiling {_workers.Capacity} (navigator.hardwareConcurrency)";
        _logWriter.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, "workers", notice);
    }

    /// <summary>
    /// Writes the restore that ran just before this boot to <c>/var/log/auth.log</c> (ADR 0099,
    /// section 1). Never blocks the boot: a host without the record or a failure leaves it unwritten.
    /// </summary>
    private async ValueTask WritePendingRestoreRecordAsync(CancellationToken cancellationToken)
    {
        if (_backup is null)
        {
            return;
        }

        try
        {
            await _backup.WritePendingRestoreRecordAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logWriter?.Write(LogFileTarget.Syslog, DiagnosticSeverity.Warning, "backup", $"restore record unreadable: {exception.Message}");
        }
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
