namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Filters out common Whisper hallucinations from transcription results.
/// Whisper model sometimes hallucinates common phrases when given silent or very short audio.
/// </summary>
public interface IHallucinationFilter
{
    /// <summary>
    /// Validates and cleans transcription text by removing known hallucinations.
    /// </summary>
    /// <param name="text">Raw transcription text from Whisper.</param>
    /// <param name="cleanedText">Output text with hallucinations removed.</param>
    /// <returns>True if valid text remains after cleaning, false if text is empty or entirely hallucination.</returns>
    bool TryClean(string? text, out string cleanedText);
}
