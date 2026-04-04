using System.Collections.Concurrent;

namespace VisionService.Events;

/// <summary>Simple in-process event bus using concurrent dictionaries.</summary>
public class InProcessEventBus : IVisionEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly ILogger<InProcessEventBus> _logger;

    /// <summary>Initializes a new instance of <see cref="InProcessEventBus"/>.</summary>
    public InProcessEventBus(ILogger<InProcessEventBus> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers)) return;

        foreach (var handler in handlers.ToList())
        {
            try
            {
                if (handler is Func<TEvent, CancellationToken, Task> typedHandler)
                    await typedHandler(@event, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Event handler threw exception for event {EventType}", typeof(TEvent).Name);
            }
        }
    }

    /// <inheritdoc/>
    public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : class
    {
        _handlers.AddOrUpdate(
            typeof(TEvent),
            _ => [handler],
            (_, list) => { list.Add(handler); return list; });
    }
}
