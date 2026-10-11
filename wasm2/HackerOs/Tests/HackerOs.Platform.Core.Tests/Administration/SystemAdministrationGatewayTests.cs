using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Administration;
using HackerOs.AppSdk.IO;
using HackerOs.AppSdk.Settings;
using HackerOs.Platform.Core.Administration;
using HackerOs.Platform.Core.Diagnostics;
using HackerOs.Platform.Core.Discovery;
using HackerOs.Platform.Core.Elevation;
using HackerOs.Platform.Core.Execution;
using HackerOs.Platform.Core.Policy;
using HackerOs.Platform.Core.Power;
using HackerOs.Platform.Core.Sessions;
using HackerOs.Platform.Core.Tests.Settings;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Simulation.Abstractions.Settings;

namespace HackerOs.Platform.Core.Tests.Administration;

/// <summary>
/// Contract tests of the system-administration gateways (ADR 0056, sections 8 to 11; chantier
/// 4-sdk-system-admin): <c>HackSettings.OpenSystemAsync</c>, <c>HackPermissionAdministration</c>,
/// <c>HackServerConnection</c>, <c>HackSystem</c> and <c>HackStorageDiagnostics</c>.
/// </summary>
public sealed class SystemAdministrationGatewayTests
{
    private const string ScannerId = "org.example.scanner";
    private const string SettingsId = "org.hackeros.settings";
    private const string SystemToolId = "org.hackeros.system-tool";
    private const string AppearanceSystemFile = "/etc/hackeros/appearance.config";
    private const string AppearanceUserFile = "/home/alice/.config/hackeros/appearance.config";
    private const string MachinePermissionsFile = "/etc/hackeros/app-permissions.config";
    private const string SecurityFile = "/etc/hackeros/security.config";

    // ---- HackSettings.OpenSystemAsync (section 8) ----

    [Fact]
    public async Task An_os_document_reads_both_layers_and_writes_the_users_own_layer_without_any_capability()
    {
        SettingsFilesHarness files = new();
        await files.BootAsync();
        AuthenticatedPrincipal alice = await files.LoginAsync(files.Alice);
        await files.WriteAsRootAsync(AppearanceSystemFile, "accent=cyan\n");
        SettingsContext app = new(files, alice);

        await using HackSettingsDocument document = await HackSettings.OpenSystemAsync(app, "appearance");
        Assert.Equal("cyan", document.Get<string>("accent"));
        Assert.Equal(SettingLayer.System, document.GetSource("accent"));

        await document.SetAsync("animationsEnabled", false);

        Assert.Contains("animationsEnabled=false", await files.ReadAsync(AppearanceUserFile));
        Assert.False(document.Get<bool>("animationsEnabled"));
        Assert.DoesNotContain("animationsEnabled", await files.ReadAsync(AppearanceSystemFile));
    }

    [Fact]
    public async Task The_system_layer_is_written_with_the_callers_own_file_rights()
    {
        SettingsFilesHarness files = new();
        await files.BootAsync();
        AuthenticatedPrincipal alice = await files.LoginAsync(files.Alice);

        await using HackSettingsDocument plain = await HackSettings.OpenSystemAsync(new SettingsContext(files, alice), "appearance");
        await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(
            () => plain.SetAsync("accent", "cyan", SettingLayer.System).AsTask());
        Assert.DoesNotContain("accent=cyan", await files.ReadAsync(AppearanceSystemFile) ?? string.Empty);

        // The same app run under sudo (root's file rights) writes /etc in one edit.
        await using HackSettingsDocument root = await HackSettings.OpenSystemAsync(
            new SettingsContext(files, alice, files.CreateRootGateway()), "appearance");
        await root.SetManyAsync(
            new Dictionary<string, object> { ["accent"] = "cyan", ["animationsEnabled"] = false },
            SettingLayer.System);

        string etc = await files.ReadAsync(AppearanceSystemFile) ?? string.Empty;
        Assert.Contains("accent=cyan", etc);
        Assert.Contains("animationsEnabled=false", etc);
    }

