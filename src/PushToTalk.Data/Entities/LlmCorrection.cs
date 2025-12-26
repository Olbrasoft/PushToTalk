namespace PushToTalk.Data.Entities;

/// <summary>
/// Represents an LLM correction attempt for a Whisper transcription.
/// </summary>
public class LlmCorrection
{
    /// <summary>
    /// Unique identifier for this correction.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to WhisperTranscription (original text is stored there - normalization).
    /// </summary>
    public int WhisperTranscriptionId { get; set; }

    /// <summary>
    /// Model name (e.g., 'mistral-large-latest').
    /// </summary>
    public string ModelName { get; set; } = "mistral-large-latest";

    /// <summary>
    /// Provider name ('mistral').
    /// </summary>
    public string Provider { get; set; } = "mistral";

    /// <summary>
    /// Corrected text returned by LLM. NULL if correction failed.
    /// </summary>
    public string? CorrectedText { get; set; }

    /// <summary>
    /// API call duration in milliseconds.
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// TRUE if correction succeeded, FALSE if failed (network error, rate limit, etc.).
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if correction failed (e.g., "Rate limit exceeded", "Network timeout").
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When this correction was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to the original Whisper transcription.
    /// </summary>
    public WhisperTranscription WhisperTranscription { get; set; } = null!;
}
