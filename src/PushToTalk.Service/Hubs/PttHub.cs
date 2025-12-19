using Microsoft.AspNetCore.SignalR;
using Olbrasoft.PushToTalk.Core.Interfaces;
using Olbrasoft.PushToTalk.Service.Models;
using Olbrasoft.PushToTalk.Service.Services;

namespace Olbrasoft.PushToTalk.Service.Hubs;

/// <summary>
/// SignalR hub for real-time Push-to-Talk dictation events.
/// Provides WebSocket endpoint for clients to receive PTT notifications.
/// </summary>
public class PttHub : Hub
{
    private readonly ILogger<PttHub> _logger;
    private readonly ManualMuteService _manualMuteService;
    private readonly IPttNotifier _pttNotifier;
    private readonly IRecordingStateProvider _stateProvider;
    private readonly IRecordingController _recordingController;
    private readonly ITextTyper _textTyper;

    /// <summary>
    /// Initializes a new instance of the <see cref="PttHub"/> class.
    /// </summary>
    /// <param name="logger">Logger for connection events.</param>
    /// <param name="manualMuteService">Service for managing manual mute state.</param>
    /// <param name="pttNotifier">Notifier for broadcasting PTT events.</param>
    /// <param name="stateProvider">Provider for recording state.</param>
    /// <param name="recordingController">Controller for recording operations.</param>
    /// <param name="textTyper">Text typer for simulating keyboard input.</param>
    public PttHub(
        ILogger<PttHub> logger,
        ManualMuteService manualMuteService,
        IPttNotifier pttNotifier,
        IRecordingStateProvider stateProvider,
        IRecordingController recordingController,
        ITextTyper textTyper)
    {
        _logger = logger;
        _manualMuteService = manualMuteService;
        _pttNotifier = pttNotifier;
        _stateProvider = stateProvider;
        _recordingController = recordingController;
        _textTyper = textTyper;
    }
    
    /// <summary>
    /// Called when a client connects to the hub.
    /// Sends a connection confirmation message to the client.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }
    
    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    /// <param name="exception">Exception that caused the disconnection, if any.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
    
    /// <summary>
    /// Allows a client to subscribe to PTT events with a custom name.
    /// </summary>
    /// <param name="clientName">Name of the subscribing client.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Subscribe(string clientName)
    {
        _logger.LogInformation("Client {ClientName} subscribed", clientName);
        await Clients.Caller.SendAsync("Subscribed", clientName);
    }
    
    /// <summary>
    /// Toggles the manual mute state and broadcasts the change to all clients.
    /// Called from clients (e.g., tray menu) to toggle mute.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ToggleManualMute()
    {
        _logger.LogInformation("Client {ConnectionId} requested ToggleManualMute", Context.ConnectionId);
        
        // Toggle internal state and get new value
        var newMuteState = _manualMuteService.Toggle();
        
        _logger.LogInformation("ManualMute toggled to: {State}", newMuteState ? "MUTED" : "UNMUTED");
        
        // Broadcast to all clients
        await _pttNotifier.NotifyManualMuteChangedAsync(newMuteState);
    }
    
    /// <summary>
    /// Gets the current manual mute state (ScrollLock LED state).
    /// </summary>
    /// <returns>True if muted (ScrollLock ON), false if unmuted.</returns>
    public Task<bool> GetManualMuteState()
    {
        return Task.FromResult(_manualMuteService.IsMuted);
    }

    // Recording control methods for remote clients

    /// <summary>
    /// Gets the current recording status.
    /// </summary>
    /// <returns>Current recording state including isRecording, isTranscribing, and duration.</returns>
    public Task<StatusResponse> GetStatus()
    {
        _logger.LogDebug("Client {ConnectionId} requested status", Context.ConnectionId);

        return Task.FromResult(new StatusResponse
        {
            IsRecording = _stateProvider.IsRecording,
            IsTranscribing = _stateProvider.IsTranscribing,
            RecordingDurationSeconds = _stateProvider.RecordingDuration?.TotalSeconds
        });
    }

    /// <summary>
    /// Starts recording audio remotely.
    /// </summary>
    /// <returns>True if recording was started, false if already recording.</returns>
    public async Task<bool> StartRecording()
    {
        _logger.LogInformation("Client {ConnectionId} requested StartRecording", Context.ConnectionId);
        return await _recordingController.StartRecordingRemoteAsync();
    }

    /// <summary>
    /// Stops recording audio remotely.
    /// </summary>
    /// <returns>True if recording was stopped, false if not recording.</returns>
    public async Task<bool> StopRecording()
    {
        _logger.LogInformation("Client {ConnectionId} requested StopRecording", Context.ConnectionId);
        return await _recordingController.StopRecordingRemoteAsync();
    }

    /// <summary>
    /// Toggles recording state - starts if not recording, stops if recording.
    /// </summary>
    /// <returns>True if now recording, false if now stopped.</returns>
    public async Task<bool> ToggleRecording()
    {
        _logger.LogInformation("Client {ConnectionId} requested ToggleRecording", Context.ConnectionId);
        return await _recordingController.ToggleRecordingAsync();
    }

    /// <summary>
    /// Simulates pressing the Enter key.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task PressEnter()
    {
        _logger.LogInformation("Client {ConnectionId} requested PressEnter", Context.ConnectionId);

        if (!_textTyper.IsAvailable)
        {
            _logger.LogWarning("TextTyper is not available for PressEnter");
            throw new InvalidOperationException("TextTyper is not available");
        }

        await _textTyper.SendKeyAsync("enter");
        _logger.LogDebug("Enter key pressed successfully");
    }

    /// <summary>
    /// Simulates pressing Ctrl+C to clear text from prompt.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ClearText()
    {
        _logger.LogInformation("Client {ConnectionId} requested ClearText", Context.ConnectionId);

        if (!_textTyper.IsAvailable)
        {
            _logger.LogWarning("TextTyper is not available for ClearText");
            throw new InvalidOperationException("TextTyper is not available");
        }

        await _textTyper.SendKeyAsync("ctrl+c");
        _logger.LogDebug("Ctrl+C pressed successfully");
    }

    /// <summary>
    /// Cancels ongoing transcription (simulates Escape key press).
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task CancelTranscription()
    {
        _logger.LogInformation("Client {ConnectionId} requested CancelTranscription", Context.ConnectionId);
        _recordingController.CancelTranscription();
        return Task.CompletedTask;
    }
}
