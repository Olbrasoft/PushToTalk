namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Manages speech lock file to prevent TTS from speaking during recording.
/// When the lock file exists, TTS services should not speak.
/// </summary>
public interface ISpeechLockService
{
    /// <summary>
    /// Gets a value indicating whether the speech lock is currently active.
    /// </summary>
    bool IsLocked { get; }

    /// <summary>
    /// Creates the speech lock file to signal TTS to not speak.
    /// </summary>
    /// <param name="reason">Reason for creating the lock (for logging/debugging).</param>
    void CreateLock(string reason);

    /// <summary>
    /// Deletes the speech lock file to allow TTS to speak again.
    /// </summary>
    void ReleaseLock();
}
