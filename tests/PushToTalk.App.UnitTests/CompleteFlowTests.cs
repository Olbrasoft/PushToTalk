using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.NotificationAudio.Abstractions;
using Olbrasoft.PushToTalk.App;
using Olbrasoft.PushToTalk.App.Services;
using Olbrasoft.PushToTalk.Core.Interfaces;
using Olbrasoft.PushToTalk.Core.Models;
using Olbrasoft.PushToTalk.TextInput;
using PushToTalk.Data;
using PushToTalk.Data.Entities;

namespace PushToTalk.App.Tests;

/// <summary>
/// Unit tests for complete transcription flow:
/// Whisper → TextFilter (DB corrections) → Mistral (LLM corrections) → Output
///
/// These tests verify the CRITICAL bugs were fixed:
/// 1. Mistral MUST receive TextFilter-corrected text, NOT original Whisper text
/// 2. User MUST receive Mistral-corrected text, NOT original Whisper text
/// 3. Sound/icon MUST play until AFTER Mistral completes, NOT just after Whisper
/// </summary>
public class CompleteFlowTests : IDisposable
{
    private readonly Mock<ILogger<DictationService>> _dictationLoggerMock;
    private readonly Mock<ILogger<TranscriptionCoordinator>> _coordinatorLoggerMock;
    private readonly Mock<ILogger<TextOutputHandler>> _outputLoggerMock;
    private readonly Mock<ILogger<TextFilter>> _filterLoggerMock;
    private readonly Mock<IKeyboardMonitor> _keyboardMonitorMock;
    private readonly Mock<IKeySimulator> _keySimulatorMock;
    private readonly Mock<IAudioRecorder> _audioRecorderMock;
    private readonly Mock<ISpeechTranscriber> _transcriberMock;
    private readonly Mock<INotificationPlayer> _notificationPlayerMock;
    private readonly Mock<ITextTyper> _textTyperMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ITranscriptionRepository> _repositoryMock;
    private readonly Mock<ILlmCorrectionService> _llmCorrectionMock;
    private readonly Mock<ITranscriptionCorrectionRepository> _correctionRepositoryMock;

    private readonly string _testSoundPath;
    private readonly string _testConfigPath;

    public CompleteFlowTests()
    {
        _dictationLoggerMock = new Mock<ILogger<DictationService>>();
        _coordinatorLoggerMock = new Mock<ILogger<TranscriptionCoordinator>>();
        _outputLoggerMock = new Mock<ILogger<TextOutputHandler>>();
        _filterLoggerMock = new Mock<ILogger<TextFilter>>();
        _keyboardMonitorMock = new Mock<IKeyboardMonitor>();
        _keySimulatorMock = new Mock<IKeySimulator>();
        _audioRecorderMock = new Mock<IAudioRecorder>();
        _transcriberMock = new Mock<ISpeechTranscriber>();
        _notificationPlayerMock = new Mock<INotificationPlayer>();
        _textTyperMock = new Mock<ITextTyper>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _repositoryMock = new Mock<ITranscriptionRepository>();
        _llmCorrectionMock = new Mock<ILlmCorrectionService>();
        _correctionRepositoryMock = new Mock<ITranscriptionCorrectionRepository>();

        // Create temporary files
        _testSoundPath = Path.Combine(Path.GetTempPath(), $"test_sound_{Guid.NewGuid()}.mp3");
        File.WriteAllText(_testSoundPath, "fake audio");

        _testConfigPath = Path.Combine(Path.GetTempPath(), $"test_filter_{Guid.NewGuid()}.json");

        // Setup service scope chain
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(p => p.GetService(typeof(ITranscriptionRepository)))
            .Returns(_repositoryMock.Object);
        _serviceProviderMock.Setup(p => p.GetService(typeof(ILlmCorrectionService)))
            .Returns(_llmCorrectionMock.Object);
        _serviceProviderMock.Setup(p => p.GetService(typeof(ITranscriptionCorrectionRepository)))
            .Returns(_correctionRepositoryMock.Object);

        // Default: CapsLock OFF
        _keyboardMonitorMock.Setup(k => k.IsCapsLockOn()).Returns(false);
    }

    public void Dispose()
    {
        if (File.Exists(_testSoundPath))
            File.Delete(_testSoundPath);
        if (File.Exists(_testConfigPath))
            File.Delete(_testConfigPath);
    }

