using HackerOs.Simulation.Abstractions.Events;

namespace HackerOs.Platform.Core.Events;

/// <summary>
/// In-memory <see cref="IEventBus"/> implementation used by every headless and Blazor host.
/// </summary>
public sealed class InMemoryEventBus : IEventBus
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Type, List<Subscription>> _subscriptionsByEventType = [];

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);

        Subscription subscription = new(@event => handler((TEvent)@event));

        lock (_gate)
        {
            if (!_subscriptionsByEventType.TryGetValue(typeof(TEvent), out List<Subscription>? subscriptions))
            {
                subscriptions = [];
                _subscriptionsByEventType[typeof(TEvent)] = subscriptions;
            }

            subscriptions.Add(subscription);
        }

        return new Unsubscriber(this, typeof(TEvent), subscription);
    }

    /// <inheritdoc />
    public IReadOnlyList<EventDispatchFault> Publish<TEvent>(TEvent @event) where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(@event);

        List<Subscription> snapshot;
        lock (_gate)
        {
            if (!_subscriptionsByEventType.TryGetValue(typeof(TEvent), out List<Subscription>? subscriptions))
            {
                return [];
            }

            snapshot = [.. subscriptions];
        }

        List<EventDispatchFault>? faults = null;
        foreach (Subscription subscription in snapshot)
        {
            try
            {
                subscription.Invoke(@event);
            }
            catch (Exception exception)
            {
                faults ??= [];
                faults.Add(new EventDispatchFault(typeof(TEvent), exception));
            }
        }

        return faults ?? (IReadOnlyList<EventDispatchFault>)[];
    }

    private void Unsubscribe(Type eventType, Subscription subscription)
    {
        lock (_gate)
        {
            if (_subscriptionsByEventType.TryGetValue(eventType, out List<Subscription>? subscriptions))
            {
                subscriptions.Remove(subscription);
            }
        }
    }

    private sealed class Subscription(Action<object> invoke)
    {
        public void Invoke(object @event) => invoke(@event);
    }

    private sealed class Unsubscriber(InMemoryEventBus bus, Type eventType, Subscription subscription) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            bus.Unsubscribe(eventType, subscription);
        }
    }
}
