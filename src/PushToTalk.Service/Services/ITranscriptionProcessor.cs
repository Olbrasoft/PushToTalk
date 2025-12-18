namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Processes audio transcription including hallucination filtering.
/// Combines speech transcription and hallucination detection into a single service.
/// </summary>
public interface ITranscriptionProcessor
{
    /// <summary>
    /// Transcribes audio data and filters out hallucinations.
    /// </summary>
    /// <param name="audioData">Audio data in WAV format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cleaned transcription text, or null if failed or filtered as hallucination.</returns>
    Task<TranscriptionProcessorResult> ProcessAsync(byte[] audioData, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of transcription processing.
/// </summary>
public record TranscriptionProcessorResult(
    bool Success,
    string? Text,
    float Confidence,
    bool WasHallucination,
    string? ErrorMessage);
