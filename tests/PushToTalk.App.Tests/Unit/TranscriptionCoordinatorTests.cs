using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.NotificationAudio.Abstractions;
using Olbrasoft.PushToTalk.App.Services;
using Olbrasoft.PushToTalk.Core.Interfaces;
using Olbrasoft.PushToTalk.Core.Models;
using PushToTalk.Data;
using PushToTalk.Data.Entities;

namespace PushToTalk.App.Tests.Services;

/// <summary>
/// Tests for TranscriptionCoordinator ensuring correct data flow:
/// Whisper → TextFilter → Mistral → Output
/// </summary>
public class TranscriptionCoordinatorTests : IDisposable
{
    private readonly Mock<ILogger<TranscriptionCoordinator>> _loggerMock;
    private readonly Mock<ISpeechTranscriber> _transcriberMock;
    private readonly Mock<INotificationPlayer> _notificationPlayerMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ITranscriptionRepository> _repositoryMock;
    private readonly Mock<ILlmCorrectionService> _llmCorrectionMock;
    private readonly string _testSoundPath;

    public TranscriptionCoordinatorTests()
    {
        _loggerMock = new Mock<ILogger<TranscriptionCoordinator>>();
        _transcriberMock = new Mock<ISpeechTranscriber>();
        _notificationPlayerMock = new Mock<INotificationPlayer>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _repositoryMock = new Mock<ITranscriptionRepository>();
        _llmCorrectionMock = new Mock<ILlmCorrectionService>();

        // Create temporary sound file for tests
        _testSoundPath = Path.Combine(Path.GetTempPath(), $"test_sound_{Guid.NewGuid()}.mp3");
        File.WriteAllText(_testSoundPath, "fake audio data");

        // Setup service scope chain
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(p => p.GetService(typeof(ITranscriptionRepository)))
            .Returns(_repositoryMock.Object);
        _serviceProviderMock.Setup(p => p.GetService(typeof(ILlmCorrectionService)))
            .Returns(_llmCorrectionMock.Object);
    }

    public void Dispose()
    {
        if (File.Exists(_testSoundPath))
        {
            File.Delete(_testSoundPath);
        }
    }

