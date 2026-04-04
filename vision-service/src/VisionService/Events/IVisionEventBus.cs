namespace VisionService.Events;

/// <summary>In-process event bus for vision service events.</summary>
public interface IVisionEventBus
{
    /// <summary>Publishes an event to all registered subscribers.</summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class;

    /// <summary>Subscribes a handler to events of type <typeparamref name="TEvent"/>.</summary>
    void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : class;
}
