using Olbrasoft.PushToTalk.Audio;
using Olbrasoft.PushToTalk.Core.Extensions;
using Olbrasoft.PushToTalk.Service.Services;

namespace Olbrasoft.PushToTalk.Service;

/// <summary>
/// Background worker service for push-to-talk dictation.
/// Monitors keyboard for CapsLock state changes and controls audio recording.
/// Records when CapsLock is ON, stops and transcribes when CapsLock is OFF.
/// Also provides remote control capabilities via IRecordingController interface.
/// </summary>
public class DictationWorker : BackgroundService, IRecordingStateProvider, IRecordingController
{
    private readonly ILogger<DictationWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IKeyboardMonitor _keyboardMonitor;
    private readonly IAudioRecorder _audioRecorder;
    private readonly ITranscriptionProcessor _transcriptionProcessor;
    private readonly ITextOutputService _textOutputService;
    private readonly IPttNotifier _pttNotifier;
    private readonly ManualMuteService _manualMuteService;
    private readonly IRecordingModeManager _recordingModeManager;

    private bool _isRecording;
    private bool _isTranscribing;
    private DateTime? _recordingStartTime;
    private KeyCode _triggerKey;

    // IRecordingStateProvider implementation
    /// <inheritdoc />
    public bool IsRecording => _isRecording;

    /// <inheritdoc />
    public bool IsTranscribing => _isTranscribing;

    /// <inheritdoc />
    public TimeSpan? RecordingDuration => _recordingStartTime.HasValue
        ? DateTime.UtcNow - _recordingStartTime.Value
        : null;

    /// <summary>
    /// Stores the recording mode context for state restoration.
    /// </summary>
    private RecordingModeContext? _recordingModeContext;

    /// <summary>
    /// CancellationTokenSource for the current transcription operation.
    /// Allows canceling Whisper transcription when user presses Escape.
    /// </summary>
    private CancellationTokenSource? _transcriptionCts;

    public DictationWorker(
        ILogger<DictationWorker> logger,
        IConfiguration configuration,
        IKeyboardMonitor keyboardMonitor,
        IAudioRecorder audioRecorder,
        ITranscriptionProcessor transcriptionProcessor,
        ITextOutputService textOutputService,
        IPttNotifier pttNotifier,
        ManualMuteService manualMuteService,
        IRecordingModeManager recordingModeManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _keyboardMonitor = keyboardMonitor ?? throw new ArgumentNullException(nameof(keyboardMonitor));
        _audioRecorder = audioRecorder ?? throw new ArgumentNullException(nameof(audioRecorder));
        _transcriptionProcessor = transcriptionProcessor ?? throw new ArgumentNullException(nameof(transcriptionProcessor));
        _textOutputService = textOutputService ?? throw new ArgumentNullException(nameof(textOutputService));
        _pttNotifier = pttNotifier ?? throw new ArgumentNullException(nameof(pttNotifier));
        _manualMuteService = manualMuteService ?? throw new ArgumentNullException(nameof(manualMuteService));
        _recordingModeManager = recordingModeManager ?? throw new ArgumentNullException(nameof(recordingModeManager));

        // Load configuration
        var triggerKeyName = _configuration.GetValue<string>("PushToTalkDictation:TriggerKey", "CapsLock");
        _triggerKey = Enum.Parse<KeyCode>(triggerKeyName);

        _logger.LogWarning("=== NOTIFIER HASH: {Hash} ===", _pttNotifier.GetHashCode());
        _logger.LogInformation("Dictation worker initialized. Trigger key: {TriggerKey}", _triggerKey);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Push-to-Talk Dictation Service starting...");

        try
        {
            // Subscribe to keyboard events
            _keyboardMonitor.KeyPressed += OnKeyPressed;
            _keyboardMonitor.KeyReleased += OnKeyReleased;

            _logger.LogInformation("Press {TriggerKey} to start dictation, release to stop", _triggerKey);

            // Start keyboard monitoring (doesn't block)
            await _keyboardMonitor.StartMonitoringAsync(stoppingToken);

            // Wait indefinitely until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Dictation service is stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in dictation worker");
            throw;
        }
        finally
        {
            _keyboardMonitor.KeyPressed -= OnKeyPressed;
            _keyboardMonitor.KeyReleased -= OnKeyReleased;

            if (_isRecording)
            {
                await StopRecordingAsync();
            }
        }
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        // We only care about the trigger key
        // Actual action is taken in OnKeyReleased after LED state updates
    }

