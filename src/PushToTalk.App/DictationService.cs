using Microsoft.Extensions.Logging;
using Olbrasoft.PushToTalk.App.Services;
using Olbrasoft.PushToTalk.Audio;
using Olbrasoft.PushToTalk.Speech;
using Olbrasoft.PushToTalk.TextInput;

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
/// Orchestrates the dictation workflow: recording, transcription, and text typing.
/// </summary>
public class DictationService : IDisposable, IAsyncDisposable
{
    private bool _disposed;
    private readonly ILogger<DictationService> _logger;
    private readonly IKeyboardMonitor _keyboardMonitor;
    private readonly IAudioRecorder _audioRecorder;
    private readonly ISpeechTranscriber _speechTranscriber;
    private readonly ITextTyper _textTyper;
    private readonly TypingSoundPlayer? _typingSoundPlayer;
    private readonly TextFilter? _textFilter;
    private readonly IVirtualAssistantClient? _virtualAssistantClient;
    private readonly KeyCode _triggerKey;
    private readonly KeyCode _cancelKey;

    private DictationState _state = DictationState.Idle;
    private readonly object _stateLock = new();
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _transcriptionCts;

    /// <summary>
    /// Event raised when dictation state changes.
    /// </summary>
    public event EventHandler<DictationState>? StateChanged;

    /// <summary>
    /// Event raised when transcription completes with the transcribed text.
    /// </summary>
    public event EventHandler<string>? TranscriptionCompleted;

    /// <summary>
    /// Gets the current dictation state.
    /// </summary>
    public DictationState State => _state;

    public DictationService(
        ILogger<DictationService> logger,
        IKeyboardMonitor keyboardMonitor,
        IAudioRecorder audioRecorder,
        ISpeechTranscriber speechTranscriber,
        ITextTyper textTyper,
        TypingSoundPlayer? typingSoundPlayer = null,
        TextFilter? textFilter = null,
        IVirtualAssistantClient? virtualAssistantClient = null,
        KeyCode triggerKey = KeyCode.CapsLock,
        KeyCode cancelKey = KeyCode.Escape)
    {
        _logger = logger;
        _keyboardMonitor = keyboardMonitor;
        _audioRecorder = audioRecorder;
        _speechTranscriber = speechTranscriber;
        _textTyper = textTyper;
        _typingSoundPlayer = typingSoundPlayer;
        _textFilter = textFilter;
        _virtualAssistantClient = virtualAssistantClient;
        _triggerKey = triggerKey;
        _cancelKey = cancelKey;
    }

    /// <summary>
    /// Starts monitoring keyboard for CapsLock trigger.
    /// </summary>
    public async Task StartMonitoringAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("DictationService initialized - TriggerKey: {TriggerKey}, CancelKey: {CancelKey}, TextFilter: {HasFilter}, SoundPlayer: {HasSound}",
            _triggerKey, _cancelKey, _textFilter != null, _typingSoundPlayer != null);

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
        // Handle cancel key during transcription
        if (e.Key == _cancelKey && _state == DictationState.Transcribing)
        {
            _logger.LogInformation("{CancelKey} pressed - cancelling transcription", _cancelKey);
            CancelTranscription();
            return;
        }

        if (e.Key != _triggerKey)
            return;

        // Check ACTUAL CapsLock LED state, not just toggle internal state
        // This prevents desynchronization when CapsLock is toggled while app is not in expected state
        var capsLockOn = _keyboardMonitor.IsCapsLockOn();
        _logger.LogDebug("{TriggerKey} released, CapsLock LED: {CapsLockOn}, current state: {State}",
            _triggerKey, capsLockOn, _state);