    [Fact]
    public async Task TranscribeWithFeedbackAsync_WithValidAudio_ShouldReturnWhisperResult()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3, 4, 5 };
        var expectedResult = new TranscriptionResult("Hello World", 1.0f);

        _transcriberMock.Setup(t => t.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var coordinator = new TranscriptionCoordinator(
            _loggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: null,
            soundPath: null);

        // Act
        var result = await coordinator.TranscribeWithFeedbackAsync(audioData);

        // Assert
        Assert.Equal("Hello World", result.Text);
        Assert.True(result.Success);
        _transcriberMock.Verify(t => t.TranscribeAsync(audioData, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TranscribeWithFeedbackAsync_WithNullAudio_ShouldReturnFailure()
    {
        // Arrange
        var coordinator = new TranscriptionCoordinator(
            _loggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: null,
            soundPath: null);

        // Act
        var result = await coordinator.TranscribeWithFeedbackAsync(null!);

        // Assert
        Assert.False(result.Success);
        _transcriberMock.Verify(t => t.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranscribeWithFeedbackAsync_WithEmptyAudio_ShouldReturnFailure()
    {
        // Arrange
        var coordinator = new TranscriptionCoordinator(
            _loggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: null,
            soundPath: null);

        // Act
        var result = await coordinator.TranscribeWithFeedbackAsync(Array.Empty<byte>());

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task TranscribeWithFeedbackAsync_WithSuccessfulTranscription_ShouldSaveToDatabase()
    {
        // Arrange
        var audioData = new byte[32000]; // 1 second of audio at 16kHz mono 16-bit
        var whisperResult = new TranscriptionResult("Original Whisper text", 1.0f);
        var savedTranscription = new WhisperTranscription
        {
            Id = 123,
            TranscribedText = "Original Whisper text",
            AudioDurationMs = 1000,
            CreatedAt = DateTime.UtcNow
        };

        _transcriberMock.Setup(t => t.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(whisperResult);
        _repositoryMock.Setup(r => r.SaveAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedTranscription);
        _llmCorrectionMock.Setup(l => l.CorrectTranscriptionAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Corrected by Mistral");

        var coordinator = new TranscriptionCoordinator(
            _loggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: null,
            soundPath: null);

        // Act
        var result = await coordinator.TranscribeWithFeedbackAsync(audioData);

        // Wait for background task to complete
        await Task.Delay(100);

        // Assert
        Assert.Equal("Original Whisper text", result.Text);
        _repositoryMock.Verify(r => r.SaveAsync(
            "Original Whisper text",
            It.IsInRange(900, 1100, Moq.Range.Inclusive), // ~1000ms duration
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TranscribeWithFeedbackAsync_WithSuccessfulTranscription_ShouldCallLlmCorrection()
    {
        // Arrange
        var audioData = new byte[16000]; // 0.5 seconds
        var whisperResult = new TranscriptionResult("Original Whisper text", 1.0f);
        var savedTranscription = new WhisperTranscription { Id = 456, TranscribedText = "Original Whisper text" };

        _transcriberMock.Setup(t => t.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(whisperResult);
        _repositoryMock.Setup(r => r.SaveAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedTranscription);
        _llmCorrectionMock.Setup(l => l.CorrectTranscriptionAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Corrected text");

        var coordinator = new TranscriptionCoordinator(
            _loggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: null,
            soundPath: null);

        // Act
        await coordinator.TranscribeWithFeedbackAsync(audioData);

        // Wait for background task
        await Task.Delay(100);

        // Assert - CRITICAL TEST: Mistral should receive original Whisper text
        // TODO: This test will FAIL because we should pass FILTERED text, not original!
        _llmCorrectionMock.Verify(l => l.CorrectTranscriptionAsync(
            456,
            "Original Whisper text",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TranscribeWithFeedbackAsync_WithFailedTranscription_ShouldNotSaveToDatabase()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3 };
        var failedResult = new TranscriptionResult("Transcription failed");

        _transcriberMock.Setup(t => t.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedResult);

        var coordinator = new TranscriptionCoordinator(
            _loggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: null,
            soundPath: null);

        // Act
        await coordinator.TranscribeWithFeedbackAsync(audioData);

        // Wait a bit to ensure background task would run if it was going to
        await Task.Delay(50);

        // Assert
        _repositoryMock.Verify(r => r.SaveAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranscribeWithFeedbackAsync_WithWhitespaceText_ShouldNotSaveToDatabase()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3 };
        var whitespaceResult = new TranscriptionResult("   ", 1.0f);

        _transcriberMock.Setup(t => t.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(whitespaceResult);

        var coordinator = new TranscriptionCoordinator(
            _loggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: null,
            soundPath: null);

        // Act
        await coordinator.TranscribeWithFeedbackAsync(audioData);
        await Task.Delay(50);

        // Assert
        _repositoryMock.Verify(r => r.SaveAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranscribeWithFeedbackAsync_WithSoundPath_ShouldStartAndStopSoundLoop()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3 };
        var result = new TranscriptionResult("Test", 1.0f);
        var playCallCount = 0;

        _transcriberMock.Setup(t => t.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        _notificationPlayerMock.Setup(p => p.PlayAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => playCallCount++)
            .Returns(Task.CompletedTask);

        var coordinator = new TranscriptionCoordinator(
            _loggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: null,
            soundPath: _testSoundPath);

        // Act
        await coordinator.TranscribeWithFeedbackAsync(audioData);

        // Assert - Sound should have been played at least once
        Assert.True(playCallCount > 0, "Sound loop should have played at least once");
        _notificationPlayerMock.Verify(p => p.PlayAsync(_testSoundPath, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task TranscribeWithFeedbackAsync_WithoutSoundPath_ShouldNotPlaySound()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3 };
        var result = new TranscriptionResult("Test", 1.0f);

        _transcriberMock.Setup(t => t.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var coordinator = new TranscriptionCoordinator(
            _loggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            soundPath: null);

        // Act
        await coordinator.TranscribeWithFeedbackAsync(audioData);

        // Assert
        _notificationPlayerMock.Verify(p => p.PlayAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranscribeWithFeedbackAsync_WhenDatabaseSaveFails_ShouldStillReturnResult()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3 };
        var result = new TranscriptionResult("Test", 1.0f);

        _transcriberMock.Setup(t => t.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        _repositoryMock.Setup(r => r.SaveAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var coordinator = new TranscriptionCoordinator(
            _loggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: null,
            soundPath: null);

        // Act
        var actualResult = await coordinator.TranscribeWithFeedbackAsync(audioData);

        // Wait for background task error handling
        await Task.Delay(100);

        // Assert - Should still return Whisper result even if DB save fails
        Assert.Equal("Test", actualResult.Text);
        Assert.True(actualResult.Success);
    }

    [Fact]
    public async Task TranscribeWithFeedbackAsync_WhenLlmCorrectionFails_ShouldStillSaveTranscription()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3 };
        var result = new TranscriptionResult("Test text that is long enough for LLM correction", 1.0f);
        var savedTranscription = new WhisperTranscription { Id = 789, TranscribedText = "Test text that is long enough for LLM correction" };

        _transcriberMock.Setup(t => t.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        _repositoryMock.Setup(r => r.SaveAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedTranscription);
        _llmCorrectionMock.Setup(l => l.CorrectTranscriptionAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM API error"));

        var coordinator = new TranscriptionCoordinator(
            _loggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: null,
            soundPath: null);

        // Act
        await coordinator.TranscribeWithFeedbackAsync(audioData);
        await Task.Delay(100);

        // Assert - Database save should succeed even if LLM fails
        _repositoryMock.Verify(r => r.SaveAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Dispose_ShouldStopSoundLoop()
    {
        // Arrange
        var coordinator = new TranscriptionCoordinator(
            _loggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: null,
            soundPath: _testSoundPath);

        // Act & Assert - Should not throw
        coordinator.Dispose();
    }

    [Fact]
    public void Dispose_MultipleCalls_ShouldNotThrow()
    {
        // Arrange
        var coordinator = new TranscriptionCoordinator(
            _loggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: null,
            soundPath: null);

        // Act & Assert
        coordinator.Dispose();
        coordinator.Dispose(); // Should not throw
    }
}
