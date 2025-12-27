using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.PushToTalk.Core.Interfaces;
using Olbrasoft.PushToTalk.Service.Services;
using PushToTalk.Data.Entities;
using PushToTalk.Data.EntityFrameworkCore;

namespace PushToTalk.Service.Tests.Services;

public class LlmCorrectionServiceTests : IDisposable
{
    private readonly PushToTalkDbContext _dbContext;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<IEmailNotificationService> _mockEmailService;
    private readonly Mock<INotificationClient> _mockNotificationClient;
    private readonly Mock<ILogger<LlmCorrectionService>> _mockLogger;
    private readonly LlmCorrectionService _service;

    public LlmCorrectionServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<PushToTalkDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
            .Options;

        _dbContext = new PushToTalkDbContext(options);

        // Setup mocks
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLlmProvider.Setup(p => p.ProviderName).Returns("mistral");
        _mockLlmProvider.Setup(p => p.ModelName).Returns("mistral-large-latest");

        _mockEmailService = new Mock<IEmailNotificationService>();
        _mockNotificationClient = new Mock<INotificationClient>();
        _mockLogger = new Mock<ILogger<LlmCorrectionService>>();

        _service = new LlmCorrectionService(
            _mockLlmProvider.Object,
            _dbContext,
            _mockEmailService.Object,
            _mockNotificationClient.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CorrectTranscriptionAsync_WhenSuccessful_SavesToLlmCorrections()
    {
        // Arrange
        var transcriptionId = 1;
        var originalText = "koukám že maš success a error mesage";
        var correctedText = "koukám že máš success a error message";

        _mockLlmProvider
            .Setup(p => p.CorrectTextAsync(originalText, It.IsAny<CancellationToken>()))
            .ReturnsAsync(correctedText);

        // Act
        var result = await _service.CorrectTranscriptionAsync(transcriptionId, originalText);

        // Assert
        Assert.Equal(correctedText, result);

        // Reload from database to ensure changes are persisted
        var correction = await _dbContext.LlmCorrections.FirstOrDefaultAsync();
        Assert.NotNull(correction);
        Assert.Equal(transcriptionId, correction.WhisperTranscriptionId);
        Assert.Equal(correctedText, correction.CorrectedText);
        Assert.True(correction.DurationMs >= 0); // Can be 0 for fast mock operations

        // Verify no errors were logged
        var errorCount = await _dbContext.LlmErrors.CountAsync();
        Assert.Equal(0, errorCount);
    }

    [Fact]
    public async Task CorrectTranscriptionAsync_WhenFails_SavesToLlmErrors()
    {
        // Arrange
        var transcriptionId = 2;
        var originalText = "This is a long enough text to trigger LLM correction"; // Must be > 30 chars
        var errorMessage = "API rate limit exceeded";

        _mockLlmProvider
            .Setup(p => p.CorrectTextAsync(originalText, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(errorMessage));

        // Act
        var result = await _service.CorrectTranscriptionAsync(transcriptionId, originalText);

        // Assert - returns original text on failure
        Assert.Equal(originalText, result);

        // Reload from database to ensure changes are persisted
        var errors = await _dbContext.LlmErrors.ToListAsync();
        Assert.Single(errors);

        var error = errors[0];
        Assert.Equal(transcriptionId, error.WhisperTranscriptionId);
        Assert.Contains(errorMessage, error.ErrorMessage);
        Assert.True(error.DurationMs >= 0);

        // Verify no corrections were saved
        var correctionCount = await _dbContext.LlmCorrections.CountAsync();
        Assert.Equal(0, correctionCount);
    }

    [Fact]
    public async Task CorrectTranscriptionAsync_SkipsShortTexts_WithoutCallingLlm()
    {
        // Arrange
        var transcriptionId = 3;
        var shortText = "ahoj"; // Less than 30 characters

        // Act
        var result = await _service.CorrectTranscriptionAsync(transcriptionId, shortText);

        // Assert
        Assert.Equal(shortText, result);

        // Verify LLM was not called
        _mockLlmProvider.Verify(
            p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Verify nothing was saved to database
        Assert.Equal(0, await _dbContext.LlmCorrections.CountAsync());
        Assert.Equal(0, await _dbContext.LlmErrors.CountAsync());
    }

    [Fact]
    public async Task CorrectTranscriptionAsync_CircuitBreaker_OpensAfter3Failures()
    {
        // Arrange
        var errorMessage = "Connection timeout";
        _mockLlmProvider
            .Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(errorMessage));

        var longText = "This is a long enough text to trigger LLM correction attempts";

        // Act - First 3 failures
        await _service.CorrectTranscriptionAsync(1, longText);
        await _service.CorrectTranscriptionAsync(2, longText);
        await _service.CorrectTranscriptionAsync(3, longText);

        // Assert - Circuit should be open now
        var circuitState = await _dbContext.CircuitBreakerStates.FirstAsync();
        Assert.True(circuitState.IsOpen);
        Assert.Equal(3, circuitState.ConsecutiveFailures);
        Assert.NotNull(circuitState.OpenedAt);

        // Verify notifications were sent
        _mockEmailService.Verify(
            s => s.SendCircuitOpenedNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockNotificationClient.Verify(
            c => c.SendNotificationAsync(
                It.Is<string>(msg => msg.Contains("Circuit breaker")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify 3 errors were saved
        Assert.Equal(3, await _dbContext.LlmErrors.CountAsync());
    }

    [Fact]
    public async Task CorrectTranscriptionAsync_CircuitBreaker_SkipsWhenOpen()
    {
        // Arrange - Open circuit first
        var errorMessage = "Connection timeout";
        _mockLlmProvider
            .Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(errorMessage));

        var longText = "This is a long enough text to trigger LLM correction attempts";

        // Cause 3 failures to open circuit
        await _service.CorrectTranscriptionAsync(1, longText);
        await _service.CorrectTranscriptionAsync(2, longText);
        await _service.CorrectTranscriptionAsync(3, longText);

        // Reset mock to track new calls
        _mockLlmProvider.Invocations.Clear();

        // Act - Try correction while circuit is open
        var result = await _service.CorrectTranscriptionAsync(4, longText);

        // Assert - Should skip and return original text
        Assert.Equal(longText, result);

        // Verify LLM was NOT called (circuit is open)
        _mockLlmProvider.Verify(
            p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Still only 3 errors (no new attempt was made)
        Assert.Equal(3, await _dbContext.LlmErrors.CountAsync());
    }

    [Fact]
    public async Task CorrectTranscriptionAsync_CircuitBreaker_ClosesOnSuccess()
    {
        // Arrange - Open circuit first
        var errorMessage = "Connection timeout";
        _mockLlmProvider
            .Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(errorMessage));

        var longText = "This is a long enough text to trigger LLM correction attempts";

        // Cause 3 failures to open circuit
        await _service.CorrectTranscriptionAsync(1, longText);
        await _service.CorrectTranscriptionAsync(2, longText);
        await _service.CorrectTranscriptionAsync(3, longText);

        // Verify circuit is open
        var circuitState = await _dbContext.CircuitBreakerStates.FirstAsync();
        Assert.True(circuitState.IsOpen);

        // Wait for retry timeout (5 minutes) - simulate by modifying OpenedAt
        circuitState.OpenedAt = DateTime.UtcNow.AddMinutes(-6);
        await _dbContext.SaveChangesAsync();

        // Setup successful response
        _mockLlmProvider
            .Setup(p => p.CorrectTextAsync(longText, It.IsAny<CancellationToken>()))
            .ReturnsAsync("This is corrected text");

        // Act - Successful correction should close circuit
        var result = await _service.CorrectTranscriptionAsync(4, longText);

        // Assert
        Assert.Equal("This is corrected text", result);

        // Circuit should be closed
        var updatedState = await _dbContext.CircuitBreakerStates.FirstAsync();
        Assert.False(updatedState.IsOpen);
        Assert.Equal(0, updatedState.ConsecutiveFailures);
        Assert.Null(updatedState.OpenedAt);

        // Verify close notification was sent
        _mockEmailService.Verify(
            s => s.SendCircuitClosedNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify correction was saved
        Assert.Equal(1, await _dbContext.LlmCorrections.CountAsync());
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
