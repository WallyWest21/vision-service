using System.Collections.Concurrent;

namespace VisionService.Events;

/// <summary>Simple in-process event bus using concurrent dictionaries.</summary>
public class InProcessEventBus : IVisionEventBus
{
    private readonly ConcurrentDictionary<Type, ImmutableHandlerList> _handlers = new();
    private readonly ILogger<InProcessEventBus> _logger;

    /// <summary>Initializes a new instance of <see cref="InProcessEventBus"/>.</summary>
    public InProcessEventBus(ILogger<InProcessEventBus> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var entry)) return;

        foreach (var handler in entry.Handlers)
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
            _ => new ImmutableHandlerList([handler]),
            (_, existing) => existing.Add(handler));
    }

    private sealed class ImmutableHandlerList
    {
        private readonly Delegate[] _handlers;

        public ImmutableHandlerList(Delegate[] handlers) => _handlers = handlers;

        public IEnumerable<Delegate> Handlers => _handlers;

        public ImmutableHandlerList Add(Delegate handler)
        {
            var newHandlers = new Delegate[_handlers.Length + 1];
            _handlers.CopyTo(newHandlers, 0);
            newHandlers[_handlers.Length] = handler;
            return new ImmutableHandlerList(newHandlers);
        }
    }
}
