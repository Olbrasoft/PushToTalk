using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.NotificationAudio.Abstractions;
using Olbrasoft.PushToTalk.Core.Interfaces;
using Olbrasoft.PushToTalk.Core.Models;
using PushToTalk.Data;

namespace Olbrasoft.PushToTalk.App.Services;

/// <summary>
/// Coordinates transcription with audio feedback (typing sound loop).
/// </summary>
public class TranscriptionCoordinator : ITranscriptionCoordinator
{
    private readonly ILogger<TranscriptionCoordinator> _logger;
    private readonly ISpeechTranscriber _speechTranscriber;
    private readonly INotificationPlayer _notificationPlayer;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITextFilter? _textFilter;
    private readonly string? _soundPath;
    private bool _disposed;
    private Task? _loopTask;
    private CancellationTokenSource? _loopCts;

    public TranscriptionCoordinator(
        ILogger<TranscriptionCoordinator> logger,
        ISpeechTranscriber speechTranscriber,
        INotificationPlayer notificationPlayer,
        IServiceScopeFactory serviceScopeFactory,
        ITextFilter? textFilter = null,
        string? soundPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _speechTranscriber = speechTranscriber ?? throw new ArgumentNullException(nameof(speechTranscriber));
        _notificationPlayer = notificationPlayer ?? throw new ArgumentNullException(nameof(notificationPlayer));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _textFilter = textFilter;
        _soundPath = soundPath;
    }

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeWithFeedbackAsync(
        byte[] audioData,
        CancellationToken cancellationToken = default)
    {
        if (audioData == null || audioData.Length == 0)
        {
            return new TranscriptionResult("No audio data provided");
        }

        try
        {
            // Start transcription sound loop
            StartSoundLoop();

            _logger.LogInformation("Starting transcription of {ByteCount} bytes...", audioData.Length);

            // Step 1: Whisper transcription
            var whisperResult = await _speechTranscriber.TranscribeAsync(audioData, cancellationToken);

            if (!whisperResult.Success || string.IsNullOrWhiteSpace(whisperResult.Text))
            {
                return whisperResult;
            }

            // Step 2: Apply TextFilter corrections (database + file-based)
            var filteredText = _textFilter?.Apply(whisperResult.Text) ?? whisperResult.Text;

            if (filteredText != whisperResult.Text)
            {
                _logger.LogInformation("TextFilter applied: '{Original}' → '{Filtered}'",
                    whisperResult.Text, filteredText);
            }

            // Step 3: Save to database and run Mistral LLM correction (SYNCHRONOUSLY - user waits for this!)
            string finalText = filteredText;

            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ITranscriptionRepository>();
                var llmCorrectionService = scope.ServiceProvider.GetRequiredService<ILlmCorrectionService>();

                // Calculate audio duration
                var durationMs = (int)((audioData.Length / 2.0) / 16000.0 * 1000.0);

                // Save Whisper transcription (original, not filtered)
                var transcription = await repository.SaveAsync(
                    text: whisperResult.Text,
                    durationMs: durationMs,
                    ct: cancellationToken);

                _logger.LogDebug("Transcription saved to database with ID: {TranscriptionId}", transcription.Id);

                // Run Mistral LLM correction (AWAIT - don't return until complete!)
                // This ensures sound loop plays during entire process
                var mistralCorrectedText = await llmCorrectionService.CorrectTranscriptionAsync(
                    transcription.Id,
                    filteredText,
                    cancellationToken);

                if (mistralCorrectedText != filteredText)
                {
                    _logger.LogInformation("Mistral corrected text from '{Filtered}' to '{Corrected}'",
                        filteredText, mistralCorrectedText);
                    finalText = mistralCorrectedText;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save transcription or run LLM correction - using filtered text");
                // On error, fallback to filtered text (TextFilter corrections only)
            }

            // Return result with final text (TextFilter + Mistral corrections)
            return new TranscriptionResult(finalText, whisperResult.Confidence);
        }
        finally
        {
            // CRITICAL: Sound loop stops ONLY after Mistral completes (or errors)
            await StopSoundLoopAsync();
        }
    }

    private void StartSoundLoop()
    {
        if (string.IsNullOrWhiteSpace(_soundPath) || !File.Exists(_soundPath))
        {
            _logger.LogDebug("Transcription sound disabled (no path configured or file not found)");
            return;
        }

        _loopCts = new CancellationTokenSource();
        _loopTask = PlaySoundLoopAsync(_loopCts.Token);
        _logger.LogDebug("Transcription sound loop started");
    }

    private async Task StopSoundLoopAsync()
    {
        if (_loopCts == null || _loopTask == null)
            return;

        _loopCts.Cancel();

        try
        {
            await _loopTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error stopping sound loop");
        }
        finally
        {
            _loopCts?.Dispose();
            _loopCts = null;
            _loopTask = null;
        }

        _logger.LogDebug("Transcription sound loop stopped");
    }

    private async Task PlaySoundLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _notificationPlayer.PlayAsync(_soundPath!, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error in sound loop");
                // Small delay before retry
                try
                {
                    await Task.Delay(100, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Stop sound loop synchronously
        try
        {
            _loopCts?.Cancel();
            _loopTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Ignore
        }
        finally
        {
            _loopCts?.Dispose();
        }

        _speechTranscriber.Dispose();

        GC.SuppressFinalize(this);
    }
}
