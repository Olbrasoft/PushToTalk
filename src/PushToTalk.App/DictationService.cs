using Microsoft.Extensions.Logging;
using Olbrasoft.NotificationAudio.Abstractions;
using Olbrasoft.PushToTalk.App.Keyboard;
using Olbrasoft.PushToTalk.App.Services;
using Olbrasoft.PushToTalk.App.StateMachine;
using Olbrasoft.PushToTalk.Audio;
using Olbrasoft.PushToTalk.Core.Extensions;
using Olbrasoft.PushToTalk.Core.Interfaces;

namespace Olbrasoft.PushToTalk.App;

/// <summary>
/// Dictation states for the application.
/// </summary>
public enum DictationState
{
    Idle,
    Recording,
    Transcribing
}

/// <summary>
/// Orchestrates the dictation workflow: recording, transcription, and text output.
/// </summary>
/// <remarks>
/// Refactored to use extracted services for SRP compliance:
/// - IDictationStateMachine: State management and transitions
/// - ICapsLockSynchronizer: CapsLock LED synchronization
/// - ITranscriptionCoordinator: Transcription with feedback
/// - ITextOutputHandler: Text output to active window
/// </remarks>
public class DictationService : IDisposable, IAsyncDisposable
{
    private bool _disposed;
    private readonly ILogger<DictationService> _logger;
    private readonly IDictationStateMachine _stateMachine;
    private readonly ICapsLockSynchronizer _capsLockSynchronizer;
    private readonly IKeyboardMonitor _keyboardMonitor;
    private readonly IAudioRecorder _audioRecorder;
    private readonly ITranscriptionCoordinator _transcriptionCoordinator;
    private readonly ITextOutputHandler _textOutputHandler;
    private readonly IVirtualAssistantClient? _virtualAssistantClient;
    private readonly INotificationPlayer? _notificationPlayer;
    private readonly string? _recordingStartSoundPath;
    private readonly KeyCode _triggerKey;
    private readonly KeyCode _cancelKey;
    private Func<Task<bool>>? _serviceAvailabilityCheck;

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _transcriptionCts;

    /// <summary>
    /// Event raised when transcription completes with the transcribed text.
    /// </summary>
    public event EventHandler<string>? TranscriptionCompleted;

    /// <summary>
    /// Gets the current dictation state.
    /// </summary>
    public DictationState State => _stateMachine.CurrentState;

