using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.Testing.Xunit.Attributes;
using PushToTalk.App.IntegrationTests.Helpers;
using PushToTalk.Data;
using PushToTalk.Data.Entities;
using PushToTalk.Data.EntityFrameworkCore;
using Xunit;

namespace PushToTalk.App.IntegrationTests;

/// <summary>
/// Integration tests that use REAL PostgreSQL database (push_to_talk_tests).
/// These tests verify that database reading/writing works correctly with LLM corrections.
/// Uses [SkipOnCIFact] to automatically skip on GitHub Actions, Azure DevOps, etc.
/// </summary>
public class DatabaseIntegrationTests : IAsyncLifetime
{
    private PushToTalkDbContext _context = null!;
    private ITranscriptionRepository _repository = null!;
    private readonly Mock<ILogger<TranscriptionRepository>> _loggerMock = new();

    public async Task InitializeAsync()
    {
        // CRITICAL: Skip initialization on CI - prevents database connection attempts
        // Tests won't run anyway due to [SkipOnCIFact], but class still gets instantiated
        if (IsRunningOnCI())
        {
            return;
        }

        // Verify we're using TEST database (safety check)
        TestDbContextFactory.VerifyTestDatabase();

        // Recreate database (drops all data and applies migrations)
        await TestDbContextFactory.RecreateDatabase();

        // Create DbContext and repository for tests
        _context = TestDbContextFactory.Create();
        _repository = new TranscriptionRepository(_context, _loggerMock.Object);
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
        }
    }

    private static bool IsRunningOnCI()
    {
        // Check common CI environment variables
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")); // Azure DevOps
    }

    [SkipOnCIFact]
    public async Task SaveAsync_ShouldSaveWhisperTranscription_AndReturnId()
    {
        // Arrange
        var whisperText = "test whisper text";
        var durationMs = 1000;

        // Act
        var result = await _repository.SaveAsync(whisperText, durationMs);

        // Assert
        Assert.True(result.Id > 0);
        Assert.Equal(whisperText, result.TranscribedText);
        Assert.Equal(durationMs, result.AudioDurationMs);
    }

    [SkipOnCIFact]
    public async Task LlmCorrection_ShouldBeSavedSeparately_WithForeignKey()
    {
        // Arrange - Save Whisper transcription
        var whisperText = "vybav prasátka";
        var transcription = await _repository.SaveAsync(whisperText, 1000);

        // Act - Save LLM correction (simulating LlmCorrectionService)
        var mistralCorrectedText = "Vybav PraG";
        var correction = new LlmCorrection
        {
            WhisperTranscriptionId = transcription.Id,
            CorrectedText = mistralCorrectedText,
            DurationMs = 500,
            CreatedAt = DateTime.UtcNow
        };

        _context.LlmCorrections.Add(correction);
        await _context.SaveChangesAsync();

        // Assert - Verify correction was saved
        var savedCorrection = await _context.LlmCorrections
            .FirstOrDefaultAsync(c => c.WhisperTranscriptionId == transcription.Id);

        Assert.NotNull(savedCorrection);
        Assert.Equal(mistralCorrectedText, savedCorrection.CorrectedText);
        Assert.Equal(transcription.Id, savedCorrection.WhisperTranscriptionId);
    }

    [SkipOnCIFact]
    public async Task GetLatestCorrectedTextAsync_ShouldReturnCorrectedText_NotOriginalWhisper()
    {
        // Arrange - Simulate complete transcription flow with LLM correction
        var whisperText = "tím rýtmií se myslel soubor, vlastně rýtmií, aby si ho přečetl";
        var mistralCorrectedText = "Tím rytmem se myslel soubor, vlastně README, aby sis ho přečetl";

        // Save Whisper transcription
        var transcription = await _repository.SaveAsync(whisperText, 1500);

        // Save Mistral correction
        var correction = new LlmCorrection
        {
            WhisperTranscriptionId = transcription.Id,
            CorrectedText = mistralCorrectedText,
            DurationMs = 800,
            CreatedAt = DateTime.UtcNow
        };

        _context.LlmCorrections.Add(correction);
        await _context.SaveChangesAsync();

        // Act - Read latest corrected text from database
        var latestText = await _repository.GetLatestCorrectedTextAsync();

        // Assert - CRITICAL: Must return Mistral-corrected text, NOT original Whisper
        Assert.Equal(mistralCorrectedText, latestText);
        Assert.NotEqual(whisperText, latestText);
    }

    [SkipOnCIFact]
    public async Task GetLatestCorrectedTextAsync_WhenNoCorrectionExists_ShouldReturnOriginalWhisperText()
    {
        // Arrange - Save transcription WITHOUT LLM correction (circuit breaker open, etc.)
        var whisperText = "uncorrected whisper text";
        await _repository.SaveAsync(whisperText, 1000);

        // Act - Read latest text
        var latestText = await _repository.GetLatestCorrectedTextAsync();

        // Assert - Should return original Whisper text when no correction exists
        Assert.Equal(whisperText, latestText);
    }

    [SkipOnCIFact]
    public async Task GetLatestCorrectedTextAsync_WithMultipleCorrections_ShouldReturnMostRecent()
    {
        // Arrange - Save transcription
        var whisperText = "original text";
        var transcription = await _repository.SaveAsync(whisperText, 1000);

        // Save first correction
        var firstCorrection = new LlmCorrection
        {
            WhisperTranscriptionId = transcription.Id,
            CorrectedText = "first correction",
            DurationMs = 500,
            CreatedAt = DateTime.UtcNow
        };

        _context.LlmCorrections.Add(firstCorrection);
        await _context.SaveChangesAsync();

        await Task.Delay(10); // Ensure different timestamps

        // Save second correction (retry after circuit breaker closed)
        var secondCorrection = new LlmCorrection
        {
            WhisperTranscriptionId = transcription.Id,
            CorrectedText = "LATEST CORRECTION",
            DurationMs = 500,
            CreatedAt = DateTime.UtcNow
        };

        _context.LlmCorrections.Add(secondCorrection);
        await _context.SaveChangesAsync();

        // Act
        var latestText = await _repository.GetLatestCorrectedTextAsync();

        // Assert - Should return the most recent correction
        Assert.Equal("LATEST CORRECTION", latestText);
    }

    [SkipOnCIFact]
    public async Task CompleteFlow_RealWorldExample_ShouldReturnCorrectText()
    {
        // Arrange - Real-world example from user bug report
        var whisperOriginal = "tím jsem měl na mysli soubor read me jako přečti mě";
        var mistralCorrected = "Měl jsem na mysli soubor README, tedy \"Přečti mě\".";

        // Act - Simulate complete transcription flow
        var transcription = await _repository.SaveAsync(whisperOriginal, 2000);

        var correction = new LlmCorrection
        {
            WhisperTranscriptionId = transcription.Id,
            CorrectedText = mistralCorrected,
            DurationMs = 750,
            CreatedAt = DateTime.UtcNow
        };

        _context.LlmCorrections.Add(correction);
        await _context.SaveChangesAsync();

        // Read back from database
        var retrievedText = await _repository.GetLatestCorrectedTextAsync();

        // Assert - CRITICAL BUG FIX: User MUST receive Mistral text, not Whisper text
        Assert.Equal(mistralCorrected, retrievedText);
        Assert.NotEqual(whisperOriginal, retrievedText);
    }

    [SkipOnCIFact]
    public async Task MultipleTranscriptions_GetLatest_ShouldReturnMostRecentTranscription()
    {
        // Arrange - Create multiple transcriptions with corrections
        var first = await _repository.SaveAsync("first transcription", 1000);
        _context.LlmCorrections.Add(new LlmCorrection
        {
            WhisperTranscriptionId = first.Id,
            CorrectedText = "first corrected",
            DurationMs = 500
        });
        await _context.SaveChangesAsync();
        await Task.Delay(10);

        var second = await _repository.SaveAsync("second transcription", 1000);
        _context.LlmCorrections.Add(new LlmCorrection
        {
            WhisperTranscriptionId = second.Id,
            CorrectedText = "second corrected",
            DurationMs = 500
        });
        await _context.SaveChangesAsync();
        await Task.Delay(10);

        var third = await _repository.SaveAsync("third transcription", 1000);
        _context.LlmCorrections.Add(new LlmCorrection
        {
            WhisperTranscriptionId = third.Id,
            CorrectedText = "LATEST TRANSCRIPTION CORRECTED",
            DurationMs = 500
        });
        await _context.SaveChangesAsync();

        // Act
        var latestText = await _repository.GetLatestCorrectedTextAsync();

        // Assert - Should return correction from the most recent transcription
        Assert.Equal("LATEST TRANSCRIPTION CORRECTED", latestText);
    }

    [SkipOnCIFact]
    public async Task CorrectTranscriptionAsync_ReturnValue_ShouldMatchDatabaseValue()
    {
        // CRITICAL TEST: Verify that what user receives (return value)
        // is EXACTLY what's stored in database (via GetLatestCorrectedTextAsync)

        // Arrange - Create mock LLM provider and service
        var mockLlmProvider = new Mock<Olbrasoft.PushToTalk.Core.Interfaces.ILlmProvider>();
        var mockEmailService = new Mock<Olbrasoft.PushToTalk.Core.Interfaces.IEmailNotificationService>();
        var mockNotificationClient = new Mock<Olbrasoft.PushToTalk.Core.Interfaces.INotificationClient>();
        var mockLogger = new Mock<ILogger<Olbrasoft.PushToTalk.Service.Services.LlmCorrectionService>>();

        var mistralCorrectedText = "This is the EXACT text that Mistral returned and should be in database";

        mockLlmProvider
            .Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mistralCorrectedText);

        var llmService = new Olbrasoft.PushToTalk.Service.Services.LlmCorrectionService(
            mockLlmProvider.Object,
            _context,
            mockEmailService.Object,
            mockNotificationClient.Object,
            mockLogger.Object);

        // Save Whisper transcription first
        var whisperText = "This is original whisper text that needs correction";
        var transcription = await _repository.SaveAsync(whisperText, 1500);

        // Act - Call LLM correction service (simulates what TranscriptionCoordinator does)
        var returnedText = await llmService.CorrectTranscriptionAsync(
            transcription.Id,
            whisperText,
            CancellationToken.None);

        // Read from database using repository method (what we use to retrieve text later)
        var databaseText = await _repository.GetLatestCorrectedTextAsync();

        // Assert - CRITICAL: Return value MUST equal database value
        Assert.Equal(mistralCorrectedText, returnedText);
        Assert.Equal(mistralCorrectedText, databaseText);
        Assert.Equal(returnedText, databaseText); // What user gets = what's in DB

        // Verify LLM service logged success
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("LLM correction succeeded")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
