using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VisionService.Clients;
using VisionService.Configuration;
using Xunit;

namespace VisionService.Tests.Clients;

public class QwenVlClientTests
{
    private static QwenVlClient CreateClient(HttpClient httpClient)
    {
        var options = Options.Create(new QwenVlOptions
        {
            BaseUrl = "http://test",
            ModelName = "test-model",
            MaxTokens = 100,
            Temperature = 0.7
        });
        return new QwenVlClient(httpClient, options, NullLogger<QwenVlClient>.Instance);
    }

    [Fact]
    public async Task AskAsync_SuccessResponse_ReturnsVlResponse()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            model = "test-model",
            choices = new[] { new { message = new { content = "This is a cat." } } },
            usage = new
            {
                prompt_tokens = 10,
                completion_tokens = 5
            }
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var client = CreateClient(httpClient);

        var result = await client.AskAsync(new MemoryStream([0x01]), "What is this?");

        result.Text.Should().Be("This is a cat.");
        result.PromptTokens.Should().Be(10);
    }

    [Fact]
    public async Task AskAsync_ServerError_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "error");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var client = CreateClient(httpClient);

        await client.Invoking(c => c.AskAsync(new MemoryStream([0x01]), "What is this?"))
            .Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CaptionAsync_SuccessResponse_ReturnsVlResponse()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            model = "test-model",
            choices = new[] { new { message = new { content = "A sunny landscape." } } },
            usage = new
            {
                prompt_tokens = 8,
                completion_tokens = 6
            }
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var client = CreateClient(httpClient);

        var result = await client.CaptionAsync(new MemoryStream([0x01, 0x02]));

        result.Text.Should().Be("A sunny landscape.");
        result.Model.Should().Be("test-model");
    }

    private static readonly string[] Categories =
        ["Tools", "Machines", "Materials", "Consumables", "Software", "Workspace", "Transport", "Skills"];

    private static HttpClient ClientReturning(string assistantContent)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = "test-model",
            choices = new[] { new { message = new { content = assistantContent } } },
            usage = new
            {
                prompt_tokens = 20,
                completion_tokens = 30
            }
        });
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, body);
        return new HttpClient(handler) { BaseAddress = new Uri("http://test") };
    }

    [Fact]
    public async Task ExtractInventoryItem_ValidJson_PopulatesFieldsAndKeepsConfidentResult()
    {
        var content = JsonSerializer.Serialize(new
        {
            name = "DEWALT 20V Drill / Driver",
            category = "Tools",
            subtitle = "Cordless power tool",
            capabilities = new[] { "Drill pilot holes", "Drive screws" },
            limitations = new[] { "Not a hammer drill" },
            quantity = 1,
            visibleText = "DEWALT DCD771 20V MAX",
            confidence = 0.9
        });
        var client = CreateClient(ClientReturning(content));

        var item = await client.ExtractInventoryItemAsync(new MemoryStream([0x01]), Categories);

        item.Name.Should().Be("DEWALT 20V Drill / Driver");
        item.Category.Should().Be("Tools");
        item.Capabilities.Should().HaveCount(2);
        item.VisibleText.Should().Contain("DCD771");
        item.NeedsReview.Should().BeFalse(); // confidence >= 0.75 and category valid
    }

    [Fact]
    public async Task ExtractInventoryItem_FencedJsonAndLowConfidence_ParsesAndFlagsReview()
    {
        var inner = JsonSerializer.Serialize(new
        {
            name = "Cordless drill",
            category = "Tools",
            capabilities = new[] { "Drive screws" },
            limitations = Array.Empty<string>(),
            quantity = 1,
            confidence = 0.4
        });
        var client = CreateClient(ClientReturning("```json\n" + inner + "\n```"));

        var item = await client.ExtractInventoryItemAsync(new MemoryStream([0x01]), Categories);

        item.Name.Should().Be("Cordless drill");
        item.NeedsReview.Should().BeTrue(); // low confidence
    }

    [Fact]
    public async Task ExtractInventoryItem_CategoryOutsideAllowedList_IsClearedAndFlagged()
    {
        var content = JsonSerializer.Serialize(new
        {
            name = "Mystery gadget",
            category = "Gadgets", // not in the allowed set
            capabilities = new[] { "Unknown" },
            limitations = Array.Empty<string>(),
            quantity = 1,
            confidence = 0.95
        });
        var client = CreateClient(ClientReturning(content));

        var item = await client.ExtractInventoryItemAsync(new MemoryStream([0x01]), Categories);

        item.Category.Should().BeEmpty();
        item.NeedsReview.Should().BeTrue(); // category dropped → must confirm
    }

    [Fact]
    public async Task ExtractInventoryItem_NonJsonResponse_FallsBackToReviewDraft()
    {
        var client = CreateClient(ClientReturning("I'm sorry, I can't tell what this is."));

        var item = await client.ExtractInventoryItemAsync(new MemoryStream([0x01]), Categories);

        item.Name.Should().BeEmpty();
        item.Confidence.Should().Be(0);
        item.NeedsReview.Should().BeTrue();
        item.RawResponse.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task IsHealthyAsync_ServerDown_ReturnsFalse()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("connection refused"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var client = CreateClient(httpClient);

        var result = await client.IsHealthyAsync();

        result.Should().BeFalse();
    }
}
