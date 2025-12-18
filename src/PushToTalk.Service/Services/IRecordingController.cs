namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Provides control over recording operations.
/// Used by API endpoints and SignalR hub to remotely start/stop recording.
/// </summary>
public interface IRecordingController
{
    /// <summary>
    /// Starts recording audio. Does nothing if already recording.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if recording was started, false if already recording.</returns>
    Task<bool> StartRecordingRemoteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops recording audio. Does nothing if not recording.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if recording was stopped, false if not recording.</returns>
    Task<bool> StopRecordingRemoteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles recording state - starts if not recording, stops if recording.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if now recording, false if now stopped.</returns>
    Task<bool> ToggleRecordingAsync(CancellationToken cancellationToken = default);
}
