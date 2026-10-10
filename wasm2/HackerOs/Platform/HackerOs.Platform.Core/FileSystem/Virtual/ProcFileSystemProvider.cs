using System.Globalization;
using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Time;

namespace HackerOs.Platform.Core.FileSystem.Virtual;

/// <summary>
/// Serves the <c>/proc</c> mount (ADR 0046): one directory per visible active process plus the
/// system files <c>meminfo</c>, <c>cpuinfo</c>, <c>uptime</c> and <c>loadavg</c>, all computed from
/// <see cref="IProcessManager"/>, <see cref="IResourceSimulator"/>, <see cref="VirtualHardwareProfile"/>
/// and <see cref="ISimulationClock"/> at the moment they are read. Nothing is ever stored.
/// </summary>
/// <remarks>
/// <para>
/// <b>Visibility.</b> Only active processes appear. A caller always sees its own process
/// (<see cref="AppOperationContext.ProcessId"/>); it sees the others only with
/// <see cref="AppCapabilities.ProcessList"/> or <see cref="AppAuthority.System"/>, mirroring
/// <c>IAppProcessGateway.ListProcesses</c>. An invisible PID answers <see cref="FileSystemErrorCode.NotFound"/>.
/// </para>
/// <para>
/// <b>Process state (ADR 0050, section 5).</b> <c>cmdline</c>, <c>environ</c> and <c>cwd</c> come
/// from the <see cref="IProcessEnvironmentTable"/>: the arguments each followed by NUL, the
/// <c>NAME=value</c> pairs each followed by NUL, and a link to the real working directory. Without
/// a table (older wiring, some tests), they fall back to the app ID, an empty file and the owner's
/// home directory.
/// </para>
/// </remarks>
public sealed class ProcFileSystemProvider : VirtualFileSystemProvider
{
    /// <summary>Canonical <c>/proc</c> mount path.</summary>
    public static readonly VirtualPath DefaultMountPath = VirtualPath.Parse("/proc");

    private const ushort DirectoryMode = 0b101_101_101; // 0555
    private const ushort PublicFileMode = 0b100_100_100; // 0444
    private const ushort PrivateFileMode = 0b100_000_000; // 0400
    private const int MaximumProcessNameLength = 15;

    private static readonly string[] SystemFiles = ["cpuinfo", "loadavg", "meminfo", "uptime"];
    private static readonly string[] ProcessEntries = ["cmdline", "cwd", "environ", "status"];

    private readonly Func<IProcessManager> _processManager;
    private readonly IResourceSimulator _resources;
    private readonly VirtualHardwareProfile _hardware;
    private readonly Func<ProcessRecord, string?>? _userNameResolver;
    private readonly Func<IProcessEnvironmentTable?>? _environments;

