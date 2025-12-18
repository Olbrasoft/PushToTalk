using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Olbrasoft.PushToTalk.Service.Services;

namespace Olbrasoft.PushToTalk.Service.Tests.Services;

public class TtsControlServiceTests
{
    private readonly Mock<ILogger<TtsControlService>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly TtsControlService _service;

    public TtsControlServiceTests()
    {
        _loggerMock = new Mock<ILogger<TtsControlService>>();
        _httpHandlerMock = new Mock<HttpMessageHandler>();

        _httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };

        // Use real in-memory configuration instead of mocking
        var configValues = new Dictionary<string, string?>
        {
            { "EdgeTts:BaseUrl", "http://localhost:5555" },
            { "VirtualAssistant:BaseUrl", "http://localhost:5055" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _service = new TtsControlService(_loggerMock.Object, _httpClient, _configuration);
    }

    [Fact]
    public async Task StopAllSpeechAsync_CallsBothEndpoints()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK);

        // Act
        await _service.StopAllSpeechAsync();

        // Assert - verify both endpoints were called
        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task StopAllSpeechAsync_HandlesFailure_DoesNotThrow()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.InternalServerError);

        // Act & Assert - should not throw
        await _service.StopAllSpeechAsync();
    }

    [Fact]
    public async Task FlushQueueAsync_CallsCorrectEndpoint()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK);

        // Act
        await _service.FlushQueueAsync();

        // Assert
        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.RequestUri!.ToString().Contains("/api/tts/flush-queue")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetMuteStateAsync_WhenSuccess_ReturnsValue()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, """{"muted": true}""");

        // Act
        var result = await _service.GetMuteStateAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetMuteStateAsync_WhenFalse_ReturnsFalse()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, """{"muted": false}""");

        // Act
        var result = await _service.GetMuteStateAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetMuteStateAsync_WhenFailure_ReturnsNull()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.InternalServerError);

        // Act
        var result = await _service.GetMuteStateAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetMuteAsync_SendsCorrectPayload()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK);

        // Act
        await _service.SetMuteAsync(true);

        // Assert
        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.RequestUri!.ToString().Contains("/api/mute") &&
                r.Method == HttpMethod.Post),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SetMuteAsync_HandlesFailure_DoesNotThrow()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.InternalServerError);

        // Act & Assert - should not throw
        await _service.SetMuteAsync(true);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TtsControlService(null!, _httpClient, _configuration));
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TtsControlService(_loggerMock.Object, null!, _configuration));
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string? content = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (content != null)
        {
            response.Content = new StringContent(content);
        }

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }
}
