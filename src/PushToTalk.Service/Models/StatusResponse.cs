namespace Olbrasoft.PushToTalk.Service.Models;

/// <summary>
/// Response model for the status endpoint.
/// </summary>
public record StatusResponse
{
    /// <summary>
    /// Gets a value indicating whether audio recording is currently in progress.
    /// </summary>
    public bool IsRecording { get; init; }

    /// <summary>
    /// Gets a value indicating whether Whisper transcription is currently in progress.
    /// </summary>
    public bool IsTranscribing { get; init; }

    /// <summary>
    /// Gets the duration of the current recording in seconds, or null if not recording.
    /// </summary>
    public double? RecordingDurationSeconds { get; init; }
}
