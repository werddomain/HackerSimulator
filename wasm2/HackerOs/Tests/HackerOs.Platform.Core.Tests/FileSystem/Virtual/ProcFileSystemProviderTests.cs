using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Processes;
using static HackerOs.Platform.Core.Tests.FileSystem.Virtual.VirtualFileSystemFixture;

namespace HackerOs.Platform.Core.Tests.FileSystem.Virtual;

/// <summary>Contract tests for the virtual <c>/proc</c> mount (ADR 0046, section 6).</summary>
public sealed class ProcFileSystemProviderTests
{
    private const string Read = AppCapabilities.FileSystemSystemRead;
    private const string Write = AppCapabilities.FileSystemSystemWrite;
    private const string ProcessList = AppCapabilities.ProcessList;

    [Fact]
    public async Task Root_listing_shows_proc_and_dev_mount_points()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();

        FileSystemResult<FileSystemDirectorySnapshot> root = await fixture.Service.EnumerateAsync(
            new FileSystemEnumerateRequest(VirtualPath.Parse("/")), fixture.SystemContext());

        string[] names = [.. root.Value!.Entries.Select(static entry => entry.Name.Value)];
        Assert.Contains("proc", names);
        Assert.Contains("dev", names);

        // The mount points are real, empty directories in the root provider, like on Linux.
        FileSystemResult<FileSystemEntrySnapshot> procMountPoint = await fixture.Root.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/proc")), fixture.SystemContext());
        FileSystemResult<FileSystemEntrySnapshot> devMountPoint = await fixture.Root.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/dev")), fixture.SystemContext());
        Assert.Equal(0x16D, procMountPoint.Value!.Metadata.Permissions.Mode); // 0555
        Assert.Equal(0x1ED, devMountPoint.Value!.Metadata.Permissions.Mode); // 0755
    }

    [Fact]
    public async Task Proc_listing_holds_visible_pids_and_system_files()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Processes.Active.Add(Process(42, "org.hackeros.apps.terminal"));

        FileSystemResult<FileSystemDirectorySnapshot> listing = await fixture.Service.EnumerateAsync(
            new FileSystemEnumerateRequest(VirtualPath.Parse("/proc")), fixture.AppContext(42, Read));

        Assert.Equal(
            ["42", "cpuinfo", "loadavg", "meminfo", "self", "uptime"],
            listing.Value!.Entries.Select(static entry => entry.Name.Value));
        Assert.Equal(fixture.Clock.CurrentTick + 1, listing.Value.Revision);
    }

    [Fact]
    public async Task Status_has_the_exact_documented_format()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        ProcessRecord process = Process(42, "org.hackeros.apps.code-editor-deluxe", parentPid: 7, kind: AppKind.Window);
        fixture.Processes.Active.Add(process);
        fixture.Resources.ProcessSamples[process.Pid] =
        [
            new ProcessResourceSample(process.Pid, 1, BootUtc, 0.9, 1024, 0, 0),
            new ProcessResourceSample(process.Pid, 2, BootUtc, 0.125, 3 * 1024 * 1024, 0, 0)
        ];

        string status = await fixture.ReadTextAsync("/proc/42/status", fixture.AppContext(42, Read));

        Assert.Equal(
            "Name:\tcode-editor-del\n" +
            "State:\tR (running)\n" +
            "Pid:\t42\n" +
            "PPid:\t7\n" +
            $"Uid:\t{Alice}\n" +
            "AppId:\torg.hackeros.apps.code-editor-deluxe\n" +
            "Kind:\twindow\n" +
            $"Session:\t{Session}\n" +
            "VmRSS:\t3072 kB\n" +
            "Cpu:\t12.5\n",
            status);
    }

    [Theory]
    [InlineData(ProcessState.Created, "I (idle)")]
    [InlineData(ProcessState.Starting, "S (sleeping)")]
    [InlineData(ProcessState.Running, "R (running)")]
    [InlineData(ProcessState.Stopping, "T (stopping)")]
    public async Task Status_maps_process_states_to_linux_letters(ProcessState state, string expected)
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Processes.Active.Add(Process(5, "org.tests.app", state));

        string status = await fixture.ReadTextAsync("/proc/5/status", fixture.SystemContext());

        Assert.Contains($"State:\t{expected}\n", status);
        Assert.Contains("PPid:\t0\n", status);
    }

    [Fact]
    public async Task Cmdline_environ_and_cwd_follow_the_phase_1_shapes()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Processes.Active.Add(Process(42, "org.hackeros.commands.ps"));
        FileSystemAuthorizationContext alice = fixture.AppContext(42, Read);

        Assert.Equal("org.hackeros.commands.ps\0", await fixture.ReadTextAsync("/proc/42/cmdline", alice));
        Assert.Equal(string.Empty, await fixture.ReadTextAsync("/proc/42/environ", alice));

        FileSystemResult<FileSystemEntrySnapshot> cwd = await fixture.Service.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/proc/42/cwd"), FileSystemLinkBehavior.NoFollow), alice);
        SymbolicLinkMetadata link = Assert.IsType<SymbolicLinkMetadata>(cwd.Value!.Metadata);
        Assert.Equal("/home/alice", link.Target);

        FileSystemResult<FileSystemEntrySnapshot> environ = await fixture.Service.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/proc/42/environ")), alice);
        Assert.Equal(0x100, environ.Value!.Metadata.Permissions.Mode); // 0400
        Assert.Equal(Alice.ToString(), environ.Value.Metadata.OwnerId);
    }

    [Fact]
    public async Task Meminfo_reports_hardware_capacity_minus_the_latest_system_sample()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Resources.SystemSamples.Add(new SystemResourceSample(1, BootUtc, 0, 512L * 1024 * 1024, 0, 0));
        fixture.Resources.SystemSamples.Add(new SystemResourceSample(2, BootUtc, 0, 1024L * 1024 * 1024, 0, 0));

        string meminfo = await fixture.ReadTextAsync("/proc/meminfo", fixture.AppContext(null, Read));

        Assert.Equal(
            "MemTotal:        4194304 kB\n" +
            "MemFree:         3145728 kB\n" +
            "MemAvailable:    3145728 kB\n",
            meminfo);
    }

    [Fact]
    public async Task Cpuinfo_has_one_block_per_rounded_up_virtual_core()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();

        string cpuinfo = await fixture.ReadTextAsync("/proc/cpuinfo", fixture.AppContext(null, Read));

        Assert.Equal(2, cpuinfo.Split("processor\t:").Length - 1);
        Assert.Contains("processor\t: 1\n", cpuinfo);
        Assert.Contains("model name\t: HackerOS Virtual CPU (test-rig)\n", cpuinfo);
    }

    [Fact]
    public async Task Uptime_and_loadavg_use_the_simulation_clock_and_system_history()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Clock.Advance(600); // 600 ticks × 100 ms = 60 s of uptime.
        DateTimeOffset now = fixture.Clock.UtcNow;
        fixture.Resources.SystemSamples.Add(new SystemResourceSample(1, now.AddMinutes(-12), 1.0, 0, 0, 0));
        fixture.Resources.SystemSamples.Add(new SystemResourceSample(2, now.AddMinutes(-4), 1.5, 0, 0, 0));
        fixture.Resources.SystemSamples.Add(new SystemResourceSample(3, now.AddSeconds(-30), 0.5, 0, 0, 0));
        fixture.Processes.Active.Add(Process(42, "org.tests.running"));
        fixture.Processes.Active.Add(Process(43, "org.tests.starting", ProcessState.Starting));
        fixture.Processes.History.Add(Process(50, "org.tests.done", ProcessState.Stopped));
        FileSystemAuthorizationContext context = fixture.AppContext(null, Read);

        string uptime = await fixture.ReadTextAsync("/proc/uptime", context);
        string loadavg = await fixture.ReadTextAsync("/proc/loadavg", context);

        // Idle = uptime × (2 cores − mean usage 1.0).
        Assert.Equal("60.00 60.00\n", uptime);
        Assert.Equal("0.50 1.00 1.00 1/2 50\n", loadavg);
    }

    [Fact]
    public async Task Other_pids_need_process_list_or_system_authority()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Processes.Active.Add(Process(42, "org.tests.own"));
        fixture.Processes.Active.Add(Process(43, "org.tests.other"));

        FileSystemResult<FileSystemDirectorySnapshot> restricted = await fixture.Service.EnumerateAsync(
            new FileSystemEnumerateRequest(VirtualPath.Parse("/proc")), fixture.AppContext(42, Read));
        FileSystemResult<FileSystemEntrySnapshot> hidden = await fixture.Service.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/proc/43/status")), fixture.AppContext(42, Read));
        FileSystemResult<FileSystemDirectorySnapshot> listed = await fixture.Service.EnumerateAsync(
            new FileSystemEnumerateRequest(VirtualPath.Parse("/proc")), fixture.AppContext(42, Read, ProcessList));
        FileSystemResult<FileSystemDirectorySnapshot> system = await fixture.Service.EnumerateAsync(
            new FileSystemEnumerateRequest(VirtualPath.Parse("/proc")), fixture.SystemContext());

        Assert.Contains("42", Names(restricted));
        Assert.DoesNotContain("43", Names(restricted));
        Assert.Equal(FileSystemErrorCode.NotFound, hidden.Error?.Code);
        Assert.Contains("43", Names(listed));
        Assert.Contains("42", Names(system));
        Assert.Contains("43", Names(system));
    }

    [Fact]
    public async Task Stopped_processes_never_appear()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Processes.Active.Add(Process(9, "org.tests.stopped", ProcessState.Stopped));

        FileSystemResult<FileSystemEntrySnapshot> stat = await fixture.Service.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/proc/9")), fixture.SystemContext());

        Assert.Equal(FileSystemErrorCode.NotFound, stat.Error?.Code);
    }

    [Fact]
    public async Task Self_links_to_the_calling_process_and_is_absent_without_one()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Processes.Active.Add(Process(42, "org.tests.own"));

        FileSystemResult<FileSystemEntrySnapshot> self = await fixture.Service.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/proc/self"), FileSystemLinkBehavior.NoFollow),
            fixture.AppContext(42, Read));
        string status = await fixture.ReadTextAsync("/proc/self/status", fixture.AppContext(42, Read));
        FileSystemResult<FileSystemEntrySnapshot> noProcess = await fixture.Service.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/proc/self"), FileSystemLinkBehavior.NoFollow),
            fixture.AppContext(null, Read));

        Assert.Equal("/proc/42", Assert.IsType<SymbolicLinkMetadata>(self.Value!.Metadata).Target);
        Assert.Contains("Pid:\t42\n", status);
        Assert.Equal(FileSystemErrorCode.NotFound, noProcess.Error?.Code);
    }

    [Fact]
    public async Task Entry_identity_is_stable_and_files_report_zero_length()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        FileSystemStatRequest request = new(VirtualPath.Parse("/proc/meminfo"));

        FileSystemEntrySnapshot first = (await fixture.Service.StatAsync(request, fixture.SystemContext())).Value!;
        fixture.Clock.Advance(5);
        FileSystemEntrySnapshot second = (await fixture.Service.StatAsync(request, fixture.SystemContext())).Value!;

        Assert.Equal(first.Metadata.Id, second.Metadata.Id);
        Assert.Equal(0, Assert.IsType<FileMetadata>(second.Metadata).Length);
        Assert.Equal(first.Metadata.Revision + 5, second.Metadata.Revision);
        Assert.Equal(BootUtc, second.Metadata.Timestamps.CreatedAtUtc);
        Assert.Equal(fixture.Clock.UtcNow, second.Metadata.Timestamps.ContentModifiedAtUtc);
    }

    [Fact]
    public async Task Mutations_are_refused_with_permission_denied()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Processes.Active.Add(Process(42, "org.tests.own"));
        VirtualPath meminfo = VirtualPath.Parse("/proc/meminfo");

        // An ordinary user is stopped by the read-only modes; even System authority, which skips
        // mode checks, is refused by the provider itself.
        foreach (FileSystemAuthorizationContext context in new[] { fixture.AppContext(42, Read, Write), fixture.SystemContext() })
        {
            FileSystemMutationResult create = await fixture.Service.CreateAsync(
                new FileSystemCreateRequest(VirtualPath.Parse("/proc/new"), FileSystemEntryKind.File, FileSystemPermissions.FromMode(0x1A4)),
                context);
            FileSystemMutationResult write = await fixture.Service.WriteAsync(
                new FileSystemWriteRequest(meminfo), new BytesSource([1]), context);
            FileSystemMutationResult delete = await fixture.Service.DeleteAsync(
                new FileSystemDeleteRequest(meminfo, 1, 1), context);
            FileSystemMutationResult chmod = await fixture.Service.SetPermissionsAsync(
                new FileSystemSetPermissionsRequest(meminfo, FileSystemPermissions.FromMode(0x1FF), 1), context);
            FileSystemMutationResult move = await fixture.Service.MoveAsync(
                new FileSystemMoveRequest(meminfo, VirtualPath.Parse("/proc/moved"), 1, 1, 1), context);
            FileSystemMutationResult copy = await fixture.Service.CopyAsync(
                new FileSystemCopyRequest(meminfo, VirtualPath.Parse("/proc/copied"), 1, 1), context);

            Assert.All(
                new[] { create, write, delete, chmod, move, copy },
                static result => Assert.Equal(FileSystemErrorCode.PermissionDenied, result.Transaction.Error?.Code));
        }

        Assert.Equal(0, fixture.Root.Mutations);
    }

    [Fact]
    public async Task Reading_every_proc_entry_never_writes_to_persistent_storage()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        fixture.Processes.Active.Add(Process(42, "org.tests.own"));
        FileSystemAuthorizationContext context = fixture.AppContext(42, Read, ProcessList);

        foreach (string path in new[]
        {
            "/proc/meminfo", "/proc/cpuinfo", "/proc/uptime", "/proc/loadavg",
            "/proc/42/status", "/proc/42/cmdline", "/proc/42/environ", "/proc/self/status"
        })
        {
            await fixture.ReadTextAsync(path, context);
        }

        await fixture.Service.EnumerateAsync(new FileSystemEnumerateRequest(VirtualPath.Parse("/proc/42")), context);

        Assert.Equal(0, fixture.Root.Mutations);
    }

    private static IEnumerable<string> Names(FileSystemResult<FileSystemDirectorySnapshot> listing) =>
        listing.Value!.Entries.Select(static entry => entry.Name.Value);
}
