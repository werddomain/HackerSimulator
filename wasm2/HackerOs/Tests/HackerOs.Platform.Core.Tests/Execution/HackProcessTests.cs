using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Apps;
using HackerOs.AppSdk.Diagnostics;
using HackerOs.AppSdk.IO;
using HackerOs.AppSdk.ServiceProcess;
using HackerOs.Platform.Core.Execution;
using HackerOs.Platform.Core.Tests.FileSystem.Virtual;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;
using static HackerOs.Platform.Core.Tests.FileSystem.Virtual.VirtualFileSystemFixture;

namespace HackerOs.Platform.Core.Tests.Execution;

/// <summary>
/// ADR 0056 sections 4 and 5: <see cref="HackProcess"/> and <see cref="HackServiceController"/> read
/// the real <c>/proc</c> provider through the app's own file gateway, exactly like <c>ps</c>.
/// </summary>
public sealed class HackProcessTests
{
    private const long OwnPid = 2;
    private static readonly LocalUserId Bob = LocalUserId.FromGuid(new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"));

    [Fact]
    public async Task GetProcessesAsync_reads_status_cmdline_cwd_io_and_the_start_time_from_proc()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        ProcessRecord editor = Process(42, "org.hackeros.text-editor", parentPid: OwnPid);
        fixture.Processes.Active.AddRange([Process(OwnPid, "org.hackeros.system-monitor"), editor]);
        fixture.Resources.ProcessSamples[editor.Pid] = [new ProcessResourceSample(editor.Pid, 1, BootUtc, 0.25, 3 * 1024 * 1024, 0, 0)];
        fixture.Environments.Create(
            new ProcessStartRequest(
                OwnProcessId, "org.hackeros.text-editor", AppInstanceId.FromGuid(Guid.NewGuid()), AppKind.Window,
                Alice, Session, ResourceProfile.None,
                ["text-editor", "/home/alice/my notes.md"],
                VirtualPath.Parse("/home/alice/Documents"),
                new Dictionary<string, string>(StringComparer.Ordinal) { ["HOME"] = "/home/alice" }),
            editor.Pid);
        fixture.ProcessIo.RecordRead(editor.Pid, 4096, fromStorage: true);
        fixture.ProcessIo.RecordWrite(editor.Pid, 100, toStorage: false);
        using IDisposable scope = Enter(fixture, AppCapabilities.ProcessList);

        HackProcess[] processes = await HackProcess.GetProcessesAsync();

        Assert.Equal([OwnPid, 42], processes.Select(static process => process.Id));
        HackProcess process = processes[1];
        Assert.Equal("text-editor", process.ProcessName);
        Assert.Equal("org.hackeros.text-editor", process.AppId);
        Assert.Equal(OwnPid, process.ParentId);
        Assert.Equal(AppKind.Window, process.Kind);
        Assert.Equal(HackProcessState.Running, process.State);
        Assert.Equal('R', process.StateCode);
        Assert.Equal("alice", process.UserName);
        Assert.Equal(Alice.ToString(), process.UserId);
        Assert.Equal(Session.ToString(), process.SessionId);
        Assert.Equal(BootUtc.AddSeconds(42), process.StartTime);
        Assert.Equal(3L * 1024 * 1024, process.WorkingSet64);
        Assert.Equal(25.0, process.CpuUsage, 3);
        Assert.Equal(["text-editor", "/home/alice/my notes.md"], process.Arguments);
        Assert.Equal("text-editor \"/home/alice/my notes.md\"", process.CommandLine);
        Assert.Equal("/home/alice/Documents", process.WorkingDirectory);
        Assert.Equal(new HackProcessIoCounters(4096, 100, 1, 1, 4096, 0, 0), process.IoCounters);
        Assert.Equal(4096, process.ReadBytes);
        Assert.Equal("text-editor", process.Status["Name"]);
        Assert.False(process.HasExited);
        Assert.Equal(["HOME"], (await process.GetEnvironmentVariablesAsync())!.Keys);
        Assert.Equal([process], processes[0].GetChildren(processes));
    }

