using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Platform.Core.FileSystem;
using HackerOs.Platform.Core.Policy;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Sessions;

/// <summary>
/// In-memory <see cref="ISessionService"/> implementing the ADR 0013 session lifecycle state
/// machine, owning the session root cancellation source and provisioning the authenticated
/// user's home directory on login.
/// </summary>
public sealed class LocalSessionService : ISessionService
{
    private readonly ILocalUserRepository _users;
    private readonly FileSystemSeeder _homeSeeder;
    private readonly IEventBus _eventBus;
    private readonly IAuditLog _auditLog;
    private readonly InstallationId _installationId;
    private readonly DeviceId _deviceId;
    private readonly Func<DateTimeOffset> _clock;

    private readonly Lock _gate = new();
    private CancellationTokenSource? _rootTokenSource;
    private AuthenticatedPrincipal? _principal;

    private readonly KeyDerivationAsyncDelegate? _asyncHasher;
    private readonly ILoginProgressTracker _progressTracker;
    private readonly ICapabilityGrantRepository? _grantRepository;
    private readonly AppCatalog? _catalog;

    /// <summary>Initializes a session service in the <see cref="SessionState.Uninitialized"/> state.</summary>
    /// <param name="users">Repository used to look up and authenticate local users.</param>
    /// <param name="homeSeeder">Seeder that provisions a freshly authenticated user's home directory.</param>
    /// <param name="eventBus">Bus used to publish session lifecycle events.</param>
    /// <param name="auditLog">Audit log used to record login/logout/shutdown operations.</param>
    /// <param name="installationId">Identifier of the HackerOS installation/profile hosting this session.</param>
    /// <param name="deviceId">Identifier of the physical/browser device hosting this session.</param>
    /// <param name="clock">Clock used for event/audit timestamps; defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
    /// <param name="progressTracker">Optional tracker used to report login step progress.</param>
    /// <param name="asyncHasher">Optional asynchronous key derivation delegate (e.g. Web Crypto API interop).</param>
    /// <param name="grantRepository">Optional capability grant repository used to seed user capability grants on login.</param>
    /// <param name="catalog">Optional app catalog containing app manifests with declared capabilities.</param>
    public LocalSessionService(
        ILocalUserRepository users,
        FileSystemSeeder homeSeeder,
        IEventBus eventBus,
        IAuditLog auditLog,
        InstallationId installationId,
        DeviceId deviceId,
        Func<DateTimeOffset>? clock = null,
        ILoginProgressTracker? progressTracker = null,
        KeyDerivationAsyncDelegate? asyncHasher = null,
        ICapabilityGrantRepository? grantRepository = null,
        AppCatalog? catalog = null)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _homeSeeder = homeSeeder ?? throw new ArgumentNullException(nameof(homeSeeder));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
        _installationId = installationId;
        _deviceId = deviceId;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _progressTracker = progressTracker ?? NullLoginProgressTracker.Instance;
        _asyncHasher = asyncHasher;
        _grantRepository = grantRepository;
        _catalog = catalog;
    }

    /// <inheritdoc />
    public SessionState State { get; private set; } = SessionState.Uninitialized;

    /// <inheritdoc />
    public AuthenticatedPrincipal? CurrentPrincipal
    {
        get
        {
            lock (_gate)
            {
                return _principal;
            }
        }
    }

    /// <inheritdoc />
    public async Task<AuthenticatedPrincipal> LoginAsync(
        LocalLoginName loginName, string? password, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (State is not (SessionState.Uninitialized or SessionState.LoggedOut))
            {
                throw new InvalidOperationException($"Cannot log in while the session is {State}.");
            }

            State = SessionState.Starting;
        }

        _progressTracker.Reset();
        _progressTracker.SetTotalSteps(3);

        try
        {
            LocalUser? user;
            using (_progressTracker.BeginStep("Loading Profile"))
            {
                user = await _users.FindByLoginNameAsync(loginName, cancellationToken);
                if (user is null)
                {
                    RecordAudit(loginName.Value, "session.login", loginName.Value, AuditOutcome.Denied);
                    throw new InvalidOperationException($"No local user '{loginName}'.");
                }

                if (!user.Enabled)
                {
                    RecordAudit(user.Id.ToString(), "session.login", loginName.Value, AuditOutcome.Denied);
                    throw new InvalidOperationException($"User '{loginName}' is disabled.");
                }

                if (user.Credential is { } credential
                    && !await LocalPasswordHasher.VerifyAsync(password ?? string.Empty, credential, _asyncHasher, cancellationToken).ConfigureAwait(false))
                {
                    RecordAudit(user.Id.ToString(), "session.login", loginName.Value, AuditOutcome.Denied);
                    throw new InvalidOperationException("Invalid credentials.");
                }
            }

            await Task.Yield();

            using (_progressTracker.BeginStep("Ensure data integrity"))
            {
                await _homeSeeder.SeedAsync(user.LoginName.Value, user.PrimaryGroupId.ToString(), cancellationToken);
                if (_grantRepository is not null && _catalog is not null)
                {
                    string userId = user.Id.ToString();
                    foreach (AppManifest manifest in _catalog.Manifests.Values)
                    {
                        CleanProfileCapabilityGrantSeeder.SeedDeclaredCapabilities(_grantRepository, manifest, userId);
                    }
                }
            }

            await Task.Yield();

            SessionId sessionId = SessionId.FromGuid(Guid.NewGuid());
            List<LocalGroupId> groupIds = [user.PrimaryGroupId, .. user.AdditionalGroupIds];
            AuthenticatedPrincipal principal = new(
                sessionId,
                user.Id,
                user.LoginName,
                user.DisplayName,
                user.Authority,
                user.PrimaryGroupId,
                groupIds,
                _installationId,
                _deviceId,
                _clock());

            using (_progressTracker.BeginStep("Starting Session"))
            {
                lock (_gate)
                {
                    _rootTokenSource = new CancellationTokenSource();
                    _principal = principal;
                    State = SessionState.Active;
                }

                _eventBus.Publish(new SessionActivatedEvent(sessionId, user.Id, principal.AuthenticatedAtUtc));
                RecordAudit(user.Id.ToString(), "session.login", loginName.Value, AuditOutcome.Success);
            }

            return principal;
        }
        catch
        {
            lock (_gate)
            {
                if (State == SessionState.Starting)
                {
                    State = SessionState.LoggedOut;
                }
            }

            throw;
        }
    }

    /// <inheritdoc />
    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        AuthenticatedPrincipal principal;
        CancellationTokenSource rootSource;
        lock (_gate)
        {
            if (State != SessionState.Active)
            {
                throw new InvalidOperationException($"Cannot log out while the session is {State}.");
            }

            State = SessionState.LoggingOut;
            principal = _principal!;
            rootSource = _rootTokenSource!;
        }

        rootSource.Cancel();
        rootSource.Dispose();

        lock (_gate)
        {
            _principal = null;
            _rootTokenSource = null;
            State = SessionState.LoggedOut;
        }

        _eventBus.Publish(new SessionLoggedOutEvent(principal.SessionId, principal.UserId, _clock()));
        RecordAudit(principal.UserId.ToString(), "session.logout", principal.UserId.ToString(), AuditOutcome.Success);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        AuthenticatedPrincipal? principal;
        CancellationTokenSource? rootSource;
        lock (_gate)
        {
            if (State == SessionState.Stopped)
            {
                throw new InvalidOperationException("The session has already stopped.");
            }

            if (State is SessionState.Starting or SessionState.LoggingOut)
            {
                throw new InvalidOperationException($"Cannot shut down while the session is {State}.");
            }

            State = SessionState.ShuttingDown;
            principal = _principal;
            rootSource = _rootTokenSource;
        }

        rootSource?.Cancel();
        rootSource?.Dispose();

        lock (_gate)
        {
            _principal = null;
            _rootTokenSource = null;
            State = SessionState.Stopped;
        }

        _eventBus.Publish(new SessionShutDownEvent(principal?.SessionId, _clock()));
        RecordAudit(principal?.UserId.ToString() ?? "system", "session.shutdown", "session", AuditOutcome.Success);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public CancellationTokenSource CreateLinkedCancellationSource()
    {
        lock (_gate)
        {
            if (_rootTokenSource is null)
            {
                throw new InvalidOperationException("Cannot link a cancellation source before the session is active.");
            }

            return CancellationTokenSource.CreateLinkedTokenSource(_rootTokenSource.Token);
        }
    }

    private void RecordAudit(string actor, string action, string subject, AuditOutcome outcome) =>
        _auditLog.Record(new AuditEntry(_clock(), Guid.NewGuid(), actor, action, subject, outcome));
}