    [Fact]
    public async Task An_unknown_os_document_is_refused()
    {
        SettingsFilesHarness files = new();
        await files.BootAsync();
        AuthenticatedPrincipal alice = await files.LoginAsync(files.Alice);

        await Assert.ThrowsAsync<ArgumentException>(
            () => HackSettings.OpenSystemAsync(new SettingsContext(files, alice), "passwords").AsTask());
    }

    // ---- HackPermissionAdministration (section 9) ----

    [Fact]
    public async Task Permission_administration_requires_permissions_manage()
    {
        AdminHarness harness = await AdminHarness.CreateAsync();
        AppPermissionAdministrationGateway gateway = harness.CreatePermissionGateway(new CountingAgent(succeed: true));

        await Assert.ThrowsAsync<AppGatewayAccessDeniedException>(() => gateway.GetOverviewAsync().AsTask());
        await Assert.ThrowsAsync<AppGatewayAccessDeniedException>(
            () => gateway.SetPolicyAsync(HackDangerousCapabilityPolicy.Block).AsTask());
    }

    [Fact]
    public async Task A_refused_administrator_prompt_writes_nothing()
    {
        AdminHarness harness = await AdminHarness.CreateAsync();
        CountingAgent agent = new(succeed: false);
        AppPermissionAdministrationGateway gateway = harness.CreatePermissionGateway(agent, AppCapabilities.PermissionsManage);

        await Assert.ThrowsAsync<HackUnauthorizedAccessException>(() => gateway.SetMachineDecisionAsync(
            ScannerId, AppCapabilities.NetworkRealAccess, HackPermissionDecision.Allow).AsTask());
        await Assert.ThrowsAsync<HackUnauthorizedAccessException>(
            () => gateway.SetPolicyAsync(HackDangerousCapabilityPolicy.Block).AsTask());

        Assert.Equal(2, agent.Calls);
        Assert.Equal("permissions.policy", agent.LastRequest!.Action);
        Assert.DoesNotContain(ScannerId, await harness.Files.ReadAsync(MachinePermissionsFile) ?? string.Empty);
        Assert.Equal(HackDangerousCapabilityPolicy.Prompt, await gateway.GetPolicyAsync());
    }

    [Fact]
    public async Task An_authenticated_administrator_decides_for_the_machine_and_is_audited()
    {
        AdminHarness harness = await AdminHarness.CreateAsync();
        CountingAgent agent = new(succeed: true);
        AppPermissionAdministrationGateway gateway = harness.CreatePermissionGateway(agent, AppCapabilities.PermissionsManage);

        await gateway.SetMachineDecisionAsync(ScannerId, AppCapabilities.NetworkRealAccess, HackPermissionDecision.Deny);

        Assert.Equal("permissions.machine-decision", agent.LastRequest!.Action);
        Assert.Contains($"{ScannerId}:{AppCapabilities.NetworkRealAccess}=deny", await harness.Files.ReadAsync(MachinePermissionsFile));
        HackAppPermissionInfo scanner = Assert.Single(await gateway.GetOverviewAsync(), app => app.AppId == ScannerId);
        Assert.Equal(
            HackPermissionState.DeniedByAdministrator,
            scanner.Capabilities.Single(capability => capability.Capability == AppCapabilities.NetworkRealAccess).State);

        await gateway.SetPolicyAsync(HackDangerousCapabilityPolicy.Block);

        Assert.Equal("permissions.policy", agent.LastRequest!.Action);
        Assert.Contains("block", await harness.Files.ReadAsync(SecurityFile));
        Assert.Equal(HackDangerousCapabilityPolicy.Block, await gateway.GetPolicyAsync());
        AuditEntry revoke = Assert.Single(harness.Audit.Entries, entry => entry.Action == "capability.revoke");
        Assert.Equal("admin", revoke.Properties["admin"]);
        Assert.Single(harness.Audit.Entries, entry => entry.Action == "capability.policy");
    }