    [Fact]
    public async Task Without_process_list_only_the_own_process_is_visible_like_ps()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Processes.Active.AddRange([Process(OwnPid, "org.hackeros.system-monitor"), Process(42, "org.hackeros.text-editor")]);
        using IDisposable scope = Enter(fixture);

        HackProcess own = Assert.Single(await HackProcess.GetProcessesAsync());

        Assert.Equal(OwnPid, own.Id);
        Assert.Equal(OwnPid, (await HackProcess.GetCurrentProcessAsync()).Id);
        ArgumentException missing = await Assert.ThrowsAsync<ArgumentException>(() => HackProcess.GetProcessByIdAsync(42));
        Assert.Contains("42", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Another_users_io_and_environment_are_private_and_read_as_null()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Processes.Active.AddRange([Process(OwnPid, "org.hackeros.system-monitor"), Process(77, "org.hackeros.sync", userId: Bob)]);
        using IDisposable scope = Enter(fixture, AppCapabilities.ProcessList);

        HackProcess other = await HackProcess.GetProcessByIdAsync(77);

        Assert.Null(other.IoCounters);
        Assert.Equal(0, other.ReadBytes);
        Assert.Null(await other.GetEnvironmentVariablesAsync());
        // The OS can only name the caller's own user; another uid reads as itself, like ps without /etc/passwd.
        Assert.Equal(Bob.ToString(), other.UserName);
    }

    [Fact]
    public async Task RefreshAsync_marks_an_exited_process()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        ProcessRecord editor = Process(42, "org.hackeros.text-editor");
        fixture.Processes.Active.AddRange([Process(OwnPid, "org.hackeros.system-monitor"), editor]);
        using IDisposable scope = Enter(fixture, AppCapabilities.ProcessList);
        HackProcess process = await HackProcess.GetProcessByIdAsync(42);

        fixture.Processes.Active.Remove(editor);
        await process.RefreshAsync();

