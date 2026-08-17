using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Time;

namespace HackerOs.Platform.Core.Execution;

/// <summary>Provides one app instance's read-only deterministic simulation clock access.</summary>
public sealed class AppClockGateway : IAppClockGateway
{
    private readonly ISimulationClock _clock;

    /// <summary>Initializes a clock gateway bound to the shared simulation clock.</summary>
    public AppClockGateway(ISimulationClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow => _clock.UtcNow;

    /// <inheritdoc />
    public long CurrentTick => _clock.CurrentTick;

    /// <inheritdoc />
    public IDisposable Schedule(TimeSpan delay, Action callback) => _clock.Schedule(delay, callback);

    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
        _clock.DelayAsync(delay, cancellationToken);
}
