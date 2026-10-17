using HackerOs.App.Abstractions;

namespace HackerOs.AppSdk.Administration;

/// <summary>
/// One local account as <see cref="HackUserAccounts.ListAsync(CancellationToken)"/> shows it, like a
/// line of <c>/etc/passwd</c> plus the status column of <c>passwd -S</c>; never the password hash.
/// </summary>
/// <param name="LoginName">Login name, e.g. <c>alice</c>.</param>
/// <param name="DisplayName">Name shown in the interface.</param>
/// <param name="Authority">Authority the account's sessions get (<see cref="AppAuthority.Administrator"/> or user).</param>
/// <param name="IsEnabled">Whether the account can start a session.</param>
/// <param name="HasPassword">Whether the account has a password (<c>P</c>) or none (<c>NP</c>).</param>
/// <param name="UpdatedAtUtc">Last change of the account, password included.</param>
/// <param name="IsCurrentUser">Whether this is the account of the calling session.</param>
public sealed record HackUserAccount(
    string LoginName,
    string DisplayName,
    AppAuthority Authority,
    bool IsEnabled,
    bool HasPassword,
    DateTimeOffset UpdatedAtUtc,
    bool IsCurrentUser)
{
    /// <summary>Gets whether the account's sessions are administrators.</summary>
    public bool IsAdministrator => Authority == AppAuthority.Administrator;
}

/// <summary>
/// The local accounts gateway behind <see cref="HackUserAccounts"/> (ADR 0120). Changing one's own
/// password needs no capability (the current password is checked, as <c>passwd</c> does through PAM);
/// resetting or removing another account's password needs <c>users.manage</c> and an administrator.
/// </summary>
public interface IAppUserAccountsGateway
{
    /// <summary>Lists every local account, without any password hash.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    ValueTask<IReadOnlyList<HackUserAccount>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Changes the session user's password, after checking the current one.</summary>
    /// <param name="currentPassword">The current password; empty when the account has none.</param>
    /// <param name="newPassword">The new password (8 to 256 characters, different from the current one).</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    /// <exception cref="UnauthorizedAccessException">The current password is wrong.</exception>
    /// <exception cref="ArgumentException">The new password breaks the rules.</exception>
    ValueTask ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>Sets another account's password, or removes it with <see langword="null"/> (<c>passwd -d</c>).</summary>
    /// <param name="loginName">The account to change.</param>
    /// <param name="newPassword">The new password, or <see langword="null"/> to leave the account without one.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    /// <exception cref="UnauthorizedAccessException">No <c>users.manage</c>, or no administrator confirmed.</exception>
    /// <exception cref="ArgumentException">The new password breaks the rules, or the account does not exist.</exception>
    /// <exception cref="InvalidOperationException">Removing the last enabled administrator's password.</exception>
    ValueTask ResetPasswordAsync(string loginName, string? newPassword, CancellationToken cancellationToken = default);
}
