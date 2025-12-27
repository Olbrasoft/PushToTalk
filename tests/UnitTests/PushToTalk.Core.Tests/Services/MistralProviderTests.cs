using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Olbrasoft.PushToTalk.Core.Configuration;
using Olbrasoft.PushToTalk.Core.Interfaces;
using Olbrasoft.PushToTalk.Core.Services;

namespace Olbrasoft.PushToTalk.Core.Tests.Services;

/// <summary>
/// Unit tests for MistralProvider service.
/// </summary>
public class MistralProviderTests
{
    private readonly Mock<IPromptCache> _mockPromptCache;
    private readonly Mock<ILogger<MistralProvider>> _mockLogger;
    private readonly MistralOptions _options;

    public MistralProviderTests()
    {
        _mockPromptCache = new Mock<IPromptCache>();
        _mockPromptCache.Setup(c => c.GetPrompt("MistralSystemPrompt"))
            .Returns("Test system prompt");

        _mockLogger = new Mock<ILogger<MistralProvider>>();

        _options = new MistralOptions
        {
            ApiKey = "test-api-key",
            BaseUrl = "https://api.mistral.ai",
            Model = "mistral-large-latest",
            MinTextLengthForCorrection = 21
        };
    }

    [Fact]
    public async Task CorrectTextAsync_WithShortText_ReturnsUnchangedWithoutApiCall()
    {
        // Arrange
        var shortText = "Ahoj"; // 4 characters < 21
        var httpClient = CreateMockHttpClient(shouldBeCalled: false);
        var provider = CreateProvider(httpClient);

        // Act
        var result = await provider.CorrectTextAsync(shortText);

        // Assert
        Assert.Equal(shortText, result);

        // Verify debug log was called
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Skipping LLM correction")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CorrectTextAsync_WithTextExactlyAtThreshold_ReturnsUnchangedWithoutApiCall()
    {
        // Arrange
        var textAtThreshold = "12345678901234567890"; // Exactly 20 characters (< 21)
        var httpClient = CreateMockHttpClient(shouldBeCalled: false);
        var provider = CreateProvider(httpClient);

        // Act
        var result = await provider.CorrectTextAsync(textAtThreshold);

        // Assert
        Assert.Equal(textAtThreshold, result);
        Assert.Equal(20, textAtThreshold.Length);
    }

    [Fact]
    public async Task CorrectTextAsync_WithTextJustAboveThreshold_CallsApi()
    {
        // Arrange
        var textAboveThreshold = "123456789012345678901"; // 21 characters (>= 21)
        var correctedText = "Corrected text above threshold";
        var httpClient = CreateMockHttpClient(shouldBeCalled: true, correctedText);
        var provider = CreateProvider(httpClient);

        // Act
        var result = await provider.CorrectTextAsync(textAboveThreshold);

        // Assert
        Assert.Equal(correctedText, result);
        Assert.Equal(21, textAboveThreshold.Length);
    }

    [Fact]
    public async Task CorrectTextAsync_WithLongText_CallsApi()
    {
        // Arrange
        var longText = "Toto je delší text, který by měl být poslán do Mistral API pro korekci.";
        var correctedText = "Toto je delší text, který by měl být poslán do Mistral API pro korekci.";
        var httpClient = CreateMockHttpClient(shouldBeCalled: true, correctedText);
        var provider = CreateProvider(httpClient);

        // Act
        var result = await provider.CorrectTextAsync(longText);

        // Assert
        Assert.Equal(correctedText, result);
        Assert.True(longText.Length > 21);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task CorrectTextAsync_WithCustomThreshold_RespectsConfiguration(int threshold)
    {
        // Arrange
        _options.MinTextLengthForCorrection = threshold;
        var testText = new string('x', threshold - 1); // One char below threshold
        var httpClient = CreateMockHttpClient(shouldBeCalled: false);
        var provider = CreateProvider(httpClient);

        // Act
        var result = await provider.CorrectTextAsync(testText);

        // Assert
        Assert.Equal(testText, result); // Should return unchanged
    }

    [Fact]
    public async Task CorrectTextAsync_WithEmptyText_ReturnsUnchangedWithoutApiCall()
    {
        // Arrange
        var emptyText = "";
        var httpClient = CreateMockHttpClient(shouldBeCalled: false);
        var provider = CreateProvider(httpClient);

        // Act
        var result = await provider.CorrectTextAsync(emptyText);

        // Assert
        Assert.Equal(emptyText, result);
    }

    [Fact]
    public void ProviderName_ReturnsCorrectValue()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(shouldBeCalled: false);
        var provider = CreateProvider(httpClient);

        // Act & Assert
        Assert.Equal("mistral", provider.ProviderName);
    }

    [Fact]
    public void ModelName_ReturnsConfiguredModel()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(shouldBeCalled: false);
        var provider = CreateProvider(httpClient);

        // Act & Assert
        Assert.Equal("mistral-large-latest", provider.ModelName);
    }

    private HttpClient CreateMockHttpClient(bool shouldBeCalled, string? responseText = null)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        if (shouldBeCalled)
        {
            var response = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = responseText ?? "Corrected text"
                        }
                    }
                }
            };

            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = JsonContent.Create(response)
                });
        }
        else
        {
            // Verify that SendAsync is NOT called when text is too short
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Throws(new InvalidOperationException("API should not be called for short texts"));
        }

        return new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.mistral.ai")
        };
    }

    private MistralProvider CreateProvider(HttpClient httpClient)
    {
        return new MistralProvider(
            httpClient,
            Options.Create(_options),
            _mockPromptCache.Object,
            _mockLogger.Object);
    }
}
