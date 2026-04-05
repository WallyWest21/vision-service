using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VisionService.Events;
using Xunit;

namespace VisionService.Tests.Events;

public class EventBusTests
{
    [Fact]
    public async Task PublishAsync_WithSubscriber_InvokesHandler()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var received = new List<DetectionCompleted>();
        bus.Subscribe<DetectionCompleted>((e, _) => { received.Add(e); return Task.CompletedTask; });

        await bus.PublishAsync(new DetectionCompleted { ProcessingTimeMs = 100 });

        received.Should().HaveCount(1);
        received[0].ProcessingTimeMs.Should().Be(100);
    }

    [Fact]
    public async Task PublishAsync_WithNoSubscribers_DoesNotThrow()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);

        await bus.Invoking(b => b.PublishAsync(new BackendUnhealthy { BackendName = "YOLO" }))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_HandlerThrows_DoesNotPropagateException()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        bus.Subscribe<BackendUnhealthy>((_, __) => throw new InvalidOperationException("test"));

        await bus.Invoking(b => b.PublishAsync(new BackendUnhealthy { BackendName = "YOLO" }))
            .Should().NotThrowAsync();
    }
}
