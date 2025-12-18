namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Provides read-only access to the current recording state.
/// Used by API endpoints and SignalR hub to query recording status.
/// </summary>
public interface IRecordingStateProvider
{
    /// <summary>
    /// Gets a value indicating whether audio recording is currently in progress.
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// Gets a value indicating whether Whisper transcription is currently in progress.
    /// </summary>
    bool IsTranscribing { get; }

    /// <summary>
    /// Gets the duration of the current recording, or null if not recording.
    /// </summary>
    TimeSpan? RecordingDuration { get; }
}
