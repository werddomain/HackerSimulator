using System.Diagnostics;
using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Commands.Echo;
using HackerOs.Commands.Grep;
using HackerOs.Platform.Core.Execution;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Processes;
using Xunit.Abstractions;

namespace HackerOs.Platform.ResourceAccounting.Tests;

/// <summary>
/// ADR 0067, section 5: the CPU measurement must cost less than 5 % on a reference scenario (open ten
/// windows, type in an editor, run a shell pipeline), compared with the same scenario and no hook.
/// </summary>
/// <remarks>
/// Timing on a shared machine is noisy, so each mode keeps its fastest of several interleaved rounds
/// (the run least disturbed by the rest of the machine), and the comparison is retried a few times
/// before the test fails: a real regression is over 5 % on every attempt, a noisy neighbour is not.
/// </remarks>
[Trait("Category", "Performance")]
public sealed class CpuAccountingOverheadTests(ITestOutputHelper output)
{
    private const double MaximumOverhead = 0.05;
    private const int Rounds = 9;
    private const int Attempts = 3;
    private const int Keystrokes = 300;
    private const int PipelineRuns = 25;

    [Fact]
    public async Task Measuring_cpu_time_costs_less_than_five_percent_on_the_reference_scenario()
    {
        // Warm-up: JIT, renderer caches, thread pool.
        await RunScenarioAsync();
        await MeasuredAsync(RunScenarioAsync);

        double overhead = double.MaxValue;
        for (int attempt = 1; attempt <= Attempts && overhead > MaximumOverhead; attempt++)
        {
            TimeSpan off = TimeSpan.MaxValue;
            TimeSpan on = TimeSpan.MaxValue;
            for (int round = 0; round < Rounds; round++)
            {
                off = Min(off, await TimeAsync(RunScenarioAsync));
                on = Min(on, await TimeAsync(() => MeasuredAsync(RunScenarioAsync)));
            }

            overhead = (on - off) / off;
            output.WriteLine($"attempt {attempt}: off {off.TotalMilliseconds:0.00} ms, on {on.TotalMilliseconds:0.00} ms, overhead {overhead:P2}");
        }

        Assert.True(overhead <= MaximumOverhead, $"CPU accounting overhead {overhead:P2} exceeds {MaximumOverhead:P0}.");
    }

    /// <summary>One run of the reference scenario.</summary>
    private static async Task RunScenarioAsync()
    {
        // Ten windows: the real Calculator, rendered the way the window host renders it.
        await using RealAppsMeasurementTests.WindowRenderer renderer = new();
        for (long pid = 201; pid <= 210; pid++)
        {
            await renderer.RenderCalculatorAsync(pid);
        }

        await TypeInEditorAsync(renderer);

        // A shell pipeline: echo | grep, both real commands.
        AppManifest echoManifest = AppDefinitionMapper.ToManifest(new EchoApp());
        AppManifest grepManifest = AppDefinitionMapper.ToManifest(new GrepApp());
        for (int run = 0; run < PipelineRuns; run++)
        {
            string lines = await RealAppsMeasurementTests.RunCommandAsync(
                new EchoCommand(echoManifest), 230, ["alpha", "beta", "gamma", "alphabet", "delta"]);
            string matches = await RealAppsMeasurementTests.RunCommandAsync(new GrepCommand(grepManifest), 231, ["alpha"], lines);
            Assert.Contains("alpha", matches, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Keystrokes delivered to an editor process: each one is an event handled in the process's frame
    /// with an asynchronous continuation, then the window renders again (Blazor re-renders after each
    /// event); every 25 keys the editor calls an OS gateway (an autosave check's <c>statfs</c>), which
    /// opens the kernel sub-frame.
    /// </summary>
    private static async Task TypeInEditorAsync(RealAppsMeasurementTests.WindowRenderer renderer)
    {
        IAppExecutionContext editor = new HackerOs.Tests.Support.MinimalAppExecutionContext(
            TestContexts.Process(220).Manifest, processId: ProcessId.FromInt64(220));
        AppDriveGateway drives = new(() => new NoMounts(), VirtualHardwareProfile.Default);
        StringBuilder document = new();
        for (int key = 0; key < Keystrokes; key++)
        {
            using (HackExecutionContext.Enter(editor))
            {
                await Task.Yield();
                document.Append((char)('a' + (key % 26)));
                if (key % 25 == 0)
                {
                    await drives.GetDrivesAsync();
                }
            }

            await renderer.RenderCalculatorAsync(220);
        }

        Assert.Equal(Keystrokes, document.Length);
    }

    /// <summary>Runs <paramref name="scenario"/> with the hook installed and the CPU accountant observing, as the OS composes them.</summary>
    private static async Task MeasuredAsync(Func<Task> scenario)
    {
        ContextSwitchDispatcher dispatcher = new();
        MeasuredCpuTimeAccountant accountant = new(TimeSpan.FromSeconds(5));
        using IDisposable registration = dispatcher.Register(accountant);
        using (ContextSwitchHook.Install(dispatcher))
        {
            await scenario();
        }

        Assert.True(accountant.TryGetProcessTimes(ProcessId.FromInt64(220), out _), "the editor was measured");
    }

    private static async Task<TimeSpan> TimeAsync(Func<Task> scenario)
    {
        Stopwatch watch = Stopwatch.StartNew();
        await scenario();
        return watch.Elapsed;
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;

    private sealed class NoMounts : IFileSystemMountRouter
    {
        public IReadOnlyList<FileSystemMount> Mounts => [];

        public FileSystemMountResolution Resolve(VirtualPath path) => throw new NotSupportedException();
    }
}
