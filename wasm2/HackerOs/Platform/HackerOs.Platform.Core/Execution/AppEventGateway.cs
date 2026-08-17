using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Platform.Core.Execution;

/// <summary>Provides one app instance's typed event bus access via direct pass-through.</summary>
public sealed class AppEventGateway : IAppEventGateway
{
    private readonly IEventBus _eventBus;

    /// <summary>Initializes an event gateway bound to the shared event bus.</summary>
    public AppEventGateway(IEventBus eventBus)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull =>
        _eventBus.Subscribe(handler);

    /// <inheritdoc />
    public IReadOnlyList<EventDispatchFault> Publish<TEvent>(TEvent @event) where TEvent : notnull =>
        _eventBus.Publish(@event);
}
