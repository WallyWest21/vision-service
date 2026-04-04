using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VisionService.Configuration;
using Xunit;

namespace VisionService.Tests.Configuration;

public class OptionsTests
{
    [Fact]
    public void YoloOptions_DefaultValues_AreValid()
    {
        var opts = new YoloOptions();
        opts.BaseUrl.Should().NotBeNullOrEmpty();
        opts.TimeoutSeconds.Should().BeGreaterThan(0);
        opts.MaxRetries.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void QwenVlOptions_DefaultValues_AreValid()
    {
        var opts = new QwenVlOptions();
        opts.BaseUrl.Should().NotBeNullOrEmpty();
        opts.ModelName.Should().NotBeNullOrEmpty();
        opts.MaxTokens.Should().BeGreaterThan(0);
        opts.Temperature.Should().BeInRange(0.0, 2.0);
    }

    [Fact]
    public void StorageOptions_DefaultValues_AreValid()
    {
        var opts = new StorageOptions();
        opts.ImageStoragePath.Should().NotBeNullOrEmpty();
        opts.RetentionDays.Should().BeGreaterThan(0);
        opts.MaxFileSizeMb.Should().BeGreaterThan(0);
        opts.AllowedExtensions.Should().NotBeEmpty();
    }

    [Fact]
    public void YoloOptions_BindFromConfiguration_Succeeds()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Yolo:BaseUrl"] = "http://test-yolo:7860",
                ["Yolo:TimeoutSeconds"] = "60",
                ["Yolo:MaxRetries"] = "5"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<YoloOptions>()
            .Bind(config.GetSection(YoloOptions.SectionName))
            .ValidateDataAnnotations();

        var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<YoloOptions>>().Value;

        opts.BaseUrl.Should().Be("http://test-yolo:7860");
        opts.TimeoutSeconds.Should().Be(60);
        opts.MaxRetries.Should().Be(5);
    }
}
