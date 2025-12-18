namespace Olbrasoft.PushToTalk.App.Services;

/// <summary>
/// Client for communicating with VirtualAssistant service.
/// Used for TTS coordination during recording.
/// </summary>
public interface IVirtualAssistantClient
{
    /// <summary>
    /// Notifies VirtualAssistant that recording has started.
    /// Stops current TTS playback and prevents new speech until stopped.
    /// </summary>
    Task NotifyRecordingStartedAsync(CancellationToken ct = default);

    /// <summary>
    /// Notifies VirtualAssistant that recording has stopped.
    /// Unlocks TTS and flushes any queued messages.
    /// </summary>
    Task NotifyRecordingStoppedAsync(CancellationToken ct = default);
}