        Assert.True(process.HasExited);
        Assert.Equal("text-editor", process.ProcessName);
    }

    [Fact]
    public async Task KillAsync_goes_through_the_process_gateway_and_maps_a_denial_and_an_exit()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Processes.Active.AddRange(
            [Process(OwnPid, "org.hackeros.system-monitor"), Process(42, "org.hackeros.text-editor"), Process(43, "org.hackeros.calculator")]);
        RecordingProcessGateway gateway = new() { Denied = { 43 } };
        using IDisposable scope = Enter(fixture, gateway, AppCapabilities.ProcessList);
        HackProcess[] processes = await HackProcess.GetProcessesAsync();

        await processes[1].KillAsync();
        HackUnauthorizedAccessException denied = await Assert.ThrowsAsync<HackUnauthorizedAccessException>(() => processes[2].KillAsync());
        gateway.Gone.Add(42);
        await Assert.ThrowsAsync<InvalidOperationException>(() => processes[1].KillAsync());

        Assert.Equal([(42L, true)], gateway.Calls);
        Assert.True(processes[1].HasExited);
        Assert.Equal(AppCapabilities.ProcessManage, denied.RequiredCapability);
    }

    [Fact]
    public async Task CloseMainWindowAsync_asks_politely_and_reports_whether_the_process_had_to_be_forced()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Processes.Active.AddRange([Process(OwnPid, "org.hackeros.system-monitor"), Process(42, "org.hackeros.text-editor")]);
        RecordingProcessGateway gateway = new();
        using IDisposable scope = Enter(fixture, gateway, AppCapabilities.ProcessList);
        HackProcess editor = await HackProcess.GetProcessByIdAsync(42);

        Assert.True(await editor.CloseMainWindowAsync(TimeSpan.FromSeconds(2)));
        gateway.StopReason = ProcessExitReason.Timeout;
        Assert.False(await editor.CloseMainWindowAsync());

        Assert.Equal([(42L, false), (42L, false)], gateway.Calls);
        Assert.Equal([TimeSpan.FromSeconds(2), HackProcess.DefaultCloseTimeout], gateway.Timeouts);
        Assert.Equal([ProcessExitReason.CloseRequested, ProcessExitReason.CloseRequested], gateway.Reasons);
    }

    [Fact]
    public async Task TerminateAsync_sends_sigterm_with_a_five_second_grace_and_reports_a_forced_end()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Processes.Active.AddRange(
            [Process(OwnPid, "org.hackeros.system-monitor"), Process(42, "org.hackeros.text-editor"), Process(43, "org.hackeros.calculator")]);
        RecordingProcessGateway gateway = new() { Denied = { 43 } };
        using IDisposable scope = Enter(fixture, gateway, AppCapabilities.ProcessList);
        HackProcess editor = await HackProcess.GetProcessByIdAsync(42);
        HackProcess calculator = await HackProcess.GetProcessByIdAsync(43);

        gateway.StopReason = ProcessExitReason.Completed;
        Assert.True(await editor.TerminateAsync());
        gateway.StopReason = ProcessExitReason.Timeout;
        Assert.False(await editor.TerminateAsync(TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<HackUnauthorizedAccessException>(() => calculator.TerminateAsync());

        Assert.Equal([(42L, false), (42L, false)], gateway.Calls);
        Assert.Equal([HackProcess.DefaultTerminateTimeout, TimeSpan.FromSeconds(1)], gateway.Timeouts);
        Assert.Equal([ProcessExitReason.Terminated, ProcessExitReason.Terminated], gateway.Reasons);
        Assert.Equal(TimeSpan.FromSeconds(5), HackProcess.DefaultTerminateTimeout);
        Assert.True(editor.HasExited);
    }

    [Fact]
    public async Task Services_come_from_the_installed_apps_with_their_status_from_proc()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Processes.Active.AddRange(
            [Process(OwnPid, "org.hackeros.system-monitor"), Process(60, "org.hackeros.sync", kind: AppKind.Service)]);
        FakeServices services = new() { Modes = { ["org.hackeros.sync"] = ServiceStartMode.Automatic }, DeniedReads = { "org.hackeros.indexer" } };
        FakeCatalog catalog = new(
            App("org.hackeros.sync", "Sync", AppKind.Service),
            App("org.hackeros.indexer", "Indexer", AppKind.Service),
            App("org.hackeros.text-editor", "Text Editor", AppKind.Window));
        using IDisposable scope = Enter(fixture, new RecordingProcessGateway(), catalog, services, AppCapabilities.ProcessList);

        HackServiceController[] list = await HackServiceController.GetServicesAsync();

        Assert.Equal(["org.hackeros.indexer", "org.hackeros.sync"], list.Select(static service => service.ServiceName));
        Assert.Equal(HackServiceControllerStatus.Stopped, list[0].Status);
        Assert.Null(list[0].StartType);
        Assert.Equal(HackServiceControllerStatus.Running, list[1].Status);
        Assert.Equal(60, list[1].ProcessId);
        Assert.Equal(ServiceStartMode.Automatic, list[1].StartType);

        await list[1].SetStartTypeAsync(ServiceStartMode.Disabled);
        Assert.Equal(ServiceStartMode.Disabled, list[1].StartType);

        services.NextOutcome = ServiceControlOutcome.ServiceDisabled;
        InvalidOperationException disabled = await Assert.ThrowsAsync<InvalidOperationException>(() => list[0].StartAsync());
        Assert.Contains("disabled", disabled.Message, StringComparison.Ordinal);

        services.DenyActions = true;
        await Assert.ThrowsAsync<HackUnauthorizedAccessException>(() => list[1].StopAsync());
        Assert.Equal(["set:org.hackeros.sync:Disabled", "start:org.hackeros.indexer", "stop:org.hackeros.sync"], services.Calls);
    }

    private static readonly ProcessId OwnProcessId = ProcessId.FromInt64(OwnPid);

    private static IDisposable Enter(VirtualFileSystemFixture fixture, params string[] capabilities) =>
        Enter(fixture, new RecordingProcessGateway(), capabilities);

    private static IDisposable Enter(VirtualFileSystemFixture fixture, IAppProcessGateway processes, params string[] capabilities) =>
        Enter(fixture, processes, new FakeCatalog(), new FakeServices(), capabilities);

    private static IDisposable Enter(
        VirtualFileSystemFixture fixture,
        IAppProcessGateway processes,
        IAppCatalogGateway catalog,
        IAppServiceControlGateway services,
        params string[] capabilities)
    {
        string[] granted = [AppCapabilities.FileSystemSystemRead, .. capabilities];
        AppOperationContext operation = new()
        {
            AppId = "org.hackeros.system-monitor",
            UserId = Alice.ToString(),
            UserName = "alice",
            UserAuthority = AppAuthority.User,
            GrantedCapabilities = new HashSet<string>(granted, StringComparer.Ordinal),
            ProcessId = OwnPid
        };
        AppFileSystemGateway fileSystem = new(fixture.Service, fixture.Clock, operation, ["users"]);
        return HackExecutionContext.Enter(new TaskManagerContext(fileSystem, processes, catalog, services, granted));
    }

    private static HackAppInfo App(string id, string name, AppKind kind) =>
        new(id, name, $"{name} test app", kind, "system", null, "1.0.0", "org.tests", true, false, []);

    /// <summary>The gateways a task manager uses: files (real), processes, catalog and services (fakes).</summary>
    private sealed class TaskManagerContext(
        IAppFileSystemGateway fileSystem,
        IAppProcessGateway processes,
        IAppCatalogGateway catalog,
        IAppServiceControlGateway services,
        IReadOnlyCollection<string> granted) : IAppExecutionContext
    {
        public AppManifest Manifest { get; } = new()
        {
            Id = "org.hackeros.system-monitor",
            Name = "System Monitor",
            Version = "1.0.0",
            PublisherId = "org.tests",
            Description = "HackProcess test application.",
            Kind = AppKind.Window,
            EntryPoint = new AppEntryPointManifest("HackerOs.Platform.Core.Tests", "TaskManagerApp"),
            SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
            Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
            Resources = AppResourceProfileManifest.None
        };

        public Guid InstanceId { get; } = Guid.NewGuid();
        public string UserId => Alice.ToString();
        public string UserLoginName => "alice";
        public string HomeDirectory => "/home/alice";
        public AppAuthority UserAuthority => AppAuthority.User;
        public IReadOnlySet<string> GrantedCapabilities { get; } = new HashSet<string>(granted, StringComparer.Ordinal);
        public SessionId SessionId => Session;
        public ProcessId ProcessId => OwnProcessId;
        public CancellationToken CancellationToken => CancellationToken.None;
        public ICapabilityChecker Capabilities => throw new NotSupportedException();
        public IAppFileSystemGateway FileSystem { get; } = fileSystem;
        public IAppEventGateway Events => throw new NotSupportedException();
        public IAppNotificationGateway Notifications => throw new NotSupportedException();
        public IAppLoggingGateway Logging => throw new NotSupportedException();
        public IAppClockGateway Clock => throw new NotSupportedException();
        public IAppProcessGateway Processes { get; } = processes;
        public IAppCatalogGateway Catalog { get; } = catalog;
        public IAppServiceControlGateway Services { get; } = services;
    }

    /// <summary>Records stop and kill calls; denies or forgets the PIDs a test lists.</summary>
    private sealed class RecordingProcessGateway : IAppProcessGateway
    {
        public HashSet<long> Denied { get; } = [];
        public HashSet<long> Gone { get; } = [];
        public List<(long Pid, bool Forced)> Calls { get; } = [];
        public List<TimeSpan> Timeouts { get; } = [];
        public List<ProcessExitReason> Reasons { get; } = [];
        public ProcessExitReason StopReason { get; set; } = ProcessExitReason.CloseRequested;

        public ProcessRecord OwnProcess => throw new NotSupportedException();

        public ProcessRecord StartChild(string appId, AppInstanceId appInstanceId, AppKind kind, ResourceProfile resourceProfile) =>
            throw new NotSupportedException();

        public IReadOnlyList<ProcessRecord> ListProcesses() => throw new NotSupportedException();

        public async Task<ProcessRecord> StopAsync(
            ProcessId pid, TimeSpan timeout, ProcessExitReason reason = ProcessExitReason.CloseRequested, CancellationToken cancellationToken = default)
        {
            // Like AppProcessGateway, the capability check happens inside the task.
            await Task.Yield();
            Check(pid);
            Calls.Add((pid.Value, false));
            Timeouts.Add(timeout);
            Reasons.Add(reason);
            ProcessRecord stopped = Process(pid.Value, "org.tests.stopped", ProcessState.Stopped);
            return new ProcessRecord(
                stopped.Pid, stopped.ParentPid, stopped.AppId, stopped.AppInstanceId, stopped.Kind, stopped.UserId, stopped.SessionId,
                ProcessState.Stopped, stopped.ResourceProfile, null, stopped.CreatedAtUtc, stopped.StartedAtUtc, stopped.StoppedAtUtc,
                0, StopReason);
        }

        public ProcessRecord Kill(ProcessId pid, ProcessExitReason reason = ProcessExitReason.Killed)
        {
            Check(pid);
            Calls.Add((pid.Value, true));
            return Process(pid.Value, "org.tests.killed", ProcessState.Stopped);
        }

        private void Check(ProcessId pid)
        {
            if (Denied.Contains(pid.Value))
            {
                throw new AppGatewayAccessDeniedException(AppCapabilities.ProcessManage, CapabilityPolicyEvaluation.DenyMissing(1));
            }

            if (Gone.Contains(pid.Value))
            {
                throw new InvalidOperationException($"Process {pid.Value} is not active.");
            }
        }
    }

    private sealed class FakeCatalog(params HackAppInfo[] apps) : IAppCatalogGateway
    {
        public event EventHandler? Changed { add { } remove { } }

        public ValueTask<IReadOnlyList<HackAppInfo>> GetInstalledAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<HackAppInfo>>(apps);

        public ValueTask<IReadOnlyList<HackAppInfo>> GetHandlersAsync(
            VirtualPath path, string? action = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeServices : IAppServiceControlGateway
    {
        public Dictionary<string, ServiceStartMode> Modes { get; } = new(StringComparer.Ordinal);
        public HashSet<string> DeniedReads { get; } = new(StringComparer.Ordinal);
        public List<string> Calls { get; } = [];
        public ServiceControlOutcome NextOutcome { get; set; } = ServiceControlOutcome.Succeeded;
        public bool DenyActions { get; set; }

        public ValueTask<ServiceControlResult> StartAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
            Act($"start:{serviceAppId}");

        public ValueTask<ServiceControlResult> StopAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
            Act($"stop:{serviceAppId}");

        public ValueTask<ServiceStartMode> GetStartModeAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
            DeniedReads.Contains(serviceAppId)
                ? throw new AppGatewayAccessDeniedException(AppCapabilities.ServicesManage, CapabilityPolicyEvaluation.DenyMissing(1))
                : ValueTask.FromResult(Modes.GetValueOrDefault(serviceAppId, ServiceStartMode.Manual));

        public ValueTask<ServiceControlResult> SetStartModeAsync(
            string serviceAppId, ServiceStartMode mode, CancellationToken cancellationToken = default)
        {
            Modes[serviceAppId] = mode;
            return Act($"set:{serviceAppId}:{mode}");
        }

        private ValueTask<ServiceControlResult> Act(string call)
        {
            Calls.Add(call);
            if (DenyActions)
            {
                throw new AppGatewayAccessDeniedException(AppCapabilities.ServicesManage, CapabilityPolicyEvaluation.DenyMissing(1));
            }

            ServiceControlOutcome outcome = NextOutcome;
            NextOutcome = ServiceControlOutcome.Succeeded;
            return ValueTask.FromResult(new ServiceControlResult(outcome));
        }
    }
}