    [Fact]
    public async Task A_system_app_or_an_undeclared_capability_is_refused_before_any_prompt()
    {
        AdminHarness harness = await AdminHarness.CreateAsync();
        CountingAgent agent = new(succeed: true);
        AppPermissionAdministrationGateway gateway = harness.CreatePermissionGateway(agent, AppCapabilities.PermissionsManage);

        await Assert.ThrowsAnyAsync<Exception>(() => gateway.SetMachineDecisionAsync(
            SystemToolId, AppCapabilities.ProcessManage, HackPermissionDecision.Deny).AsTask());
        await Assert.ThrowsAnyAsync<Exception>(() => gateway.SetMachineDecisionAsync(
            ScannerId, AppCapabilities.SystemPower, HackPermissionDecision.Allow).AsTask());

        Assert.Equal(0, agent.Calls);
    }

    // ---- HackServerConnection (section 10) ----

    [Fact]
    public async Task Without_server_support_the_connection_is_absent_and_sync_is_a_no_op()
    {
        AppServerConnectionGateway gateway = new(services: null, new FixedCapabilities(AppCapabilities.ServerConnectionManage));

        Assert.Equal(HackServerConnectionState.NotConnected, await gateway.GetStateAsync());
        await gateway.SyncAsync(HackSyncDomain.FileSystem, HackSyncDirection.Push);
        Assert.Empty(await gateway.GetConflictsAsync());
        await Assert.ThrowsAsync<HackServerConnectionException>(
            () => gateway.ConnectAsync(new Uri("https://example.test"), "alice", "pw").AsTask());
    }

    [Fact]
    public async Task Server_connection_requires_its_capability_and_grants_are_pull_only()
    {
        AppServerConnectionGateway denied = new(services: null, new FixedCapabilities());
        AppServerConnectionGateway granted = new(services: null, new FixedCapabilities(AppCapabilities.ServerConnectionManage));

        await Assert.ThrowsAsync<AppGatewayAccessDeniedException>(() => denied.GetStateAsync().AsTask());
        await Assert.ThrowsAsync<ArgumentException>(
            () => granted.SyncAsync(HackSyncDomain.Grants, HackSyncDirection.Push).AsTask());
    }

    // ---- HackSystem and HackStorageDiagnostics (section 11) ----

    [Fact]
    public async Task Reboot_requires_system_power_and_an_administrator()
    {
        AdminHarness harness = await AdminHarness.CreateAsync();
        RecordingDevice device = new();
        CountingAgent refusing = new(succeed: false);

        await Assert.ThrowsAsync<AppGatewayAccessDeniedException>(
            () => harness.CreatePowerGateway(device, new CountingAgent(succeed: true)).RebootAsync().AsTask());
        await Assert.ThrowsAsync<HackUnauthorizedAccessException>(
            () => harness.CreatePowerGateway(device, refusing, AppCapabilities.SystemPower).RebootAsync().AsTask());

        Assert.Equal("system.reboot", refusing.LastRequest!.Action);
        Assert.Empty(device.Calls);
        Assert.Equal(SessionState.Active, harness.Files.Session.State);
    }

    [Fact]
    public async Task Factory_reset_closes_the_session_then_erases_then_restarts()
    {
        AdminHarness harness = await AdminHarness.CreateAsync();
        RecordingDevice device = new();
        CountingAgent agent = new(succeed: true);

        await harness.CreatePowerGateway(device, agent, AppCapabilities.SystemPower).FactoryResetAsync();

        Assert.Equal("system.factory-reset", agent.LastRequest!.Action);
        Assert.Equal(["erase", "restart"], device.Calls);
        Assert.NotEqual(SessionState.Active, harness.Files.Session.State);
        AuditEntry entry = Assert.Single(harness.Audit.Entries, audit => audit.Action == "system.factory-reset");
        Assert.Equal("admin", entry.Properties["admin"]);
    }

