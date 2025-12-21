using Microsoft.Extensions.Logging;
using Olbrasoft.NotificationAudio.Abstractions;
using Olbrasoft.PushToTalk.Core.Interfaces;
using Olbrasoft.PushToTalk.Core.Models;

namespace Olbrasoft.PushToTalk.App.Services;

/// <summary>
/// Coordinates transcription with audio feedback (typing sound loop).
/// </summary>
public class TranscriptionCoordinator : ITranscriptionCoordinator
{
    private readonly ILogger<TranscriptionCoordinator> _logger;
    private readonly ISpeechTranscriber _speechTranscriber;
    private readonly INotificationPlayer _notificationPlayer;
    private readonly string? _soundPath;
    private bool _disposed;
    private Task? _loopTask;
    private CancellationTokenSource? _loopCts;

    public TranscriptionCoordinator(
        ILogger<TranscriptionCoordinator> logger,
        ISpeechTranscriber speechTranscriber,
        INotificationPlayer notificationPlayer,
        string? soundPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _speechTranscriber = speechTranscriber ?? throw new ArgumentNullException(nameof(speechTranscriber));
        _notificationPlayer = notificationPlayer ?? throw new ArgumentNullException(nameof(notificationPlayer));
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
