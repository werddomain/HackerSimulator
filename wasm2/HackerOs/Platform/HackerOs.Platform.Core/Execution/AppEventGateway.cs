using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Provides one app instance's typed event bus access: read-only pass-through to the kernel-lane
/// <see cref="IEventBus"/>, and authorized publish/subscribe through the app-lane
/// <see cref="ITopicMessageBus"/>, stamping this instance's own trusted identity as publisher/subscriber.
/// See <c>docs/adr/0038-emitter-authorized-topic-messaging.md</c>.
/// </summary>
public sealed class AppEventGateway : IAppEventGateway
{
    private readonly IEventBus _eventBus;
    private readonly ITopicMessageBus _topicBus;
    private readonly PublisherIdentity _identity;

    /// <summary>Initializes an event gateway bound to the shared event/topic buses and this app instance's identity.</summary>
    public AppEventGateway(IEventBus eventBus, ITopicMessageBus topicBus, string appId, string userId, string processId)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _topicBus = topicBus ?? throw new ArgumentNullException(nameof(topicBus));
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(processId);
        _identity = new PublisherIdentity(appId, userId, processId);
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull =>
        _eventBus.Subscribe(handler);

    /// <inheritdoc />
    public IReadOnlyList<EventDispatchFault> Publish<TEvent>(TEvent @event) where TEvent : notnull
    {
        // No CLR event type is currently app-publishable — see the interface remarks. Denied silently
        // (no delivery attempted) rather than throwing, matching the existing fault-isolation convention.
        ArgumentNullException.ThrowIfNull(@event);
        return [];
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TPayload>(TopicName topic, Action<TopicMessage<TPayload>> handler) where TPayload : notnull =>
        _topicBus.Subscribe(topic, _identity, handler);

    /// <inheritdoc />
    public ITopicChannelSubscription<TPayload> SubscribeChannel<TPayload>(TopicName topic, int? boundedCapacity = null)
        where TPayload : notnull =>
        _topicBus.SubscribeChannel<TPayload>(topic, _identity, boundedCapacity);

    /// <inheritdoc />
    public TopicPublishResult Publish<TPayload>(TopicName topic, TPayload payload) where TPayload : notnull =>
        _topicBus.Publish(topic, payload, _identity);

    /// <inheritdoc />
    public void RegisterSharedChannel(TopicName root, SharedChannelPolicy policy) =>
        _topicBus.RegisterSharedChannel(root, policy, _identity);
}
