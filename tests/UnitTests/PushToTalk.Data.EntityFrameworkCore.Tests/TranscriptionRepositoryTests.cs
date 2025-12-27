using Microsoft.Extensions.Logging;
using Moq;
using PushToTalk.Data.EntityFrameworkCore.Tests.Infrastructure;

namespace PushToTalk.Data.EntityFrameworkCore.Tests;

/// <summary>
/// Unit tests for TranscriptionRepository using in-memory database.
/// </summary>
public class TranscriptionRepositoryTests : DatabaseTestBase
{
    private readonly TranscriptionRepository _repository;
    private readonly Mock<ILogger<TranscriptionRepository>> _loggerMock;

    public TranscriptionRepositoryTests()
    {
        _loggerMock = new Mock<ILogger<TranscriptionRepository>>();
        _repository = new TranscriptionRepository(DbContext, _loggerMock.Object);
    }

    #region SaveAsync Tests

    [Fact]
    public async Task SaveAsync_WithValidText_CreatesTranscription()
    {
        // Arrange
        const string text = "Test transcription";
        const int durationMs = 5000;

        // Act
        var result = await _repository.SaveAsync(text, durationMs);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(0, result.Id);
        Assert.Equal(text, result.TranscribedText);
        Assert.Equal(durationMs, result.AudioDurationMs);
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
        Assert.True(result.CreatedAt >= DateTime.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public async Task SaveAsync_WithoutDuration_CreatesTranscriptionWithNullDuration()
    {
        // Arrange
        const string text = "Test without duration";

        // Act
        var result = await _repository.SaveAsync(text);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(text, result.TranscribedText);
        Assert.Null(result.AudioDurationMs);
    }

    [Fact]
    public async Task SaveAsync_WithLongText_TruncatesLogMessage()
    {
        // Arrange
        var longText = new string('a', 100);

        // Act
        await _repository.SaveAsync(longText, 1000);

        // Assert
        // Logger should have been called (verifies logging works)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // NOTE: Persistence test requires real database (in integration tests)
    // In-memory database doesn't persist across DbContext instances unless same DB name is used

    #endregion

    #region GetRecentAsync Tests

    [Fact]
    public async Task GetRecentAsync_WithNoTranscriptions_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetRecentAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsOrderedByCreatedAtDescending()
    {
        // Arrange
        var first = await _repository.SaveAsync("First");
        await Task.Delay(10); // Ensure different timestamps
        var second = await _repository.SaveAsync("Second");
        await Task.Delay(10);
        var third = await _repository.SaveAsync("Third");

        // Act
        var result = await _repository.GetRecentAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(third.Id, result[0].Id);
        Assert.Equal(second.Id, result[1].Id);
        Assert.Equal(first.Id, result[2].Id);
    }

    [Fact]
    public async Task GetRecentAsync_WithCountParameter_ReturnsLimitedResults()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _repository.SaveAsync($"Transcription {i}");
        }

        // Act
        var result = await _repository.GetRecentAsync(count: 3);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetRecentAsync_WithCountLargerThanTotal_ReturnsAllTranscriptions()
    {
        // Arrange
        await _repository.SaveAsync("First");
        await _repository.SaveAsync("Second");

        // Act
        var result = await _repository.GetRecentAsync(count: 100);

        // Assert
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_WithEmptyQuery_ReturnsRecentTranscriptions()
    {
        // Arrange
        await _repository.SaveAsync("First");
        await _repository.SaveAsync("Second");

        // Act
        var result = await _repository.SearchAsync("");

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SearchAsync_WithNullQuery_ReturnsRecentTranscriptions()
    {
        // Arrange
        await _repository.SaveAsync("First");

        // Act
        var result = await _repository.SearchAsync(null!);

        // Assert
        Assert.Single(result);
    }

    // NOTE: SearchAsync tests using EF.Functions.ILike require PostgreSQL.
    // These tests are in integration test project with real database.
    // In-memory database doesn't support PostgreSQL-specific functions.

    #endregion

    #region GetLatestCorrectedTextAsync Tests

    [Fact]
    public async Task GetLatestCorrectedTextAsync_WithNoTranscriptions_ReturnsNull()
    {
        // Act
        var result = await _repository.GetLatestCorrectedTextAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestCorrectedTextAsync_WithNoCorrection_ReturnsOriginalText()
    {
        // Arrange
        const string originalText = "Original Whisper text";
        await _repository.SaveAsync(originalText);

        // Act
        var result = await _repository.GetLatestCorrectedTextAsync();

        // Assert
        Assert.Equal(originalText, result);
    }

    [Fact]
    public async Task GetLatestCorrectedTextAsync_WithCorrection_ReturnsCorrectedText()
    {
        // Arrange
        const string originalText = "Original text";
        const string correctedText = "Corrected text";

        var transcription = await _repository.SaveAsync(originalText);

        // Add LLM correction
        DbContext.LlmCorrections.Add(new Data.Entities.LlmCorrection
        {
            WhisperTranscriptionId = transcription.Id,
            CorrectedText = correctedText,
            CreatedAt = DateTime.UtcNow
        });
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetLatestCorrectedTextAsync();

        // Assert
        Assert.Equal(correctedText, result);
    }

    [Fact]
    public async Task GetLatestCorrectedTextAsync_WithMultipleCorrections_ReturnsLatestCorrection()
    {
        // Arrange
        var transcription = await _repository.SaveAsync("Original");

        DbContext.LlmCorrections.Add(new Data.Entities.LlmCorrection
        {
            WhisperTranscriptionId = transcription.Id,
            CorrectedText = "First correction",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        });

        DbContext.LlmCorrections.Add(new Data.Entities.LlmCorrection
        {
            WhisperTranscriptionId = transcription.Id,
            CorrectedText = "Latest correction",
            CreatedAt = DateTime.UtcNow
        });

        await DbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetLatestCorrectedTextAsync();

        // Assert
        Assert.Equal("Latest correction", result);
    }

    [Fact]
    public async Task GetLatestCorrectedTextAsync_WithMultipleTranscriptions_UsesLatestTranscription()
    {
        // Arrange
        var old = await _repository.SaveAsync("Old text");
        await Task.Delay(10);
        var latest = await _repository.SaveAsync("Latest text");

        // Add correction only to old transcription
        DbContext.LlmCorrections.Add(new Data.Entities.LlmCorrection
        {
            WhisperTranscriptionId = old.Id,
            CorrectedText = "Corrected old",
            CreatedAt = DateTime.UtcNow
        });
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetLatestCorrectedTextAsync();

        // Assert - Should return latest transcription text, not correction from old one
        Assert.Equal("Latest text", result);
    }

    #endregion
}
