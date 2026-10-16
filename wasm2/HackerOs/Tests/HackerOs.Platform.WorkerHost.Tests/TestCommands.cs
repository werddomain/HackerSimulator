using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Diagnostics;
using HackerOs.Simulation.Abstractions.Diagnostics;

namespace HackerOs.Platform.WorkerHost.Tests;

/// <summary>Test apps for the worker tests: a command that never yields, one that ignores its token, a service.</summary>
internal static class TestCommands
{
    public const string AssemblyFile = "HackerOs.Platform.WorkerHost.Tests.dll";

    private static volatile bool s_release;

    /// <summary>Gets the manifests of the test apps.</summary>
    public static IReadOnlyList<AppManifest> Manifests { get; } =
    [
        Command<SpinCommand>("spin"),
        Command<StubbornCommand>("stubborn"),
        Command<WorkerInfoCommand>("whereami"),
        Command<DedicatedEchoCommand>("dedicated-echo", AppExecutionHost.Dedicated),
        Command<ThrowCommand>("throw"),
        Service<TestService>("org.test.ticker")
    ];

    /// <summary>Lets every <c>spin</c> stuck in a terminated loopback worker return, so the test run ends cleanly.</summary>
    public static void ReleaseSpinners() => s_release = true;

    public static void ArmSpinners() => s_release = false;

    private static AppManifest Command<TCommand>(string name, AppExecutionHost execution = AppExecutionHost.Pool) => new()
    {
        Id = $"org.test.{name}",
        Name = name,
        Version = "1.0.0",
        PublisherId = "org.test",
        Description = $"Test command {name}.",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest(AssemblyFile, typeof(TCommand).FullName!),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Hidden, []),
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest(name, [], $"{name} [args]"),
        Execution = execution
    };

    private static AppManifest Service<TService>(string id) => new()
    {
        Id = id,
        Name = "ticker",
        Version = "1.0.0",
        PublisherId = "org.test",
        Description = "Test service.",
        Kind = AppKind.Service,
        EntryPoint = new AppEntryPointManifest(AssemblyFile, typeof(TService).FullName!),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Hidden, []),
        Resources = AppResourceProfileManifest.None,
        Execution = AppExecutionHost.Dedicated
    };

    /// <summary>
    /// <c>spin</c>: an infinite synchronous loop that never yields its thread, the case ADR 0070
    /// exists for. It writes one line first, then spins until the test run releases it.
    /// </summary>
    public sealed class SpinCommand(AppManifest manifest) : TerminalAppBase(manifest)
    {
        public override ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
        {
            context.StandardOutput.WriteLine("spinning");
            context.StandardOutput.Flush();
            while (!s_release)
            {
                Thread.SpinWait(1000);
            }

            return ValueTask.FromResult(0);
        }
    }

    /// <summary><c>stubborn</c>: keeps yielding but ignores its token and its stop request (escalation step 3).</summary>
    public sealed class StubbornCommand(AppManifest manifest) : TerminalAppBase(manifest)
    {
        public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
        {
            await context.StandardOutput.WriteLineAsync("ignoring");
            while (!s_release)
            {
                await Task.Delay(10, CancellationToken.None);
            }

            return 0;
        }
    }

    /// <summary><c>whereami</c>: prints whether it runs in a worker (its context type) and its environment.</summary>
    public sealed class WorkerInfoCommand(AppManifest manifest) : TerminalAppBase(manifest)
    {
        public override ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
        {
            string where = context.App.GetType().Name == "RemoteAppExecutionContext" ? "worker" : "main";
            context.StandardOutput.WriteLine($"{where} pid={context.App.ProcessId.Value} cwd={HackEnvironment.CurrentDirectory} user={context.App.UserLoginName}");
            return ValueTask.FromResult(where == "worker" ? 0 : 1);
        }
    }

    /// <summary><c>dedicated-echo</c>: echo, declared <see cref="AppExecutionHost.Dedicated"/>.</summary>
    public sealed class DedicatedEchoCommand(AppManifest manifest) : TerminalAppBase(manifest)
    {
        public override ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
        {
            context.StandardOutput.WriteLine(string.Join(' ', context.Arguments));
            return ValueTask.FromResult(0);
        }
    }

    /// <summary><c>throw</c>: an exception escapes the entry point.</summary>
    public sealed class ThrowCommand(AppManifest manifest) : TerminalAppBase(manifest)
    {
        public override ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }

    /// <summary>A service that logs a line per simulated second and reports its stop.</summary>
    public sealed class TestService(AppManifest manifest) : ServiceAppBase(manifest)
    {
        protected override async Task RunCoreAsync(IAppExecutionContext context, CancellationToken sessionCancellationToken)
        {
            context.Logging.Log(DiagnosticSeverity.Information, $"ticker started in {context.GetType().Name}");
            while (!sessionCancellationToken.IsCancellationRequested)
            {
                await context.Clock.DelayAsync(TimeSpan.FromSeconds(1), sessionCancellationToken);
                await HackProcess.CheckpointAsync(sessionCancellationToken);
            }
        }

        protected override ValueTask OnStoppingAsync(ServiceStopReason reason, CancellationToken cancellationToken)
        {
            HackExecutionContext.Current.Logging.Log(DiagnosticSeverity.Information, $"ticker stopping: {reason}");
            return ValueTask.CompletedTask;
        }
    }
}