        // CapsLock ON + Idle → start recording
        if (capsLockOn && _state == DictationState.Idle)
        {
            _logger.LogInformation("{TriggerKey} pressed, CapsLock ON - starting dictation", _triggerKey);
            _ = Task.Run(() => StartDictationAsync());
        }
        // CapsLock OFF + Recording → stop recording and transcribe
        else if (!capsLockOn && _state == DictationState.Recording)
        {
            _logger.LogInformation("{TriggerKey} pressed, CapsLock OFF - stopping dictation", _triggerKey);
            _ = Task.Run(() => StopDictationAsync());
        }
        // CapsLock ON + Recording → user toggled again, stop (emergency stop)
        else if (capsLockOn && _state == DictationState.Recording)
        {
            _logger.LogWarning("CapsLock toggled ON while recording - emergency stop");
            _ = Task.Run(() => StopDictationAsync());
        }
        // If Transcribing, ignore the trigger key press (but cancel key is handled above)
    }

    /// <summary>
    /// Cancels ongoing transcription.
    /// </summary>
    public void CancelTranscription()
    {
        _logger.LogInformation("CancelTranscription called, state: {State}, has CTS: {HasCts}",
            _state, _transcriptionCts != null);

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
        lock (_stateLock)
        {
            if (_state != DictationState.Idle)
            {
                _logger.LogWarning("Cannot start dictation, current state: {State}", _state);
                return;
            }

            SetState(DictationState.Recording);
        }

        try
        {
            // Notify VirtualAssistant to stop TTS (fire-and-forget, don't block recording)
            if (_virtualAssistantClient != null)
            {
                _ = _virtualAssistantClient.NotifyRecordingStartedAsync();
            }

            _cts = new CancellationTokenSource();
            _logger.LogInformation("Starting audio recording...");
            // Fire-and-forget: don't await - recording runs in background
            // Awaiting would block until recording stops
            _ = _audioRecorder.StartRecordingAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            SetState(DictationState.Idle);
        }
    }

    /// <summary>
    /// Stops recording, transcribes, and types the result.
    /// </summary>
    public async Task StopDictationAsync()
    {
        // Check and transition state atomically
        lock (_stateLock)
        {
            if (_state != DictationState.Recording)
            {
                _logger.LogWarning("Cannot stop dictation, current state: {State}", _state);
                return;
            }
            // Create cancellation token BEFORE changing state - so it's available immediately for cancel requests
            _transcriptionCts = new CancellationTokenSource();
            // Mark as transcribing immediately to prevent concurrent calls
            SetState(DictationState.Transcribing);
        }

        try
        {
            _logger.LogInformation("Stopping audio recording...");
            await _audioRecorder.StopRecordingAsync();

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
                SetState(DictationState.Idle);
                return;
            }

            // State is already Transcribing and CTS is ready (set in lock above)

            // Check for cancellation before starting transcription
            // This gives user time to cancel while recording was stopping
            if (_transcriptionCts.Token.IsCancellationRequested)
            {
                _logger.LogInformation("Transcription cancelled before starting");
                throw new OperationCanceledException(_transcriptionCts.Token);
            }

            // Start transcription sound loop
            _typingSoundPlayer?.StartLoop();

            // One more check right before transcription
            _transcriptionCts.Token.ThrowIfCancellationRequested();

            _logger.LogInformation("Starting transcription...");
            var result = await _speechTranscriber.TranscribeAsync(audioData, _transcriptionCts.Token);

            // Stop transcription sound loop
            _typingSoundPlayer?.StopLoop();

            if (result.Success && !string.IsNullOrWhiteSpace(result.Text))
            {
                _logger.LogInformation("Transcription: {Text}", result.Text);

                // Apply text filters
                var filteredText = _textFilter?.Apply(result.Text) ?? result.Text;

                if (!string.IsNullOrWhiteSpace(filteredText))
                {
                    // Type the transcribed text
                    await _textTyper.TypeTextAsync(filteredText);
                    _logger.LogInformation("Text typed successfully");

                    // Notify listeners about transcription completion
                    TranscriptionCompleted?.Invoke(this, filteredText);
                }
                else
                {
                    _logger.LogInformation("Text empty after filtering, nothing to type");
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
            // Ensure sound is stopped even on error/cancel
            _typingSoundPlayer?.StopLoop();
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;
            _cts?.Dispose();
            _cts = null;
            SetState(DictationState.Idle);
        }
    }

    private void SetState(DictationState newState)
    {
        if (_state == newState)
            return;

        _state = newState;
        _logger.LogDebug("State changed to: {State}", _state);
        StateChanged?.Invoke(this, _state);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _keyboardMonitor.KeyReleased -= OnKeyReleased;
        _cts?.Cancel();
        _cts?.Dispose();
        _typingSoundPlayer?.Dispose();
        _speechTranscriber.Dispose();

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
        _typingSoundPlayer?.Dispose();
        _speechTranscriber.Dispose();

        GC.SuppressFinalize(this);
    }
}
