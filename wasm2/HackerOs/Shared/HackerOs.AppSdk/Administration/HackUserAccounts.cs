using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.AppSdk.Administration;

/// <summary>
/// Local accounts and their passwords (ADR 0120), what <c>passwd</c> does on Linux. Anyone changes
/// their own password by giving the current one; resetting or removing another account's password
/// needs the dangerous capability <c>users.manage</c> and an administrator, and sets that account's
/// keyring aside (its secrets stay sealed under the old password).
/// </summary>
public static class HackUserAccounts
{
    /// <summary>The shortest password accepted, as when the first account is created.</summary>
    public const int MinimumPasswordLength = 8;

    /// <summary>The longest password accepted.</summary>
    public const int MaximumPasswordLength = 256;

    /// <summary>Lists every local account (ambient context).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static Task<IReadOnlyList<HackUserAccount>> ListAsync(CancellationToken cancellationToken = default) =>
        ListAsync(HackExecutionContext.Current, cancellationToken);

    /// <summary>Lists every local account.</summary>
    /// <param name="context">The calling app's context.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static Task<IReadOnlyList<HackUserAccount>> ListAsync(IAppExecutionContext context, CancellationToken cancellationToken = default) =>
        Call(context, gateway => gateway.ListAsync(cancellationToken));

    /// <summary>Changes the session user's password (ambient context).</summary>
    /// <param name="currentPassword">The current password; empty when the account has none.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    public static Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default) =>
        ChangePasswordAsync(HackExecutionContext.Current, currentPassword, newPassword, cancellationToken);

    /// <summary>Changes the session user's password, after checking the current one.</summary>
    /// <param name="context">The calling app's context.</param>
    /// <param name="currentPassword">The current password; empty when the account has none.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    /// <exception cref="UnauthorizedAccessException">The current password is wrong.</exception>
    /// <exception cref="ArgumentException">The new password breaks the rules (see <see cref="ValidateNewPassword"/>).</exception>
    public static Task ChangePasswordAsync(
        IAppExecutionContext context,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentPassword);
        ThrowIfInvalid(newPassword);
        return Call(context, async gateway =>
        {
            await gateway.ChangePasswordAsync(currentPassword, newPassword, cancellationToken).ConfigureAwait(false);
            return true;
        });
    }

    /// <summary>Sets another account's password (ambient context).</summary>
    /// <param name="loginName">The account to change.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    public static Task ResetPasswordAsync(string loginName, string newPassword, CancellationToken cancellationToken = default) =>
        ResetPasswordAsync(HackExecutionContext.Current, loginName, newPassword, cancellationToken);

    /// <summary>
    /// Sets another account's password, as <c>sudo passwd NAME</c>: needs <c>users.manage</c> and an
    /// administrator; the account's keyring is set aside.
    /// </summary>
    /// <param name="context">The calling app's context.</param>
    /// <param name="loginName">The account to change.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    /// <exception cref="UnauthorizedAccessException">No <c>users.manage</c>, or no administrator confirmed.</exception>
    /// <exception cref="ArgumentException">The new password breaks the rules, or the account does not exist.</exception>
    public static Task ResetPasswordAsync(
        IAppExecutionContext context,
        string loginName,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loginName);
        ThrowIfInvalid(newPassword);
        return Call(context, async gateway =>
        {
            await gateway.ResetPasswordAsync(loginName, newPassword, cancellationToken).ConfigureAwait(false);
            return true;
        });
    }

    /// <summary>Removes another account's password (ambient context).</summary>
    /// <param name="loginName">The account to change.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    public static Task RemovePasswordAsync(string loginName, CancellationToken cancellationToken = default) =>
        RemovePasswordAsync(HackExecutionContext.Current, loginName, cancellationToken);

    /// <summary>
    /// Leaves an account without a password, as <c>passwd -d NAME</c>: needs <c>users.manage</c> and an
    /// administrator. Refused for the last enabled administrator.
    /// </summary>
    /// <param name="context">The calling app's context.</param>
    /// <param name="loginName">The account to change.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    /// <exception cref="UnauthorizedAccessException">No <c>users.manage</c>, or no administrator confirmed.</exception>
    /// <exception cref="InvalidOperationException">The account is the last enabled administrator.</exception>
    public static Task RemovePasswordAsync(IAppExecutionContext context, string loginName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loginName);
        return Call(context, async gateway =>
        {
            await gateway.ResetPasswordAsync(loginName, null, cancellationToken).ConfigureAwait(false);
            return true;
        });
    }

    /// <summary>
    /// Checks a new password against the rules of ADR 0120: between <see cref="MinimumPasswordLength"/>
    /// and <see cref="MaximumPasswordLength"/> characters, with no control character.
    /// </summary>
    /// <param name="newPassword">The candidate password.</param>
    /// <returns><see langword="null"/> when valid; otherwise the reason, in the words of <c>passwd</c>.</returns>
    public static string? ValidateNewPassword(string? newPassword)
    {
        if (string.IsNullOrEmpty(newPassword))
        {
            return "No password has been supplied.";
        }

        if (newPassword.Length < MinimumPasswordLength)
        {
            return $"The password is shorter than {MinimumPasswordLength} characters.";
        }

        if (newPassword.Length > MaximumPasswordLength)
        {
            return $"The password is longer than {MaximumPasswordLength} characters.";
        }

        return newPassword.Any(char.IsControl) ? "The password contains a control character." : null;
    }

    private static void ThrowIfInvalid(string newPassword)
    {
        if (ValidateNewPassword(newPassword) is { } reason)
        {
            throw new ArgumentException($"BAD PASSWORD: {reason}", nameof(newPassword));
        }
    }

    private static async Task<T> Call<T>(IAppExecutionContext context, Func<IAppUserAccountsGateway, ValueTask<T>> call)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return await call(context.UserAccounts).ConfigureAwait(false);
        }
        catch (AppGatewayAccessDeniedException exception)
        {
            throw HackGatewayErrors.ToUnauthorized(exception);
        }
    }
}