    /// <summary>
    /// CRITICAL BUG TEST #1: Mistral must receive TextFilter-corrected text, not original Whisper text
    /// </summary>
    [Fact]
    public async Task CompleteFlow_MistralShouldReceiveFilteredText_NotOriginalWhisperText()
    {
        // Arrange - Setup database corrections
        var dbCorrections = new List<TranscriptionCorrection>
        {
            new TranscriptionCorrection
            {
                Id = 1,
                IncorrectText = "prasátka",
                CorrectText = "PraG",
                Priority = 100,
                CaseSensitive = false,
                IsActive = true
            }
        };

        _correctionRepositoryMock.Setup(r => r.GetActiveCorrectionsAsync())
            .ReturnsAsync(dbCorrections);

        // Whisper returns text with error
        var whisperText = "vybav prasátka";
        var whisperResult = new TranscriptionResult(whisperText, 1.0f);

        // Expected: TextFilter should correct "prasátka" → "PraG"
        var expectedFilteredText = "vybav PraG";

        var savedTranscription = new WhisperTranscription { Id = 100, TranscribedText = whisperText };

        _transcriberMock.Setup(t => t.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(whisperResult);
        _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedTranscription);
        _llmCorrectionMock.Setup(l => l.CorrectTranscriptionAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mistral corrected text");

        // Mock TextFilter to apply database corrections
        var textFilterMock = new Mock<ITextFilter>();
        textFilterMock.Setup(f => f.Apply(whisperText))
            .Returns(expectedFilteredText); // "prasátka" → "PraG"

        var coordinator = new TranscriptionCoordinator(
            _coordinatorLoggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: textFilterMock.Object,
            soundPath: null);

        // Act
        var audioData = new byte[1000];
        await coordinator.TranscribeWithFeedbackAsync(audioData);

        // Wait for background task to complete
        await Task.Delay(200);

        // Assert - THIS TEST WILL FAIL!
        // Bug: Current code passes original "vybav prasátka" to Mistral
        // Fix: Should pass filtered "vybav PraG" to Mistral
        _llmCorrectionMock.Verify(l => l.CorrectTranscriptionAsync(
            It.IsAny<int>(),
            expectedFilteredText, // Should receive FILTERED text
            It.IsAny<CancellationToken>()), Times.Once,
            $"Mistral should receive filtered text '{expectedFilteredText}', not original Whisper text '{whisperText}'");
    }

    /// <summary>
    /// CRITICAL BUG TEST #2: User must receive Mistral-corrected text, not original Whisper text
    /// </summary>
    [Fact]
    public async Task CompleteFlow_UserShouldReceiveMistralText_NotOriginalWhisperText()
    {
        // Arrange
        var whisperText = "prosím tě otevři Visual Studio kód";
        var whisperResult = new TranscriptionResult(whisperText, 1.0f);
        var savedTranscription = new WhisperTranscription { Id = 200, TranscribedText = whisperText };
        var mistralCorrectedText = "prosím tě, otevři Visual Studio Code";

        var audioData = new byte[1000];
        string? actualTypedText = null;

        _audioRecorderMock.Setup(r => r.StartRecordingAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audioRecorderMock.Setup(r => r.StopRecordingAsync())
            .Returns(Task.CompletedTask);
        _audioRecorderMock.Setup(r => r.GetRecordedData())
            .Returns(audioData);
        _transcriberMock.Setup(t => t.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(whisperResult);
        _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedTranscription);
        _llmCorrectionMock.Setup(l => l.CorrectTranscriptionAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mistralCorrectedText);
        _textTyperMock.Setup(t => t.TypeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((text, ct) => actualTypedText = text)
            .Returns(Task.CompletedTask);

        var coordinator = new TranscriptionCoordinator(
            _coordinatorLoggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: null,
            soundPath: null);

        var textOutputHandler = new TextOutputHandler(
            _outputLoggerMock.Object,
            _textTyperMock.Object,
            textFilter: null);

        var dictationService = new DictationService(
            _dictationLoggerMock.Object,
            _keyboardMonitorMock.Object,
            _keySimulatorMock.Object,
            _audioRecorderMock.Object,
            coordinator,
            textOutputHandler);

        // Act
        await dictationService.StartDictationAsync();
        await dictationService.StopDictationAsync();

        // Wait for background Mistral correction
        await Task.Delay(200);

        // Assert - THIS TEST WILL FAIL!
        // Bug: Current code types original Whisper text "prosím tě otevři Visual Studio kód"
        // Fix: Should type Mistral-corrected text "prosím tě, otevři Visual Studio Code"
        Assert.Equal(mistralCorrectedText, actualTypedText);
        Assert.NotEqual(whisperText, actualTypedText);
    }

    /// <summary>
    /// CRITICAL BUG TEST #3: Sound loop must play until AFTER Mistral completes, not just until Whisper completes
    /// </summary>
    [Fact]
    public async Task CompleteFlow_SoundLoopShouldPlayUntilAfterMistral_NotJustWhisper()
    {
        // Arrange
        var whisperText = "dlouhý text který potřebuje LLM korekci";
        var whisperResult = new TranscriptionResult(whisperText, 1.0f);
        var savedTranscription = new WhisperTranscription { Id = 300, TranscribedText = whisperText };
        var mistralText = "dlouhý text, který potřebuje LLM korekci";

        var audioData = new byte[32000]; // 1 second
        var soundPlayCount = 0;
        var soundStoppedAt = DateTime.MinValue;
        var whisperCompletedAt = DateTime.MinValue;
        var mistralCompletedAt = DateTime.MinValue;

        _transcriberMock.Setup(t => t.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                Thread.Sleep(100); // Simulate Whisper processing time
            })
            .ReturnsAsync(() =>
            {
                whisperCompletedAt = DateTime.UtcNow;
                return whisperResult;
            });

        _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedTranscription);

        _llmCorrectionMock.Setup(l => l.CorrectTranscriptionAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(200); // Simulate Mistral API call time
                mistralCompletedAt = DateTime.UtcNow;
                return mistralText;
            });

        _notificationPlayerMock.Setup(p => p.PlayAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                soundPlayCount++;
                soundStoppedAt = DateTime.UtcNow; // Update on EVERY call to track LAST call time
            })
            .Returns(Task.Delay(50)); // Each sound loop iteration takes 50ms

