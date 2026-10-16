using HackerOs.Simulation.Abstractions.Workers;

namespace HackerOs.Platform.WorkerHost.Tests;

/// <summary>
/// ADR 0070, sections 1 and 3: a service runs in a worker of its own, taken from the ready worker;
/// <c>systemctl stop</c> reaches its <c>StopRequested</c> and <c>OnStoppingAsync</c> in the worker, and
/// the worker is freed when the service ends.
/// </summary>
public sealed class DedicatedServiceTests
{
    [Fact]
    public async Task A_service_runs_in_a_dedicated_worker_and_stops_cleanly_through_systemctl()
    {
        await using OsFixture os = await WorkerPoolTests.StartedAsync();

        ShellRun start = await os.RunAsync("systemctl start org.test.ticker").WaitAsync(TimeSpan.FromSeconds(10));
        await OsFixture.WaitUntilAsync(() => os.FindPid("org.test.ticker") is not null);
        long pid = os.FindPid("org.test.ticker")!.Value;
        await OsFixture.WaitUntilAsync(() => os.Pool!.GetWorkers().Any(w => w.Role == WorkerRole.Dedicated && w.ProcessIds.Contains(pid)));
        ShellRun active = await os.RunAsync("systemctl is-active org.test.ticker").WaitAsync(TimeSpan.FromSeconds(10));
        os.Clock.Advance(3);

        Task<ShellRun> stopping = os.RunAsync("systemctl stop org.test.ticker");
        for (int second = 0; second < 10 && !stopping.IsCompleted; second++)
        {
            await Task.WhenAny(stopping, Task.Delay(50));
            os.Clock.Advance();
        }

        ShellRun stop = await stopping.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, start.ExitCode);
        Assert.Equal(0, active.ExitCode);
        Assert.Equal(0, stop.ExitCode);
        Assert.Null(os.FindPid("org.test.ticker"));
        // The dedicated worker is freed with its process; the ready worker is back.
        await OsFixture.WaitUntilAsync(() =>
            os.Pool!.GetWorkers().All(w => w.Role != WorkerRole.Dedicated)
            && os.Pool.GetWorkers().Any(w => w.Role == WorkerRole.Ready));
        string log = await os.ReadAppLogAsync("org.test.ticker");
        Assert.Contains("ticker started in RemoteAppExecutionContext", log, StringComparison.Ordinal);
        Assert.Contains("ticker stopping", log, StringComparison.Ordinal);
    }
}