    /// <summary>Initializes the <c>/proc</c> provider.</summary>
    /// <param name="processManager">
    /// Lazily resolves the process table. Lazy because the process manager depends on the session
    /// service, which depends on the filesystem seeder, which reaches this provider through the
    /// mount router: resolving eagerly would close a dependency cycle.
    /// </param>
    /// <param name="resources">Resource simulator supplying per-process and system samples.</param>
    /// <param name="hardware">Virtual hardware capacity behind <c>meminfo</c> and <c>cpuinfo</c>.</param>
    /// <param name="clock">Simulation clock supplying uptime, revisions and timestamps.</param>
    /// <param name="userNameResolver">
    /// Optional login-name lookup for a process owner, used to build its <c>cwd</c> link
    /// (<c>/home/&lt;login&gt;</c>). When it returns <see langword="null"/>, the caller's own login
    /// name is used for its own processes and the raw user ID otherwise.
    /// </param>
    /// <param name="transactionIdFactory">Optional deterministic transaction ID source for tests.</param>
    /// <param name="environments">
    /// Lazily resolves the per-process environment table behind <c>cmdline</c>, <c>environ</c> and
    /// <c>cwd</c> (lazy for the same cycle as <paramref name="processManager"/>).
    /// </param>
    public ProcFileSystemProvider(
        Func<IProcessManager> processManager,
        IResourceSimulator resources,
        VirtualHardwareProfile hardware,
        ISimulationClock clock,
        Func<ProcessRecord, string?>? userNameResolver = null,
        Func<Guid>? transactionIdFactory = null,
        Func<IProcessEnvironmentTable?>? environments = null)
        : base(DefaultMountPath, clock, transactionIdFactory)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
        _userNameResolver = userNameResolver;
        _environments = environments;
    }

    /// <inheritdoc />
    public override string ProviderId => "procfs";

    /// <inheritdoc />
    public override ValueTask<FileSystemResult<FileSystemContentReadHandle>> ReadAsync(
        FileSystemReadRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(Failure<FileSystemContentReadHandle>(
                FileSystemOperation.Read, FileSystemErrorCode.Cancelled, request.Path));
        }

        FileSystemResult<FileSystemEntrySnapshot> stat = Lookup(request.Path, context, FileSystemOperation.Read);
        if (!stat.Succeeded)
        {
            return ValueTask.FromResult(FileSystemResult<FileSystemContentReadHandle>.Failure(stat.Error!));
        }

        FileSystemEntryMetadata metadata = stat.Value!.Metadata;
        if (metadata is not FileMetadata)
        {
            return ValueTask.FromResult(Failure<FileSystemContentReadHandle>(
                FileSystemOperation.Read, FileSystemErrorCode.NotFile, request.Path));
        }

        string[] segments = RelativeSegments(request.Path);
        string text = segments.Length == 1
            ? RenderSystemFile(segments[0])
            : RenderProcessFile(FindVisible(ParsePid(segments[0])!.Value, context)!, segments[1]);
        return ValueTask.FromResult(TextHandle(request.Path, metadata, text));
    }

    /// <inheritdoc />
    public override ValueTask<FileSystemResult<FileSystemDirectorySnapshot>> EnumerateAsync(
        FileSystemEnumerateRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        FileSystemResult<FileSystemEntrySnapshot> stat = Lookup(request.Path, context, FileSystemOperation.Enumerate);
        if (!stat.Succeeded)
        {
            return ValueTask.FromResult(FileSystemResult<FileSystemDirectorySnapshot>.Failure(stat.Error!));
        }

        if (stat.Value!.Metadata is not DirectoryMetadata)
        {
            return ValueTask.FromResult(Failure<FileSystemDirectorySnapshot>(
                FileSystemOperation.Enumerate, FileSystemErrorCode.NotDirectory, request.Path));
        }

        string[] segments = RelativeSegments(request.Path);
        IEnumerable<string> names = segments.Length == 0
            ? RootNames(context)
            : ProcessEntries;

        List<FileSystemDirectoryItem> items = [];
        foreach (string name in names.Order(StringComparer.Ordinal))
        {
            FileSystemResult<FileSystemEntrySnapshot> child =
                Lookup(Child(request.Path, name), context, FileSystemOperation.Enumerate);
            if (child.Succeeded)
            {
                items.Add(new FileSystemDirectoryItem(FileSystemEntryName.Parse(name), child.Value!.Metadata));
            }
        }

        return ValueTask.FromResult(FileSystemResult<FileSystemDirectorySnapshot>.Success(
            new FileSystemDirectorySnapshot(request.Path, CurrentRevision, items)));
    }

    /// <inheritdoc />
    public override ValueTask<FileSystemResult<FileSystemEntrySnapshot>> StatAsync(
        FileSystemStatRequest request,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Lookup(request.Path, context, FileSystemOperation.Stat));

    /// <summary>Resolves one path under <c>/proc</c> to its current metadata, without following links.</summary>
    private FileSystemResult<FileSystemEntrySnapshot> Lookup(
        VirtualPath path,
        FileSystemAuthorizationContext context,
        FileSystemOperation operation)
    {
        string[] segments = RelativeSegments(path);
        FileSystemEntryMetadata? metadata = segments switch
        {
            [] => Directory(path, SystemPrincipal, SystemPrincipal, DirectoryMode, BootTimeUtc),
            ["self"] => SelfLink(path, context),
            [string name] when SystemFiles.Contains(name, StringComparer.Ordinal) =>
                File(path, SystemPrincipal, SystemPrincipal, PublicFileMode, BootTimeUtc),
            [string pid] => ParsePid(pid) is { } value && FindVisible(value, context) is { } process
                ? Directory(path, Owner(process), Owner(process), DirectoryMode, StartTime(process))
                : null,
            [string pid, string entry] => ParsePid(pid) is { } value && FindVisible(value, context) is { } process
                ? ProcessEntry(path, process, entry, context)
                : null,
            _ => null
        };

        return metadata is null
            ? Failure<FileSystemEntrySnapshot>(operation, FileSystemErrorCode.NotFound, path)
            : FileSystemResult<FileSystemEntrySnapshot>.Success(new FileSystemEntrySnapshot(path, metadata));
    }

    private FileSystemEntryMetadata? ProcessEntry(
        VirtualPath path,
        ProcessRecord process,
        string entry,
        FileSystemAuthorizationContext context)
    {
        string owner = Owner(process);
        DateTimeOffset started = StartTime(process);
        return entry switch
        {
            "status" or "cmdline" => File(path, owner, owner, PublicFileMode, started),
            "environ" => File(path, owner, owner, PrivateFileMode, started),
            "cwd" => Link(path, owner, owner, started,
                EnvironmentOf(process)?.WorkingDirectory ?? $"/home/{HomeName(process, context)}"),
            _ => null
        };
    }

    private SymbolicLinkMetadata? SelfLink(VirtualPath path, FileSystemAuthorizationContext context)
    {
        if (context.OperationContext.ProcessId is not { } pid
            || pid <= 0
            || FindVisible(pid, context) is not { } process)
        {
            return null;
        }

        return Link(path, Owner(process), Owner(process), StartTime(process), $"{MountPath.Value}/{pid.ToString(CultureInfo.InvariantCulture)}");
    }

    private IEnumerable<string> RootNames(FileSystemAuthorizationContext context)
    {
        foreach (ProcessRecord process in VisibleProcesses(context))
        {
            yield return process.Pid.ToString();
        }

        foreach (string file in SystemFiles)
        {
            yield return file;
        }

        if (SelfLink(Child(MountPath, "self"), context) is not null)
        {
            yield return "self";
        }
    }

    private IEnumerable<ProcessRecord> VisibleProcesses(FileSystemAuthorizationContext context)
    {
        bool seesAll = SeesAllProcesses(context);
        long? own = context.OperationContext.ProcessId;
        return ActiveProcesses().Where(process => seesAll || process.Pid.Value == own);
    }

    private ProcessRecord? FindVisible(long pid, FileSystemAuthorizationContext context) =>
        VisibleProcesses(context).FirstOrDefault(process => process.Pid.Value == pid);

    private static bool SeesAllProcesses(FileSystemAuthorizationContext context) =>
        context.OperationContext.EffectiveAuthority == AppAuthority.System
        || context.OperationContext.HasCapability(AppCapabilities.ProcessList);

    /// <summary>Gets live processes; <c>Stopped</c> (and other terminal) records never appear.</summary>
    private IEnumerable<ProcessRecord> ActiveProcesses() =>
        _processManager().GetActiveProcesses().Where(static process => !process.IsTerminal);

    private string RenderProcessFile(ProcessRecord process, string entry) => entry switch
    {
        "status" => RenderStatus(process),
        // Linux terminates every argument and every NAME=value pair with NUL.
        "cmdline" => EnvironmentOf(process) is { } state
            ? string.Concat(state.Arguments.Select(static argument => argument + "\0"))
            : $"{process.AppId}\0",
        "environ" => EnvironmentOf(process) is { } environment
            ? string.Concat(environment.Variables.Select(static pair => $"{pair.Key}={pair.Value}\0"))
            : string.Empty,
        _ => throw new InvalidOperationException($"'{entry}' is not a readable /proc/<pid> file.")
    };

    private ProcessEnvironmentSnapshot? EnvironmentOf(ProcessRecord process) =>
        _environments?.Invoke() is { } table && table.TryGet(process.Pid, out ProcessEnvironmentSnapshot snapshot)
            ? snapshot
            : null;

    private string RenderStatus(ProcessRecord process)
    {
        ProcessResourceSample? sample = _resources.GetHistory(process.Pid).LastOrDefault();
        long rssKb = (long)Math.Round((sample?.MemoryUsageBytes ?? 0) / 1024d, MidpointRounding.AwayFromZero);
        double cpuPercent = (sample?.CpuUsage ?? 0) * 100d;

        StringBuilder builder = new();
        AppendField(builder, "Name", ShortName(process.AppId));
        AppendField(builder, "State", StateText(process.State));
        AppendField(builder, "Pid", process.Pid.ToString());
        AppendField(builder, "PPid", process.ParentPid?.ToString() ?? "0");
        AppendField(builder, "Uid", process.UserId.ToString());
        AppendField(builder, "AppId", process.AppId);
        AppendField(builder, "Kind", process.Kind.ToString().ToLowerInvariant());
        AppendField(builder, "Session", process.SessionId.ToString());
        AppendField(builder, "VmRSS", $"{rssKb.ToString(CultureInfo.InvariantCulture)} kB");
        AppendField(builder, "Cpu", cpuPercent.ToString("0.0", CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private string RenderSystemFile(string name) => name switch
    {
        "meminfo" => RenderMemInfo(),
        "cpuinfo" => RenderCpuInfo(),
        "uptime" => RenderUptime(),
        "loadavg" => RenderLoadAverage(),
        _ => throw new InvalidOperationException($"'{name}' is not a /proc system file.")
    };

    private string RenderMemInfo()
    {
        long totalKb = (long)(_hardware.MemoryCapacityBytes / 1024d);
        double usedBytes = _resources.GetSystemHistory().LastOrDefault()?.TotalMemoryUsageBytes ?? 0;
        long freeKb = Math.Max(0, totalKb - (long)Math.Round(usedBytes / 1024d, MidpointRounding.AwayFromZero));

        // There is no page cache to reclaim in the simulation, so available memory equals free memory.
        StringBuilder builder = new();
        AppendMemInfoLine(builder, "MemTotal", totalKb);
        AppendMemInfoLine(builder, "MemFree", freeKb);
        AppendMemInfoLine(builder, "MemAvailable", freeKb);
        return builder.ToString();
    }

    private string RenderCpuInfo()
    {
        int cores = CoreCount;
        StringBuilder builder = new();
        for (int core = 0; core < cores; core++)
        {
            builder.Append(CultureInfo.InvariantCulture, $"processor\t: {core}\n");
            builder.Append("vendor_id\t: HackerOS\n");
            builder.Append(CultureInfo.InvariantCulture, $"model name\t: HackerOS Virtual CPU ({_hardware.Name})\n");
            builder.Append(CultureInfo.InvariantCulture, $"cpu cores\t: {cores}\n");
            builder.Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Renders <c>&lt;uptime&gt; &lt;idle&gt;</c> in seconds. Idle time is summed over every core,
    /// as on Linux, estimated from the mean utilization in the bounded system history.
    /// </summary>
    private string RenderUptime()
    {
        double uptime = TimeSpan.FromTicks(Clock.TickDuration.Ticks * Clock.CurrentTick).TotalSeconds;
        IReadOnlyList<SystemResourceSample> history = _resources.GetSystemHistory();
        double meanUsage = history.Count == 0 ? 0 : history.Average(static sample => sample.TotalCpuUsage);
        double idle = Math.Max(0, uptime * (CoreCount - meanUsage));
        return string.Create(CultureInfo.InvariantCulture, $"{uptime:0.00} {idle:0.00}\n");
    }

    /// <summary>
    /// Renders <c>&lt;1 min&gt; &lt;5 min&gt; &lt;15 min&gt; &lt;running&gt;/&lt;total&gt; &lt;last pid&gt;</c>.
    /// Each average is the mean <see cref="SystemResourceSample.TotalCpuUsage"/> over the samples of
    /// the bounded history that fall inside its window of simulated time.
    /// </summary>
    private string RenderLoadAverage()
    {
        IReadOnlyList<SystemResourceSample> history = _resources.GetSystemHistory();
        DateTimeOffset now = NowUtc;
        IProcessManager processes = _processManager();
        ProcessRecord[] active = [.. ActiveProcesses()];
        int running = active.Count(static process => process.State == ProcessState.Running);
        long lastPid = active.Concat(processes.GetHistory())
            .Select(static process => process.Pid.Value)
            .DefaultIfEmpty(0)
            .Max();

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Average(1):0.00} {Average(5):0.00} {Average(15):0.00} {running}/{active.Length} {lastPid}\n");

        double Average(int minutes)
        {
            DateTimeOffset since = now - TimeSpan.FromMinutes(minutes);
            SystemResourceSample[] window = [.. history.Where(sample => sample.AtUtc >= since)];
            return window.Length == 0 ? 0 : window.Average(static sample => sample.TotalCpuUsage);
        }
    }

    private int CoreCount => Math.Max(1, (int)Math.Ceiling(_hardware.CpuCapacity));

    private string HomeName(ProcessRecord process, FileSystemAuthorizationContext context)
    {
        string? resolved = _userNameResolver?.Invoke(process);
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            return resolved;
        }

        AppOperationContext caller = context.OperationContext;
        return string.Equals(caller.UserId, Owner(process), StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(caller.UserName)
            ? caller.UserName
            : Owner(process);
    }

    private static string Owner(ProcessRecord process) => process.UserId.ToString();

    private static DateTimeOffset StartTime(ProcessRecord process) => process.StartedAtUtc ?? process.CreatedAtUtc;

    private static long? ParsePid(string segment) =>
        segment.Length > 0
        && segment.All(char.IsAsciiDigit)
        && long.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out long pid)
        && pid > 0
            ? pid
            : null;

    /// <summary>Gets the last dotted segment of an app ID, cut to 15 characters like Linux's <c>comm</c>.</summary>
    private static string ShortName(string appId)
    {
        string name = appId[(appId.LastIndexOf('.') + 1)..];
        if (name.Length == 0)
        {
            name = appId;
        }

        return name.Length > MaximumProcessNameLength ? name[..MaximumProcessNameLength] : name;
    }

    private static string StateText(ProcessState state) => state switch
    {
        ProcessState.Created => "I (idle)",
        ProcessState.Starting => "S (sleeping)",
        ProcessState.Running => "R (running)",
        ProcessState.Stopping => "T (stopping)",
        ProcessState.Faulted => "Z (zombie)",
        _ => "X (dead)"
    };

    private static void AppendField(StringBuilder builder, string key, string value) =>
        builder.Append(key).Append(":\t").Append(value).Append('\n');

    /// <summary>Appends a line laid out like Linux's <c>meminfo</c>: a 16-column label, an 8-column value.</summary>
    private static void AppendMemInfoLine(StringBuilder builder, string key, long kilobytes) =>
        builder.Append(CultureInfo.InvariantCulture, $"{key + ":",-16}{kilobytes,8} kB\n");
}
