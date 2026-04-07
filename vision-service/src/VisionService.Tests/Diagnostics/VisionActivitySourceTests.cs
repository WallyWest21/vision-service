using System.Diagnostics;
using FluentAssertions;
using VisionService.Diagnostics;
using Xunit;

namespace VisionService.Tests.Diagnostics;

public class VisionActivitySourceTests
{
    [Fact]
    public void Name_ShouldBe_VisionService()
    {
        VisionActivitySource.Name.Should().Be("VisionService");
    }

    [Fact]
    public void Source_ShouldNotBeNull()
    {
        VisionActivitySource.Source.Should().NotBeNull();
    }

    [Fact]
    public void Source_Name_ShouldMatch_ConstantName()
    {
        VisionActivitySource.Source.Name.Should().Be(VisionActivitySource.Name);
    }

    [Fact]
    public void Source_StartActivity_WhenListenerRegistered_ReturnsActivity()
    {
        // Arrange – register a listener so that the ActivitySource emits activities
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == VisionActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = VisionActivitySource.Source.StartActivity("TestOperation");

        // Assert
        activity.Should().NotBeNull();
        activities.Should().ContainSingle(a => a.OperationName == "TestOperation");
    }
}
