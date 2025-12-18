namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Manages recording mode state including TTS coordination and speech locks.
/// Combines TTS control and speech lock services into a single service.
/// </summary>
public interface IRecordingModeManager
{
    /// <summary>
    /// Enters recording mode: stops TTS, creates speech lock, saves mute state.
    /// </summary>
    /// <returns>Context with previous mute state for restoration.</returns>
    Task<RecordingModeContext> EnterRecordingModeAsync();

    /// <summary>
    /// Exits recording mode: releases speech lock, restores mute state, flushes TTS queue.
    /// </summary>
    /// <param name="context">Context from EnterRecordingModeAsync.</param>
    Task ExitRecordingModeAsync(RecordingModeContext context);
}

/// <summary>
/// Context for recording mode state restoration.
/// </summary>
public record RecordingModeContext(bool? PreviousMuteState);
