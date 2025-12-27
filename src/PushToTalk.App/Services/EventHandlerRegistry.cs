using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Olbrasoft.PushToTalk.App.Hubs;
using Olbrasoft.PushToTalk.App.StateMachine;
using Olbrasoft.PushToTalk.App.Tray;
using Olbrasoft.PushToTalk.Core.Extensions;

namespace Olbrasoft.PushToTalk.App.Services;

/// <summary>
/// Centralizes event handler registration for dictation and tray services.
/// Follows Single Responsibility Principle by separating event coordination from Program.cs.
/// </summary>
public class EventHandlerRegistry
{
    private readonly ILogger<EventHandlerRegistry> _logger;
    private readonly DictationService _dictationService;
    private readonly IDictationStateMachine _stateMachine;
    private readonly PushToTalkTrayService _trayService;
    private readonly IHubContext<DictationHub> _hubContext;
    private readonly SpeechToTextServiceManager _sttServiceManager;
    private readonly string _appVersion;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public EventHandlerRegistry(
        ILogger<EventHandlerRegistry> logger,
        DictationService dictationService,
        IDictationStateMachine stateMachine,
        PushToTalkTrayService trayService,
        IHubContext<DictationHub> hubContext,
        SpeechToTextServiceManager sttServiceManager,
        string appVersion,
        CancellationTokenSource cancellationTokenSource)
    {
        _logger = logger;
        _dictationService = dictationService;
        _stateMachine = stateMachine;
        _trayService = trayService;
        _hubContext = hubContext;
        _sttServiceManager = sttServiceManager;
        _appVersion = appVersion;
        _cancellationTokenSource = cancellationTokenSource;
    }

    /// <summary>
    /// Registers all event handlers for dictation and tray services.
    /// </summary>
    public void RegisterHandlers()
    {
        RegisterDictationServiceHandlers();
        RegisterTrayServiceHandlers();
        CheckSpeechToTextServiceStatusOnStartup();
    }

    private void RegisterDictationServiceHandlers()
    {
        // Handle state changes from state machine
        // Main icon stays visible, animated icon shows NEXT TO it during transcription (issue #62)
        _stateMachine.StateChanged += async (_, state) =>
        {
            // Update tray icon
            switch (state)
            {
                case DictationState.Idle:
                    _trayService.HideAnimatedIcon();
                    // Check if SpeechToText service is running and show appropriate icon
                    var isRunning = await _sttServiceManager.IsRunningAsync();
                    if (isRunning)
                    {
                        _trayService.SetIcon("push-to-talk");
                        _trayService.SetTooltip("Push To Talk - Idle");
                    }
                    else
                    {
                        _trayService.SetIcon("push-to-talk-off");
                        _trayService.SetTooltip("Push To Talk - Service Stopped");
                    }
                    break;
                case DictationState.Recording:
                    _trayService.HideAnimatedIcon();
                    _trayService.SetIcon("push-to-talk-recording");
                    _trayService.SetTooltip("Push To Talk - Recording...");
                    break;
                case DictationState.Transcribing:
                    // Change icon back to white immediately when recording stops (issue #28)
                    _trayService.SetIcon("push-to-talk");
                    _trayService.SetTooltip("Push To Talk - Transcribing...");
                    // Show animated icon NEXT TO main icon (main stays visible)
                    await _trayService.ShowAnimatedIconAsync();
                    break;
            }

            // Broadcast state change to SignalR clients
            var eventType = state switch
            {
                DictationState.Recording => DictationEventType.RecordingStarted,
                DictationState.Transcribing => DictationEventType.TranscriptionStarted,
                _ => DictationEventType.RecordingStopped
            };

            await _hubContext.Clients.All.SendAsync("DictationEvent", new DictationEvent
            {
                EventType = eventType,
                Text = null
            });
        };

        // Handle transcription completion - send result to SignalR clients
        _dictationService.TranscriptionCompleted += async (_, text) =>
        {
            await _hubContext.Clients.All.SendAsync("DictationEvent", new DictationEvent
            {
                EventType = DictationEventType.TranscriptionCompleted,
                Text = text
            });
        };
    }

    private void RegisterTrayServiceHandlers()
    {
        // Handle click on tray icon
        _trayService.OnClicked += () =>
        {
            _logger.LogInformation("Tray icon clicked");
        };

        // Handle Quit menu item
        _trayService.OnQuitRequested += () =>
        {
            _logger.LogInformation("Quit requested from tray menu");
            Console.WriteLine("\nQuit requested - shutting down...");
            _cancellationTokenSource.Cancel();
        };

        // Handle About menu item
        _trayService.OnAboutRequested += () =>
        {
            _logger.LogInformation("About dialog requested");
            AboutDialog.Show(_appVersion);
        };

        // Handle SpeechToText service stop request
        _trayService.OnStopSpeechToTextRequested += async () =>
        {
            _logger.LogInformation("Stopping SpeechToText service...");
            var stopped = await _sttServiceManager.StopAsync();
            if (stopped)
            {
                _logger.LogInformation("SpeechToText service stopped successfully");
                _trayService.UpdateSpeechToTextStatus(false, _sttServiceManager.GetVersion());
                _trayService.SetIcon("push-to-talk-off");
                _trayService.SetTooltip("Push To Talk (Service Stopped)");
            }
        };

        // Handle SpeechToText service start request
        _trayService.OnStartSpeechToTextRequested += async () =>
        {
            _logger.LogInformation("Starting SpeechToText service...");
            var started = await _sttServiceManager.StartAsync();
            if (started)
            {
                _logger.LogInformation("SpeechToText service started successfully");
                // Wait a moment for service to fully start
                await Task.Delay(1000);
                var isRunning = await _sttServiceManager.IsRunningAsync();
                _trayService.UpdateSpeechToTextStatus(isRunning, _sttServiceManager.GetVersion());
                _trayService.SetIcon("push-to-talk");
                _trayService.SetTooltip("Push To Talk");
            }
            else
            {
                _logger.LogError("Failed to start SpeechToText service");
            }
        };
    }

    private void CheckSpeechToTextServiceStatusOnStartup()
    {
        // Check SpeechToText service status on startup
        Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Checking SpeechToText service status...");
                var isRunning = await _sttServiceManager.IsRunningAsync();
                var version = _sttServiceManager.GetVersion();

                if (!isRunning)
                {
                    _logger.LogInformation("SpeechToText service is not running, attempting to start...");
                    var started = await _sttServiceManager.StartAsync();
                    if (started)
                    {
                        // Wait a moment for service to fully start
                        await Task.Delay(1000);
                        isRunning = await _sttServiceManager.IsRunningAsync();
                    }
                }

                _logger.LogInformation("SpeechToText service status: {Status}, version: {Version}",
                    isRunning ? "Running" : "Stopped", version);
                _trayService.UpdateSpeechToTextStatus(isRunning, version);

                // Set icon based on service status
                if (isRunning)
                {
                    _trayService.SetIcon("push-to-talk");
                    _trayService.SetTooltip("Push To Talk");
                }
                else
                {
                    _trayService.SetIcon("push-to-talk-off");
                    _trayService.SetTooltip("Push To Talk (Service Stopped)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check/start SpeechToText service");
                _trayService.UpdateSpeechToTextStatus(false, "Unknown");
                _trayService.SetIcon("push-to-talk-off");
                _trayService.SetTooltip("Push To Talk (Service Error)");
            }
        }).FireAndForget(_logger, "SttServiceCheck");
    }
}