    [Fact]
    public async Task Storage_inspection_requires_its_capability()
    {
        HackStorageSnapshot snapshot = new(DateTimeOffset.UnixEpoch, "hackeros", 3, [], new Dictionary<string, string>(), 0);
        AppStorageDiagnosticsGateway denied = new(() => new FixedInspector(snapshot), new FixedCapabilities());
        AppStorageDiagnosticsGateway granted = new(() => new FixedInspector(snapshot), new FixedCapabilities(AppCapabilities.SystemStorageInspect));
        AppStorageDiagnosticsGateway absent = new(null, new FixedCapabilities(AppCapabilities.SystemStorageInspect));

        await Assert.ThrowsAsync<AppGatewayAccessDeniedException>(() => denied.GetSnapshotAsync().AsTask());
        Assert.Same(snapshot, await granted.GetSnapshotAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => absent.GetSnapshotAsync().AsTask());
    }

    private static AppManifest Manifest(string id, params string[] capabilities) => new()
    {
        Id = id,
        Name = id,
        Version = "1.0.0",
        PublisherId = id.StartsWith("org.hackeros", StringComparison.Ordinal) ? "pub.hackeros" : "org.example",
        Description = "System-administration fixture.",
        Kind = AppKind.Window,
        EntryPoint = new AppEntryPointManifest("HackerOs.Apps.Sample", "HackerOs.Apps.Sample.SampleApp"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        Capabilities = capabilities
    };

    /// <summary>Settings (system app) calling the gateways on behalf of alice, with an admin account available.</summary>
    private sealed class AdminHarness
    {
        private AdminHarness(SettingsFilesHarness files, AuthenticatedPrincipal principal)
        {
            Files = files;
            Principal = principal;
            AppCatalogBuildResult built = AppCatalog.Build(
                [
                    Manifest(ScannerId, AppCapabilities.NetworkRealAccess, AppCapabilities.ProcessManage),
                    Manifest(SystemToolId, AppCapabilities.ProcessManage),
                    Manifest(SettingsId, AppCapabilities.PermissionsManage, AppCapabilities.SystemPower)
                ],
                [SystemToolId, SettingsId]);
            Assert.True(built.IsSuccess, string.Join(", ", built.Errors.Select(error => error.Message)));
            Catalog = built.Catalog!;
            Audit = new BoundedAuditLog(maxEntries: 200);
            Administration = new AppPermissionAdministration(
                Catalog, new AppPermissionStore(files.Settings), files.Settings, new AdminElevationVerifier(files.Users, Audit), Audit);
        }

        internal SettingsFilesHarness Files { get; }
        internal AuthenticatedPrincipal Principal { get; }
        internal AppCatalog Catalog { get; }
        internal BoundedAuditLog Audit { get; }
        internal AppPermissionAdministration Administration { get; }

        internal static async Task<AdminHarness> CreateAsync()
        {
            SettingsFilesHarness files = new();
            await files.BootAsync();
            AuthenticatedPrincipal alice = await files.LoginAsync(files.Alice);
            return new AdminHarness(files, alice);
        }

        internal AppPermissionAdministrationGateway CreatePermissionGateway(
            IAdministratorAuthenticationAgent agent, params string[] capabilities) =>
            new(() => Administration, new FixedCapabilities(capabilities), Gate(agent));

        internal AppSystemPowerGateway CreatePowerGateway(
            ISystemPowerDevice device, IAdministratorAuthenticationAgent agent, params string[] capabilities) =>
            new(() => new SystemPowerService(device, Files.Session, auditLog: Audit), new FixedCapabilities(capabilities), Gate(agent));

        private AdministratorGate Gate(IAdministratorAuthenticationAgent agent) =>
            new(() => agent, Catalog.Manifests[SettingsId], Principal, isRootElevated: false);
    }

    /// <summary>Records requests and answers with a fixed outcome.</summary>
    private sealed class CountingAgent(bool succeed) : IAdministratorAuthenticationAgent
    {
        public int Calls { get; private set; }

        public AdministratorAuthenticationRequest? LastRequest { get; private set; }

        public Task<AdministratorAuthenticationResult> AuthenticateAsync(
            AdministratorAuthenticationRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(succeed
                ? new AdministratorAuthenticationResult(true, "admin", null)
                : AdministratorAuthenticationResult.Failed("cancelled"));
        }
    }

    /// <summary>Stands in for the browser: records erase and restart in order.</summary>
    private sealed class RecordingDevice : ISystemPowerDevice
    {
        public List<string> Calls { get; } = [];

        public Task RestartAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("restart");
            return Task.CompletedTask;
        }

        public Task EraseStorageAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("erase");
            return Task.CompletedTask;
        }
    }

    private sealed class FixedInspector(HackStorageSnapshot snapshot) : IStorageInspector
    {
        public Task<HackStorageSnapshot> CaptureAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    }

    /// <summary>Grants exactly the listed capabilities (the gateways only call <see cref="Require"/>).</summary>
    private sealed class FixedCapabilities(params string[] granted) : ICapabilityChecker
    {
        public CapabilityPolicyEvaluation Evaluate(
            string capability, AppAuthority requiredAuthority = AppAuthority.User, CapabilityResourceCandidate? resourceCandidate = null) =>
            throw new NotSupportedException();

        public void Require(
            string capability, AppAuthority requiredAuthority = AppAuthority.User, CapabilityResourceCandidate? resourceCandidate = null)
        {
            if (!granted.Contains(capability, StringComparer.Ordinal))
            {
                throw new AppGatewayAccessDeniedException(capability, CapabilityPolicyEvaluation.DenyMissing(1));
            }
        }
    }

    /// <summary>
    /// Minimal context of an app run by alice whose <see cref="SystemSettings"/> is the real gateway;
    /// <paramref name="writer"/> replaces the app's own filesystem rights (root for a sudo run).
    /// </summary>
    private sealed class SettingsContext : IAppExecutionContext
    {
        internal SettingsContext(SettingsFilesHarness files, AuthenticatedPrincipal principal, IAppFileSystemGateway? writer = null)
        {
            Manifest = Manifest(SettingsId);
            UserId = principal.UserId.ToString();
            HomeDirectory = principal.HomePath;
            FileSystem = writer ?? files.CreateAppGateway(principal, SettingsId);
            SystemSettings = new AppSystemSettingsGateway(() => files.Settings, FileSystem);
        }

        public AppManifest Manifest { get; }
        public Guid InstanceId { get; } = Guid.NewGuid();
        public string UserId { get; }
        public string HomeDirectory { get; }
        public AppAuthority UserAuthority => AppAuthority.User;
        public IReadOnlySet<string> GrantedCapabilities { get; } = new HashSet<string>();
        public SessionId SessionId { get; } = SessionId.FromGuid(Guid.NewGuid());
        public ProcessId ProcessId { get; } = ProcessId.FromInt64(1);
        public CancellationToken CancellationToken => CancellationToken.None;
        public IAppFileSystemGateway FileSystem { get; }
        public IAppSystemSettingsGateway SystemSettings { get; }
        public ICapabilityChecker Capabilities => throw new NotSupportedException();
        public IAppEventGateway Events => throw new NotSupportedException();
        public IAppNotificationGateway Notifications => throw new NotSupportedException();
        public IAppClockGateway Clock => throw new NotSupportedException();
        public IAppProcessGateway Processes => throw new NotSupportedException();
        public IAppLoggingGateway Logging => throw new NotSupportedException();
    }
}
