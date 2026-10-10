using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Events;
using HackerOs.Platform.Core.FileSystem;
using HackerOs.Platform.Core.FileSystem.Virtual;
using HackerOs.Platform.Core.Policy;
using HackerOs.Platform.Core.Processes;
using HackerOs.Platform.Core.Time;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Tests.FileSystem.Virtual;

/// <summary>
/// Builds the mounted filesystem the OS composes (ADR 0046): an in-memory root standing in for
/// IndexedDB, with <c>/proc</c> and <c>/dev</c> mounted over it, a deterministic clock and
/// deterministic process/resource doubles.
/// </summary>
internal sealed class VirtualFileSystemFixture
{
    internal static readonly DateTimeOffset BootUtc = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    internal static readonly LocalUserId Alice = LocalUserId.FromGuid(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    internal static readonly SessionId Session = SessionId.FromGuid(new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

    internal static readonly VirtualHardwareProfile Hardware =
        new("test-rig", cpuCapacity: 1.5, memoryCapacityBytes: 4L * 1024 * 1024 * 1024, 1, 1);

    internal VirtualFileSystemFixture()
    {
        Clock = new ManualSimulationClock(BootUtc, TimeSpan.FromMilliseconds(100));
        Root = new MutationCountingProvider(new InMemoryFileSystemRepository());
        Proc = new ProcFileSystemProvider(() => Processes, Resources, Hardware, Clock, environments: () => Environments);
        Dev = new DevFileSystemProvider(Clock);
        Router = new FileSystemMountRouter(Root,
        [
            new FileSystemMount(ProcFileSystemProvider.DefaultMountPath, Proc),
            new FileSystemMount(DevFileSystemProvider.DefaultMountPath, Dev)
        ]);
        TopicBus = new InMemoryTopicMessageBus(new CapabilityGrantRepository());
        Service = new FileSystemService(Router, new FileSystemPathResolver(Router), new FileSystemAuthorizer(), TopicBus);
        Seeder = new FileSystemSeeder(Service, mountRouter: Router);
    }

    internal ManualSimulationClock Clock { get; }

    internal FakeProcessManager Processes { get; } = new();

    /// <summary>Per-process environment table read by <c>/proc</c>; empty unless a test fills it.</summary>
    internal InMemoryProcessEnvironmentTable Environments { get; } = new();

    internal FakeResourceSimulator Resources { get; } = new();

    internal MutationCountingProvider Root { get; }

    internal ProcFileSystemProvider Proc { get; }

    internal DevFileSystemProvider Dev { get; }

    internal FileSystemMountRouter Router { get; }

    internal InMemoryTopicMessageBus TopicBus { get; }

    internal FileSystemService Service { get; }

    internal FileSystemSeeder Seeder { get; }

    /// <summary>Seeds the clean profile, then forgets the seeding writes so tests count only their own.</summary>
    internal static async Task<VirtualFileSystemFixture> CreateSeededAsync()
    {
        VirtualFileSystemFixture fixture = new();
        await fixture.Seeder.SeedAsync("alice", "users");
        fixture.Root.ResetCounts();
        return fixture;
    }

    internal static ProcessRecord Process(
        long pid,
        string appId,
        ProcessState state = ProcessState.Running,
        long? parentPid = null,
        AppKind kind = AppKind.Window,
        LocalUserId? userId = null)
    {
        DateTimeOffset started = BootUtc.AddSeconds(pid);
        bool hasStarted = state is ProcessState.Running or ProcessState.Stopping;
        bool isTerminal = state is ProcessState.Stopped or ProcessState.Faulted;
        return new ProcessRecord(
            ProcessId.FromInt64(pid),
            parentPid is { } parent ? ProcessId.FromInt64(parent) : null,
            appId,
            AppInstanceId.FromGuid(Guid.NewGuid()),
            kind,
            userId ?? Alice,
            Session,
            state,
            ResourceProfile.None,
            serviceHealth: null,
            started,
            hasStarted ? started : null,
            isTerminal ? started.AddSeconds(1) : null,
            exitCode: null,
            exitReason: isTerminal ? ProcessExitReason.Completed : null);
    }

    /// <summary>Context of an app running as Alice, inside process <paramref name="pid"/>.</summary>
    internal FileSystemAuthorizationContext AppContext(long? pid, params string[] capabilities) =>
        new(
            new AppOperationContext
            {
                AppId = "org.tests.virtual-fs",
                UserId = Alice.ToString(),
                UserName = "alice",
                UserAuthority = AppAuthority.User,
                GrantedCapabilities = new HashSet<string>(capabilities, StringComparer.Ordinal),
                ProcessId = pid
            },
            ["users"],
            Clock.UtcNow);

    /// <summary>Trusted platform context (System authority, every capability).</summary>
    internal FileSystemAuthorizationContext SystemContext() =>
        new(
            new AppOperationContext
            {
                AppId = "org.hackeros.kernel",
                UserId = "system",
                UserAuthority = AppAuthority.System,
                GrantedCapabilities = new HashSet<string>(AppCapabilities.All, StringComparer.Ordinal),
                IsSystemOperation = true
            },
            ["system"],
            Clock.UtcNow);

    internal async Task<string> ReadTextAsync(string path, FileSystemAuthorizationContext context)
    {
        FileSystemResult<FileSystemContentReadHandle> read =
            await Service.ReadAsync(new FileSystemReadRequest(VirtualPath.Parse(path)), context);
        Assert.True(read.Succeeded, $"Reading {path} failed with {read.Error?.Code}.");
        await using FileSystemContentReadHandle handle = read.Value!;
        using StreamReader reader = new(handle.Content);
        return await reader.ReadToEndAsync();
    }

    /// <summary>A process table double: only the read side the procfs provider uses is implemented.</summary>
    internal sealed class FakeProcessManager : IProcessManager
    {
        internal List<ProcessRecord> Active { get; } = [];

        internal List<ProcessRecord> History { get; } = [];

        public IReadOnlyList<ProcessRecord> GetActiveProcesses() => [.. Active];

        public IReadOnlyList<ProcessRecord> GetHistory() => [.. History];

        public bool TryGetActive(ProcessId pid, out ProcessRecord process)
        {
            process = Active.FirstOrDefault(candidate => candidate.Pid == pid)!;
            return process is not null;
        }

        public ProcessRecord Start(ProcessStartRequest request) => throw new NotSupportedException();

        public ProcessRecord MarkRunning(ProcessId pid) => throw new NotSupportedException();

        public ProcessRecord Complete(ProcessId pid, int exitCode) => throw new NotSupportedException();

        public ProcessRecord Fault(ProcessId pid, ProcessExitReason reason = ProcessExitReason.Fault) =>
            throw new NotSupportedException();

        public Task<ProcessRecord> StopAsync(
            ProcessId pid, TimeSpan timeout, ProcessExitReason reason = ProcessExitReason.CloseRequested, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ProcessRecord Kill(ProcessId pid, ProcessExitReason reason = ProcessExitReason.Killed) =>
            throw new NotSupportedException();

        public bool TryGetSingleton(string appId, out ProcessRecord process) => throw new NotSupportedException();

        public CancellationToken GetCancellationToken(ProcessId pid) => throw new NotSupportedException();
    }

    /// <summary>Resource simulator double returning exactly the samples a test supplies.</summary>
    internal sealed class FakeResourceSimulator : IResourceSimulator
    {
        internal Dictionary<ProcessId, List<ProcessResourceSample>> ProcessSamples { get; } = [];

        internal List<SystemResourceSample> SystemSamples { get; } = [];

        public IReadOnlyList<ProcessResourceSample> Tick(
            IReadOnlyList<ProcessRecord> activeProcesses,
            IReadOnlyDictionary<ProcessId, WorkloadActivity>? activity = null) =>
            throw new NotSupportedException();

        public IReadOnlyList<ProcessResourceSample> GetHistory(ProcessId pid) =>
            ProcessSamples.TryGetValue(pid, out List<ProcessResourceSample>? samples) ? [.. samples] : [];

        public IReadOnlyList<SystemResourceSample> GetSystemHistory() => [.. SystemSamples];
    }

    /// <summary>
    /// Wraps the root provider (the IndexedDB stand-in) and counts every mutation that reaches it,
    /// proving virtual paths never write to persistent storage.
    /// </summary>
    internal sealed class MutationCountingProvider(IFileSystemProvider inner) : IFileSystemProvider
    {
        internal int Mutations { get; private set; }

        internal void ResetCounts() => Mutations = 0;

        public string ProviderId => inner.ProviderId;

        public ValueTask<FileSystemResult<FileSystemContentReadHandle>> ReadAsync(
            FileSystemReadRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(request, context, cancellationToken);

        public ValueTask<FileSystemResult<FileSystemDirectorySnapshot>> EnumerateAsync(
            FileSystemEnumerateRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default) =>
            inner.EnumerateAsync(request, context, cancellationToken);

        public ValueTask<FileSystemResult<FileSystemEntrySnapshot>> StatAsync(
            FileSystemStatRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default) =>
            inner.StatAsync(request, context, cancellationToken);

        public ValueTask<FileSystemMutationResult> CreateAsync(
            FileSystemCreateRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default)
        {
            Mutations++;
            return inner.CreateAsync(request, context, cancellationToken);
        }

        public ValueTask<FileSystemMutationResult> WriteAsync(
            FileSystemWriteRequest request, IFileSystemContentSource content, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default)
        {
            Mutations++;
            return inner.WriteAsync(request, content, context, cancellationToken);
        }

        public ValueTask<FileSystemMutationResult> MoveAsync(
            FileSystemMoveRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default)
        {
            Mutations++;
            return inner.MoveAsync(request, context, cancellationToken);
        }

        public ValueTask<FileSystemMutationResult> CopyAsync(
            FileSystemCopyRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default)
        {
            Mutations++;
            return inner.CopyAsync(request, context, cancellationToken);
        }

        public ValueTask<FileSystemMutationResult> DeleteAsync(
            FileSystemDeleteRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default)
        {
            Mutations++;
            return inner.DeleteAsync(request, context, cancellationToken);
        }

        public ValueTask<FileSystemMutationResult> SetPermissionsAsync(
            FileSystemSetPermissionsRequest request, FileSystemAuthorizationContext context, CancellationToken cancellationToken = default)
        {
            Mutations++;
            return inner.SetPermissionsAsync(request, context, cancellationToken);
        }
    }

    /// <summary>In-memory content source for write requests.</summary>
    internal sealed class BytesSource(byte[] bytes) : IFileSystemContentSource
    {
        public FileSystemContentDescriptor Descriptor => FileSystemContentDescriptor.Binary();

        public long? Length => bytes.Length;

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }
}
