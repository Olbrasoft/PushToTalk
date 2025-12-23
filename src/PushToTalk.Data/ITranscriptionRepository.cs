using PushToTalk.Data.Entities;

namespace PushToTalk.Data;

/// <summary>
/// Repository for managing Whisper transcription records.
/// </summary>
public interface ITranscriptionRepository
{
    /// <summary>
    /// Saves a new Whisper transcription record.
    /// </summary>
    /// <param name="text">The transcribed text from Whisper.</param>
    /// <param name="sourceApp">The application that had focus during dictation (optional).</param>
    /// <param name="durationMs">The audio recording duration in milliseconds (optional).</param>
    /// <param name="modelName">The Whisper model used (optional).</param>
    /// <param name="language">The language code (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved transcription entity.</returns>
    Task<WhisperTranscription> SaveAsync(
        string text,
        string? sourceApp = null,
        int? durationMs = null,
        string? modelName = null,
        string? language = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the most recent transcriptions.
    /// </summary>
    /// <param name="count">Maximum number of transcriptions to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of recent transcriptions ordered by creation date descending.</returns>
    Task<IReadOnlyList<WhisperTranscription>> GetRecentAsync(int count = 50, CancellationToken ct = default);

    /// <summary>
    /// Searches for transcriptions containing the specified query.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching transcriptions.</returns>
    Task<IReadOnlyList<WhisperTranscription>> SearchAsync(string query, CancellationToken ct = default);
}