    public DictationService(
        ILogger<DictationService> logger,
        IDictationStateMachine stateMachine,
        ICapsLockSynchronizer capsLockSynchronizer,
        IKeyboardMonitor keyboardMonitor,
        IAudioRecorder audioRecorder,
        ITranscriptionCoordinator transcriptionCoordinator,
        ITextOutputHandler textOutputHandler,
        IVirtualAssistantClient? virtualAssistantClient = null,
        INotificationPlayer? notificationPlayer = null,
        string? recordingStartSoundPath = null,
        KeyCode triggerKey = KeyCode.CapsLock,
        KeyCode cancelKey = KeyCode.Escape)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _capsLockSynchronizer = capsLockSynchronizer ?? throw new ArgumentNullException(nameof(capsLockSynchronizer));
        _keyboardMonitor = keyboardMonitor ?? throw new ArgumentNullException(nameof(keyboardMonitor));
        _audioRecorder = audioRecorder ?? throw new ArgumentNullException(nameof(audioRecorder));
        _transcriptionCoordinator = transcriptionCoordinator ?? throw new ArgumentNullException(nameof(transcriptionCoordinator));
        _textOutputHandler = textOutputHandler ?? throw new ArgumentNullException(nameof(textOutputHandler));
        _virtualAssistantClient = virtualAssistantClient;
        _notificationPlayer = notificationPlayer;
        _recordingStartSoundPath = recordingStartSoundPath;
        _triggerKey = triggerKey;
        _cancelKey = cancelKey;
    }

    /// <summary>
    /// Sets a callback to check if the transcription service is available before starting recording.
    /// </summary>
    public void SetServiceAvailabilityCheck(Func<Task<bool>> check)
    {
        _serviceAvailabilityCheck = check;
    }

    /// <summary>
    /// Starts monitoring keyboard for CapsLock trigger.
    /// </summary>
    public async Task StartMonitoringAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("DictationService initialized - TriggerKey: {TriggerKey}, CancelKey: {CancelKey}",
            _triggerKey, _cancelKey);

        _keyboardMonitor.KeyReleased += OnKeyReleased;

        _logger.LogInformation("Starting keyboard monitor, trigger key: {TriggerKey}", _triggerKey);
        await _keyboardMonitor.StartMonitoringAsync(cancellationToken);
    }

    /// <summary>
    /// Stops keyboard monitoring.
    /// </summary>
    public async Task StopMonitoringAsync()
    {
        _keyboardMonitor.KeyReleased -= OnKeyReleased;
        await _keyboardMonitor.StopMonitoringAsync();
    }

    private void OnKeyReleased(object? sender, KeyEventArgs e)
    {
        var currentState = _stateMachine.CurrentState;

        // Handle cancel key during transcription
        if (e.Key == _cancelKey && currentState == DictationState.Transcribing)
        {
            _logger.LogInformation("{CancelKey} pressed - cancelling transcription", _cancelKey);
            CancelTranscription();
            return;
        }

        if (e.Key != _triggerKey)
            return;

        // Ignore CapsLock events during LED synchronization (web remote control)
        if (_capsLockSynchronizer.IsSynchronizing)
        {
            _logger.LogDebug("Ignoring {TriggerKey} event during LED synchronization", _triggerKey);
            return;
        }

        // Check ACTUAL CapsLock LED state, not just toggle internal state
        // This prevents desynchronization when CapsLock is toggled while app is not in expected state
        var capsLockOn = _keyboardMonitor.IsCapsLockOn();
        _logger.LogDebug("{TriggerKey} released, CapsLock LED: {CapsLockOn}, current state: {State}",
            _triggerKey, capsLockOn, currentState);

        // CapsLock ON + Idle → start recording (check service availability first)
        if (capsLockOn && currentState == DictationState.Idle)
        {
            _logger.LogInformation("{TriggerKey} pressed, CapsLock ON - checking service availability", _triggerKey);
            Task.Run(async () =>
            {
                // Check if transcription service is available
                if (_serviceAvailabilityCheck != null)
                {
                    var isAvailable = await _serviceAvailabilityCheck();
                    if (!isAvailable)
                    {
                        _logger.LogWarning("SpeechToText service is not running - cannot start dictation");
                        return;
                    }
                }

                await StartDictationAsync();
            }).FireAndForget(_logger, "StartDictation");
        }
        // CapsLock OFF + Recording → stop recording and transcribe
        else if (!capsLockOn && currentState == DictationState.Recording)
        {
            _logger.LogInformation("{TriggerKey} pressed, CapsLock OFF - stopping dictation", _triggerKey);
            Task.Run(() => StopDictationAsync()).FireAndForget(_logger, "StopDictation");
        }
        // CapsLock ON + Recording → user toggled again, stop (emergency stop)
        else if (capsLockOn && currentState == DictationState.Recording)
        {
            _logger.LogWarning("CapsLock toggled ON while recording - emergency stop");
            Task.Run(() => StopDictationAsync()).FireAndForget(_logger, "StopDictation");
        }
        // If Transcribing, ignore the trigger key press (but cancel key is handled above)
    }

    /// <summary>
    /// Cancels ongoing transcription.
    /// </summary>
    public void CancelTranscription()
    {
        _logger.LogInformation("CancelTranscription called, state: {State}, has CTS: {HasCts}",
            _stateMachine.CurrentState, _transcriptionCts != null);

        // Always try to cancel, regardless of state (race condition protection)
        if (_transcriptionCts != null && !_transcriptionCts.IsCancellationRequested)
        {
            _logger.LogInformation("Cancelling transcription token");
            _transcriptionCts.Cancel();
        }
    }

    /// <summary>
    /// Starts recording audio for dictation.
    /// </summary>
    public async Task StartDictationAsync()
    {
        // Check and transition state
        if (!_stateMachine.CanTransitionTo(DictationState.Recording))
        {
            _logger.LogWarning("Cannot start dictation, current state: {State}", _stateMachine.CurrentState);
            return;
        }

        _stateMachine.TransitionTo(DictationState.Recording);

        try
        {
            // Synchronize CapsLock LED (for web remote control)
            await _capsLockSynchronizer.SynchronizeLedAsync(shouldBeOn: true);

            // Play recording start notification sound (fire-and-forget)
            if (_notificationPlayer != null && !string.IsNullOrWhiteSpace(_recordingStartSoundPath))
            {
                _ = _notificationPlayer.PlayAsync(_recordingStartSoundPath);
            }

            // Notify VirtualAssistant to stop TTS (fire-and-forget, don't block recording)
            if (_virtualAssistantClient != null)
            {
                _ = _virtualAssistantClient.NotifyRecordingStartedAsync();
            }

            _cts = new CancellationTokenSource();
            _logger.LogInformation("Starting audio recording...");
            // Fire-and-forget: don't await - recording runs in background
            // Awaiting would block until recording stops
            // But we need to handle failures, so use ContinueWith
            var recordingTask = _audioRecorder.StartRecordingAsync(_cts.Token);
            _ = recordingTask.ContinueWith(t =>
            {
                if (t.IsFaulted && _stateMachine.CurrentState == DictationState.Recording)
                {
                    _logger.LogError(t.Exception, "Recording failed");
                    _stateMachine.TransitionTo(DictationState.Idle);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            _stateMachine.TransitionTo(DictationState.Idle);
        }
    }

    /// <summary>
    /// Stops recording, transcribes, and outputs the result.
    /// </summary>
    public async Task StopDictationAsync()
    {
        // Check and transition state
        if (!_stateMachine.CanTransitionTo(DictationState.Transcribing))
        {
            _logger.LogWarning("Cannot stop dictation, current state: {State}", _stateMachine.CurrentState);
            return;
        }

        // Create cancellation token BEFORE changing state - so it's available immediately for cancel requests
        _transcriptionCts = new CancellationTokenSource();

        // Mark as transcribing immediately to prevent concurrent calls
        _stateMachine.TransitionTo(DictationState.Transcribing);

        try
        {
            _logger.LogInformation("Stopping audio recording...");
            await _audioRecorder.StopRecordingAsync();

            // Synchronize CapsLock LED IMMEDIATELY after stopping recording (for web remote control)
            // This MUST happen before transcription starts, not after!
            try
            {
                await _capsLockSynchronizer.SynchronizeLedAsync(shouldBeOn: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to synchronize CapsLock LED after recording stop");
            }

            // Notify VirtualAssistant to release speech lock immediately after recording stops
            // This allows TTS to play while Whisper transcribes (they're independent)
            if (_virtualAssistantClient != null)
            {
                _ = _virtualAssistantClient.NotifyRecordingStoppedAsync();
            }

            var audioData = _audioRecorder.GetRecordedData();
            _logger.LogInformation("Recording stopped. Captured {ByteCount} bytes", audioData.Length);

            if (audioData.Length == 0)
            {
                _logger.LogWarning("No audio data recorded");
                _stateMachine.TransitionTo(DictationState.Idle);
                return;
            }

            // Check for cancellation before starting transcription
            // This gives user time to cancel while recording was stopping
            if (_transcriptionCts.Token.IsCancellationRequested)
            {
                _logger.LogInformation("Transcription cancelled before starting");
                throw new OperationCanceledException(_transcriptionCts.Token);
            }

            // Transcribe with feedback (sound loop handled by coordinator)
            var result = await _transcriptionCoordinator.TranscribeWithFeedbackAsync(
                audioData,
                _transcriptionCts.Token);

            if (result.Success && !string.IsNullOrWhiteSpace(result.Text))
            {
                _logger.LogInformation("Transcription: {Text}", result.Text);

                // Output text (filtering + typing handled by handler)
                var typedText = await _textOutputHandler.OutputTextAsync(result.Text);

                if (typedText != null)
                {
                    // Notify listeners about transcription completion
                    TranscriptionCompleted?.Invoke(this, typedText);
                }
            }
            else
            {
                _logger.LogWarning("Transcription failed: {Error}", result.ErrorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Transcription cancelled by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed during transcription");
        }
        finally
        {
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;
            _cts?.Dispose();
            _cts = null;
            _stateMachine.TransitionTo(DictationState.Idle);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _keyboardMonitor.KeyReleased -= OnKeyReleased;
        _cts?.Cancel();
        _cts?.Dispose();
        _transcriptionCoordinator.Dispose();

        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _keyboardMonitor.KeyReleased -= OnKeyReleased;

        // Async cleanup
        await StopMonitoringAsync();

        _cts?.Cancel();
        _cts?.Dispose();
        _transcriptionCoordinator.Dispose();

        GC.SuppressFinalize(this);
    }
}
