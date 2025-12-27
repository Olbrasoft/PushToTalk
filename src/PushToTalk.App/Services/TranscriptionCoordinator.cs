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
    private readonly string? _soundPath;
    private bool _disposed;
    private Task? _loopTask;
    private CancellationTokenSource? _loopCts;

    public TranscriptionCoordinator(
        ILogger<TranscriptionCoordinator> logger,
        ISpeechTranscriber speechTranscriber,
        INotificationPlayer notificationPlayer,
        IServiceScopeFactory serviceScopeFactory,
        string? soundPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _speechTranscriber = speechTranscriber ?? throw new ArgumentNullException(nameof(speechTranscriber));
        _notificationPlayer = notificationPlayer ?? throw new ArgumentNullException(nameof(notificationPlayer));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
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
            var result = await _speechTranscriber.TranscribeAsync(audioData, cancellationToken);

            // Save successful transcription to database (background task, don't block)
            if (result.Success && !string.IsNullOrWhiteSpace(result.Text))
            {
                // Calculate audio duration from byte size
                // Audio format: 16000 Hz, 1 channel (mono), 16-bit (2 bytes per sample)
                var durationMs = (int)((audioData.Length / 2.0) / 16000.0 * 1000.0);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var repository = scope.ServiceProvider.GetRequiredService<ITranscriptionRepository>();
                        var llmCorrectionService = scope.ServiceProvider.GetRequiredService<ILlmCorrectionService>();

                        // Save Whisper transcription first
                        var transcription = await repository.SaveAsync(
                            text: result.Text,
                            durationMs: durationMs,
                            ct: CancellationToken.None); // Use None since this is background task

                        _logger.LogDebug("Transcription saved to database with ID: {TranscriptionId}", transcription.Id);

                        // Run LLM correction (async, non-blocking for dictation workflow)
                        var correctedText = await llmCorrectionService.CorrectTranscriptionAsync(
                            transcription.Id,
                            result.Text,
                            CancellationToken.None);

                        if (correctedText != result.Text)
                        {
                            _logger.LogInformation("LLM corrected text from '{Original}' to '{Corrected}'",
                                result.Text, correctedText);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save transcription or run LLM correction");
                    }
                }, cancellationToken);
            }

            return result;
        }
        finally
        {
            // Always stop sound loop
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

                // Small delay to allow cancellation token to be checked
                // Prevents tight loop when PlayAsync completes immediately (e.g., in tests)
                await Task.Delay(10, cancellationToken);
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
