using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Administration;
using HackerOs.Platform.Core.Sessions;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Administration;

/// <summary>
/// Trusted <see cref="IAppUserAccountsGateway"/> (ADR 0120): lists accounts and changes the session
/// user's own password with no capability (the current password is the proof, as <c>passwd</c>
/// checks it through PAM); resetting or removing another account's password checks
/// <c>users.manage</c> and asks the OS authentication agent for an administrator first.
/// </summary>
internal sealed class AppUserAccountsGateway : IAppUserAccountsGateway
{
    private readonly Func<LocalPasswordService>? _passwords;
    private readonly ICapabilityChecker _capabilities;
    private readonly AdministratorGate _gate;

    /// <summary>Creates the gateway for one app instance.</summary>
    /// <param name="passwords">Lazily resolved password service; <see langword="null"/> on a host without accounts.</param>
    /// <param name="capabilities">The app's capability checker.</param>
    /// <param name="gate">Administrator authentication for this app.</param>
    public AppUserAccountsGateway(Func<LocalPasswordService>? passwords, ICapabilityChecker capabilities, AdministratorGate gate)
    {
        _passwords = passwords;
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<HackUserAccount>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LocalUser> users = await Service().ListAsync(cancellationToken).ConfigureAwait(false);
        return users
            .OrderBy(user => user.LoginName.Value, StringComparer.Ordinal)
            .Select(user => new HackUserAccount(
                user.LoginName.Value,
                user.DisplayName,
                user.Authority,
                user.Enabled,
                user.Credential is not null,
                user.UpdatedAtUtc,
                user.Id == _gate.Principal.UserId))
            .ToArray();
    }

    /// <inheritdoc />
    public ValueTask ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default) =>
        Service().ChangeOwnPasswordAsync(_gate.Principal, currentPassword, newPassword, cancellationToken);

    /// <inheritdoc />
    public async ValueTask ResetPasswordAsync(string loginName, string? newPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loginName);
        LocalPasswordService service = Service();
        _capabilities.Require(AppCapabilities.UsersManage);
        bool remove = newPassword is null;
        string admin = await _gate.AuthenticateAsync(
            _gate.Text[remove ? "Admin.RemovePassword" : "Admin.ResetPassword", loginName],
            remove ? "users.password-delete" : "users.password-reset",
            loginName,
            cancellationToken).ConfigureAwait(false);
        await service.ResetPasswordAsync(_gate.Principal, admin, loginName, newPassword, cancellationToken).ConfigureAwait(false);
    }

    private LocalPasswordService Service() =>
        _passwords?.Invoke() ?? throw new NotSupportedException("This HackerOS host cannot change passwords.");
}
