namespace Olbrasoft.PushToTalk.Core.Interfaces;

/// <summary>
/// Service for correcting Whisper transcriptions using LLM with circuit breaker pattern.
/// </summary>
public interface ILlmCorrectionService
{
    /// <summary>
    /// Corrects the transcription using LLM.
    /// Returns original text if circuit is open or text is too short.
    /// </summary>
    /// <param name="transcriptionId">Whisper transcription ID</param>
    /// <param name="text">Original transcribed text</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Corrected text (or original if correction failed/skipped)</returns>
    Task<string> CorrectTranscriptionAsync(int transcriptionId, string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the circuit breaker is currently open for the given provider.
    /// </summary>
    /// <param name="providerName">Provider name</param>
    /// <returns>True if circuit is open (blocking requests)</returns>
    Task<bool> IsCircuitOpenAsync(string providerName);
}
