using Olbrasoft.Data.Entities.Abstractions;

namespace PushToTalk.Data.Entities;

/// <summary>
/// Represents a Whisper AI transcription from push-to-talk recording.
/// </summary>
public class WhisperTranscription : BaseEnity
{
    /// <summary>
    /// Gets or sets the transcribed text from Whisper AI.
    /// </summary>
    public required string TranscribedText { get; set; }

    /// <summary>
    /// Gets or sets the application that had focus during dictation (e.g., "code", "firefox").
    /// </summary>
    public string? SourceApplication { get; set; }

    /// <summary>
    /// Gets or sets the audio recording duration in milliseconds.
    /// </summary>
    public int? AudioDurationMs { get; set; }

    /// <summary>
    /// Gets or sets the Whisper model used (e.g., "ggml-large-v3-turbo").
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Gets or sets the language code (e.g., "cs", "en").
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets when the transcription was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
