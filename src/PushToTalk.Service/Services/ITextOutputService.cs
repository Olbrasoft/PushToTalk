namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Handles text output including typing, history management, and sound feedback.
/// Combines text typing, transcription history, and sound playback into a single service.
/// </summary>
public interface ITextOutputService
{
    /// <summary>
    /// Outputs the transcribed text by typing it and saving to history.
    /// </summary>
    /// <param name="text">Text to output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OutputTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Plays rejection sound (tear paper) to indicate failed/cancelled transcription.
    /// </summary>
    Task PlayRejectionSoundAsync();

    /// <summary>
    /// Gets the last transcribed text from history.
    /// </summary>
    string? LastText { get; }
}
