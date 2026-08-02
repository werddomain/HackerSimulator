using HackerOs.App.Abstractions;

namespace HackerOs.Simulation.Abstractions.Settings;

/// <summary>
/// Identifies the outcome of authorizing a request to read or write one settings scope.
/// </summary>
public enum SettingsScopeAuthorizationReason
{
    /// <summary>The request is authorized.</summary>
    Authorized = 0,

    /// <summary>The requested scope was not declared by the app's manifest.</summary>
    ScopeNotDeclared = 1,

    /// <summary>Roaming access requires an explicit sync-eligibility capability grant.</summary>
    RoamingCapabilityRequired = 2,

    /// <summary>OS/admin access requires the protected system-settings capability.</summary>
    SystemSettingsCapabilityRequired = 3,

    /// <summary>OS/admin access requires Administrator or explicit audited System authority.</summary>
    AdministratorAuthorityRequired = 4
}

/// <summary>
/// Authorizes which settings scope a manifest-declared app may use for a given operation.
/// </summary>
/// <remarks>
/// App kind alone never grants elevated scope. Roaming access requires sync-eligibility
/// capability; OS/admin access requires protected policy plus Administrator/System authority.
/// This mirrors the scope rules recorded in ADR 0011.
/// </remarks>
public static class SettingsScopePolicy
{
    /// <summary>Authorizes one settings scope request for the acting operation context.</summary>
    /// <param name="scope">Settings scope the app is attempting to use.</param>
    /// <param name="manifestDeclaredScopes">Scopes the app's manifest declares it may use.</param>
    /// <param name="context">Trusted acting operation context.</param>
    /// <param name="hasRoamingSyncCapability">Whether trusted policy granted roaming sync-eligibility for this app/user.</param>
    /// <returns>The stable authorization reason; <see cref="SettingsScopeAuthorizationReason.Authorized"/> when permitted.</returns>
    public static SettingsScopeAuthorizationReason Authorize(
        SettingsScope scope,
        IReadOnlySet<SettingsScope> manifestDeclaredScopes,
        AppOperationContext context,
        bool hasRoamingSyncCapability)
    {
        ArgumentNullException.ThrowIfNull(manifestDeclaredScopes);
        ArgumentNullException.ThrowIfNull(context);

        if (!manifestDeclaredScopes.Contains(scope))
        {
            return SettingsScopeAuthorizationReason.ScopeNotDeclared;
        }

        return scope switch
        {
            SettingsScope.AppUser or SettingsScope.AppDevice or SettingsScope.AppUserDevice =>
                SettingsScopeAuthorizationReason.Authorized,
            SettingsScope.AppRoamingUser => hasRoamingSyncCapability
                ? SettingsScopeAuthorizationReason.Authorized
                : SettingsScopeAuthorizationReason.RoamingCapabilityRequired,
            SettingsScope.OsAdmin => AuthorizeOsAdmin(context),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown settings scope.")
        };
    }

    private static SettingsScopeAuthorizationReason AuthorizeOsAdmin(AppOperationContext context)
    {
        if (!context.HasCapability(AppCapabilities.SettingsSystemRead)
            && !context.HasCapability(AppCapabilities.SettingsSystemWrite))
        {
            return SettingsScopeAuthorizationReason.SystemSettingsCapabilityRequired;
        }

        return AppAuthorityPolicy.Satisfies(context.EffectiveAuthority, AppAuthority.Administrator)
            ? SettingsScopeAuthorizationReason.Authorized
            : SettingsScopeAuthorizationReason.AdministratorAuthorityRequired;
    }
}