    /// <summary>
    /// Handles ScrollLock (ManualMute) key press - toggles mute state and broadcasts to all clients.
    /// Uses internal state tracking since ScrollLock LED doesn't work on Wayland.
    /// </summary>
    private async Task HandleScrollLockPressedAsync()
    {
        try
        {
            // Toggle internal mute state (LED doesn't work on Wayland)
            var newMuteState = _manualMuteService.Toggle();

            _logger.LogInformation("ScrollLock pressed - ManualMute: {State}",
                newMuteState ? "MUTED" : "UNMUTED");

            // Broadcast to all clients
            await _pttNotifier.NotifyManualMuteChangedAsync(newMuteState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle ScrollLock press");
        }
    }

    private void OnKeyReleased(object? sender, KeyEventArgs e)
    {
        // Handle ScrollLock (ManualMute) - toggle on key release
        if (e.Key == KeyCode.ScrollLock)
        {
            Task.Run(async () => await HandleScrollLockPressedAsync()).FireAndForget(_logger, "HandleScrollLockPressed");
            return;
        }

        // Handle Escape - cancel ongoing transcription
        if (e.Key == KeyCode.Escape)
        {
            if (_isTranscribing && _transcriptionCts != null)
            {
                _logger.LogInformation("Escape pressed - canceling transcription");
                _transcriptionCts.Cancel();
            }
            return;
        }

        if (e.Key != _triggerKey)
            return;

        // Read actual CapsLock state from LED - this is the reliable source of truth
        // LED state is updated by the kernel AFTER the key event is processed
        // Small delay to ensure LED state is updated
        Thread.Sleep(50);

        var capsLockOn = _keyboardMonitor.IsCapsLockOn();
        _logger.LogDebug("CapsLock released - LED state: {CapsLockOn}, Recording state: {Recording}", capsLockOn, _isRecording);

        if (capsLockOn && !_isRecording)
        {
            // CapsLock is ON and not recording - start recording
            _logger.LogInformation("CapsLock ON - starting dictation");
            Task.Run(async () => await StartRecordingAsync()).FireAndForget(_logger, "StartRecording");
        }
        else if (!capsLockOn && _isRecording)
        {
            // CapsLock is OFF and recording - stop and transcribe
            _logger.LogInformation("CapsLock OFF - stopping dictation");
            Task.Run(async () => await StopRecordingAsync()).FireAndForget(_logger, "StopRecording");
        }
        else
        {
            _logger.LogDebug("CapsLock state ({CapsLockOn}) matches recording state ({Recording}) - no action needed",
                capsLockOn, _isRecording);
        }
    }

    private async Task StartRecordingAsync()
    {
        if (_isRecording)
        {
            _logger.LogWarning("Recording is already in progress");
            return;
        }

        try
        {
            _isRecording = true;
            _recordingStartTime = DateTime.UtcNow;

            _logger.LogInformation("Starting audio recording...");

            // Enter recording mode (stops TTS, creates lock, saves mute state)
            _recordingModeContext = await _recordingModeManager.EnterRecordingModeAsync();

            // Notify clients about recording start
            await _pttNotifier.NotifyRecordingStartedAsync();

            // Start recording (runs until cancelled)
            var cts = new CancellationTokenSource();
            await _audioRecorder.StartRecordingAsync(cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            _isRecording = false;
            _recordingStartTime = null;
        }
    }

    private async Task StopRecordingAsync()
    {
        if (!_isRecording)
        {
            _logger.LogWarning("No recording in progress");
            return;
        }

        double durationSeconds = 0;

        try
        {
            _logger.LogInformation("Stopping audio recording...");

            await _audioRecorder.StopRecordingAsync();

            var recordedData = _audioRecorder.GetRecordedData();
            _logger.LogInformation("Recording stopped. Captured {ByteCount} bytes", recordedData.Length);

            // Calculate duration
            if (_recordingStartTime.HasValue)
            {
                durationSeconds = (DateTime.UtcNow - _recordingStartTime.Value).TotalSeconds;
                _logger.LogInformation("Total recording duration: {Duration:F2}s", durationSeconds);
            }

            // Notify clients about recording stop
            await _pttNotifier.NotifyRecordingStoppedAsync(durationSeconds);

            if (recordedData.Length > 0)
            {
                // Show icon and play sound IMMEDIATELY (before Whisper processing)
                await _pttNotifier.NotifyTranscriptionStartedAsync();

                // Create cancellation token for transcription (can be cancelled with Escape key)
                _transcriptionCts = new CancellationTokenSource();
                _isTranscribing = true;

                try
                {
                    _logger.LogInformation("Starting transcription... (press Escape to cancel)");
                    var result = await _transcriptionProcessor.ProcessAsync(recordedData, _transcriptionCts.Token);

                    if (result.Success && !string.IsNullOrWhiteSpace(result.Text))
                    {
                        // Notify clients about successful transcription
                        await _pttNotifier.NotifyTranscriptionCompletedAsync(result.Text, result.Confidence);

                        // Output text (types and saves to history)
                        await _textOutputService.OutputTextAsync(result.Text);
                    }
                    else if (result.WasHallucination)
                    {
                        // Play rejection sound for hallucination
                        Task.Run(async () => await _textOutputService.PlayRejectionSoundAsync()).FireAndForget(_logger, "PlayRejectionSound");
                        await _pttNotifier.NotifyTranscriptionFailedAsync(result.ErrorMessage ?? "Whisper hallucination filtered");
                    }
                    else
                    {
                        _logger.LogWarning("Transcription failed: {Error}", result.ErrorMessage);
                        await _pttNotifier.NotifyTranscriptionFailedAsync(result.ErrorMessage ?? "Transcription failed");
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Transcription cancelled by user (Escape key pressed)");

                    // Play rejection sound for cancellation
                    Task.Run(async () => await _textOutputService.PlayRejectionSoundAsync()).FireAndForget(_logger, "PlayRejectionSound");

                    await _pttNotifier.NotifyTranscriptionFailedAsync("Transcription cancelled");
                }
                finally
                {
                    _isTranscribing = false;
                    _transcriptionCts?.Dispose();
                    _transcriptionCts = null;
                }
            }
            else
            {
                _logger.LogWarning("No audio data recorded");

                // Play rejection sound for empty recording
                Task.Run(async () => await _textOutputService.PlayRejectionSoundAsync()).FireAndForget(_logger, "PlayRejectionSound");

                await _pttNotifier.NotifyTranscriptionFailedAsync("No audio data recorded");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop recording");
            await _pttNotifier.NotifyTranscriptionFailedAsync(ex.Message);
        }
        finally
        {
            _isRecording = false;
            _recordingStartTime = null;

            // Exit recording mode (releases lock, restores mute state)
            if (_recordingModeContext != null)
            {
                await _recordingModeManager.ExitRecordingModeAsync(_recordingModeContext);
                _recordingModeContext = null;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dictation service stopping...");

        if (_isRecording)
        {
            await StopRecordingAsync();
        }

        await _keyboardMonitor.StopMonitoringAsync();

        await base.StopAsync(cancellationToken);
    }

    // IRecordingController implementation

    /// <inheritdoc />
    public async Task<bool> StartRecordingRemoteAsync(CancellationToken cancellationToken = default)
    {
        if (_isRecording)
        {
            _logger.LogWarning("Remote start requested but recording is already in progress");
            return false;
        }

        _logger.LogInformation("Remote recording start requested");
        await StartRecordingAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> StopRecordingRemoteAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRecording)
        {
            _logger.LogWarning("Remote stop requested but no recording in progress");
            return false;
        }

        _logger.LogInformation("Remote recording stop requested");
        await StopRecordingAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ToggleRecordingAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Remote recording toggle requested, current state: {IsRecording}", _isRecording);

        if (_isRecording)
        {
            await StopRecordingAsync();
            return false; // Now stopped
        }
        else
        {
            await StartRecordingAsync();
            return true; // Now recording
        }
    }
}
