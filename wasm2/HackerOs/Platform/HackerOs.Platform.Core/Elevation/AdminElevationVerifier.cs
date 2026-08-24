using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Platform.Core.Sessions;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Elevation;

/// <summary>
/// Verifies that a login belongs to an enabled Administrator, reusing the same lookup/hash-check
/// sequence as <see cref="HackerOs.Platform.Core.Sessions.LocalSessionService.LoginAsync"/>, but
/// without touching session state — safe to call while a different user's session is active
/// (`ADR 0042`).
/// </summary>
public sealed class AdminElevationVerifier : IAdminElevationVerifier
{
    private readonly ILocalUserRepository _users;
    private readonly IAuditLog _auditLog;
    private readonly Func<DateTimeOffset> _clock;
    private readonly KeyDerivationAsyncDelegate? _asyncHasher;

    /// <summary>Initializes the verifier over the user repository and audit log.</summary>
    public AdminElevationVerifier(
        ILocalUserRepository users,
        IAuditLog auditLog,
        Func<DateTimeOffset>? clock = null,
        KeyDerivationAsyncDelegate? asyncHasher = null)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _asyncHasher = asyncHasher;
    }

    /// <inheritdoc />
    public async Task<AdminElevationVerificationResult> VerifyAsync(
        string loginName, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loginName);

        LocalLoginName parsed;
        try
        {
            parsed = LocalLoginName.Parse(loginName);
        }
        catch (ArgumentException)
        {
            RecordAudit(loginName, loginName, AuditOutcome.Denied);
            return AdminElevationVerificationResult.Failed("invalid-login-name");
        }

        LocalUser? user = await _users.FindByLoginNameAsync(parsed, cancellationToken);
        if (user is null)
        {
            RecordAudit(loginName, loginName, AuditOutcome.Denied);
            return AdminElevationVerificationResult.Failed("user-not-found");
        }

        if (!user.Enabled)
        {
            RecordAudit(user.Id.ToString(), loginName, AuditOutcome.Denied);
            return AdminElevationVerificationResult.Failed("user-disabled");
        }

        if (!AppAuthorityPolicy.Satisfies(user.Authority, AppAuthority.Administrator))
        {
            RecordAudit(user.Id.ToString(), loginName, AuditOutcome.Denied);
            return AdminElevationVerificationResult.Failed("not-administrator");
        }

        if (user.Credential is not { } credential
            || !await LocalPasswordHasher.VerifyAsync(password ?? string.Empty, credential, _asyncHasher, cancellationToken)
                .ConfigureAwait(false))
        {
            RecordAudit(user.Id.ToString(), loginName, AuditOutcome.Denied);
            return AdminElevationVerificationResult.Failed("invalid-credentials");
        }

        RecordAudit(user.Id.ToString(), loginName, AuditOutcome.Success);
        return new AdminElevationVerificationResult(true, user.Id.ToString(), user.LoginName.Value, null);
    }

    private void RecordAudit(string actor, string subject, AuditOutcome outcome) =>
        _auditLog.Record(new AuditEntry(_clock(), Guid.NewGuid(), actor, "session.elevate", subject, outcome));
}