        var coordinator = new TranscriptionCoordinator(
            _coordinatorLoggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: null,
            soundPath: _testSoundPath);

        // Act
        await coordinator.TranscribeWithFeedbackAsync(audioData);

        // Wait for background Mistral task
        await Task.Delay(500);

        // Assert - THIS TEST WILL FAIL!
        // Bug: Sound stops at whisperCompletedAt (in finally block)
        // Fix: Sound should stop AFTER mistralCompletedAt
        Assert.True(soundStoppedAt > whisperCompletedAt,
            $"Sound stopped at {soundStoppedAt}, but Whisper completed at {whisperCompletedAt}. Sound should continue playing!");

        // Ideally, sound should play until after Mistral
        // This assertion will FAIL with current code because sound stops in finally block
        Assert.True(soundStoppedAt >= mistralCompletedAt,
            $"Sound stopped at {soundStoppedAt}, but Mistral completed at {mistralCompletedAt}. Sound should play until AFTER Mistral!");
    }

    [Fact]
    public async Task CompleteFlow_WithFileBasedCorrections_ShouldApplyBeforeMistral()
    {
        // Arrange - Create file-based corrections
        var config = """{"replace": {"kód": "code", "otevři": "otevři"}}""";
        File.WriteAllText(_testConfigPath, config);

        var whisperText = "otevři Visual Studio kód";
        var whisperResult = new TranscriptionResult(whisperText, 1.0f);
        var savedTranscription = new WhisperTranscription { Id = 400, TranscribedText = whisperText };
        var expectedFilteredText = "otevři Visual Studio code"; // "kód" → "code"

        _transcriberMock.Setup(t => t.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(whisperResult);
        _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedTranscription);
        _llmCorrectionMock.Setup(l => l.CorrectTranscriptionAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mistral corrected");

        // Mock TextFilter to apply file-based corrections
        var textFilterMock = new Mock<ITextFilter>();
        textFilterMock.Setup(f => f.Apply(whisperText))
            .Returns(expectedFilteredText); // "kód" → "code"

        var coordinator = new TranscriptionCoordinator(
            _coordinatorLoggerMock.Object,
            _transcriberMock.Object,
            _notificationPlayerMock.Object,
            _scopeFactoryMock.Object,
            textFilter: textFilterMock.Object,
            soundPath: null);

        // Act
        var audioData = new byte[1000];
        await coordinator.TranscribeWithFeedbackAsync(audioData);
        await Task.Delay(200);

        // Assert - Mistral should receive file-corrected text
        _llmCorrectionMock.Verify(l => l.CorrectTranscriptionAsync(
            It.IsAny<int>(),
            expectedFilteredText,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteFlow_WithBothFileAndDatabaseCorrections_ShouldApplyDatabaseFirst()
    {
        // Arrange
        var dbCorrections = new List<TranscriptionCorrection>
        {
            new TranscriptionCorrection
            {
                Id = 1,
                IncorrectText = "studio",
                CorrectText = "Studio",
                Priority = 100,
                CaseSensitive = false,
                IsActive = true
            }
        };

        _correctionRepositoryMock.Setup(r => r.GetActiveCorrectionsAsync())
            .ReturnsAsync(dbCorrections);

        var fileConfig = """{"replace": {"kód": "Code"}}""";
        File.WriteAllText(_testConfigPath, fileConfig);

        var whisperText = "Visual studio kód";
        // Expected: DB correction first: "Visual Studio kód"
        // Then file correction: "Visual Studio Code"
        var expectedFilteredText = "Visual Studio Code";

        var whisperResult = new TranscriptionResult(whisperText, 1.0f);
        var savedTranscription = new WhisperTranscription { Id = 500, TranscribedText = whisperText };

        _transcriberMock.Setup(t => t.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(whisperResult);
        _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedTranscription);
        _llmCorrectionMock.Setup(l => l.CorrectTranscriptionAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mistral result");

        var textFilter = new TextFilter(
            _filterLoggerMock.Object,
            _scopeFactoryMock.Object,
            _testConfigPath);

        // Act
        var filteredText = textFilter.Apply(whisperText);

        // Assert
        Assert.Equal(expectedFilteredText, filteredText);
    }
}
