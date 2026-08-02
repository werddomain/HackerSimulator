using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.Settings;

namespace HackerOs.Platform.Core.Tests.Settings;

public sealed class SettingsScopePolicyTests
{
    [Fact]
    public void Undeclared_scope_is_rejected_regardless_of_authority_or_capability()
    {
        AppOperationContext context = CreateContext(AppAuthority.User, AppCapabilities.SettingsSystemWrite) with
        {
            IsSystemOperation = true
        };

        SettingsScopeAuthorizationReason reason = SettingsScopePolicy.Authorize(
            SettingsScope.OsAdmin,
            new HashSet<SettingsScope> { SettingsScope.AppUser },
            context,
            hasRoamingSyncCapability: false);

        Assert.Equal(SettingsScopeAuthorizationReason.ScopeNotDeclared, reason);
    }

    [Fact]
    public void AppUser_and_AppDevice_scopes_only_require_manifest_declaration()
    {
        AppOperationContext context = CreateContext(AppAuthority.User);
        HashSet<SettingsScope> declared = [SettingsScope.AppUser, SettingsScope.AppDevice, SettingsScope.AppUserDevice];

        Assert.Equal(
            SettingsScopeAuthorizationReason.Authorized,
            SettingsScopePolicy.Authorize(SettingsScope.AppUser, declared, context, false));
        Assert.Equal(
            SettingsScopeAuthorizationReason.Authorized,
            SettingsScopePolicy.Authorize(SettingsScope.AppDevice, declared, context, false));
        Assert.Equal(
            SettingsScopeAuthorizationReason.Authorized,
            SettingsScopePolicy.Authorize(SettingsScope.AppUserDevice, declared, context, false));
    }

    [Fact]
    public void Roaming_scope_requires_sync_capability_even_when_declared()
    {
        AppOperationContext context = CreateContext(AppAuthority.User);
        HashSet<SettingsScope> declared = [SettingsScope.AppRoamingUser];

        Assert.Equal(
            SettingsScopeAuthorizationReason.RoamingCapabilityRequired,
            SettingsScopePolicy.Authorize(SettingsScope.AppRoamingUser, declared, context, hasRoamingSyncCapability: false));
        Assert.Equal(
            SettingsScopeAuthorizationReason.Authorized,
            SettingsScopePolicy.Authorize(SettingsScope.AppRoamingUser, declared, context, hasRoamingSyncCapability: true));
    }

    [Fact]
    public void OsAdmin_scope_requires_system_settings_capability_and_administrator_authority()
    {
        HashSet<SettingsScope> declared = [SettingsScope.OsAdmin];

        Assert.Equal(
            SettingsScopeAuthorizationReason.SystemSettingsCapabilityRequired,
            SettingsScopePolicy.Authorize(SettingsScope.OsAdmin, declared, CreateContext(AppAuthority.Administrator), false));
        Assert.Equal(
            SettingsScopeAuthorizationReason.AdministratorAuthorityRequired,
            SettingsScopePolicy.Authorize(
                SettingsScope.OsAdmin,
                declared,
                CreateContext(AppAuthority.User, AppCapabilities.SettingsSystemWrite),
                false));
        Assert.Equal(
            SettingsScopeAuthorizationReason.Authorized,
            SettingsScopePolicy.Authorize(
                SettingsScope.OsAdmin,
                declared,
                CreateContext(AppAuthority.Administrator, AppCapabilities.SettingsSystemWrite),
                false));
    }

    [Fact]
    public void System_kind_app_operated_by_user_does_not_gain_system_authority_for_os_admin_scope()
    {
        // A "system app" is only distinguished by trusted platform-issued IsSystemOperation;
        // a user-launched instance keeps User authority even if the app is normally system-owned.
        AppOperationContext userLaunchedSystemApp = CreateContext(AppAuthority.User, AppCapabilities.SettingsSystemWrite) with
        {
            IsSystemOperation = false
        };
        HashSet<SettingsScope> declared = [SettingsScope.OsAdmin];

        SettingsScopeAuthorizationReason reason = SettingsScopePolicy.Authorize(
            SettingsScope.OsAdmin,
            declared,
            userLaunchedSystemApp,
            false);

        Assert.Equal(SettingsScopeAuthorizationReason.AdministratorAuthorityRequired, reason);
    }

    private static AppOperationContext CreateContext(AppAuthority authority, params string[] capabilities) => new()
    {
        AppId = "org.hackeros.test",
        UserId = "user",
        UserAuthority = authority,
        GrantedCapabilities = new HashSet<string>(capabilities, StringComparer.Ordinal)
    };
}
