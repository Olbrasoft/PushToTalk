using Microsoft.Extensions.Logging;
using Olbrasoft.PushToTalk.Audio;
using Olbrasoft.PushToTalk.Core.Interfaces;

namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Handles text output including typing, history management, and sound feedback.
/// Combines text typing, transcription history, and sound playback into a single service.
/// </summary>
public class TextOutputService : ITextOutputService
{
    private readonly ILogger<TextOutputService> _logger;
    private readonly ITextTyper _textTyper;
    private readonly ITranscriptionHistory _transcriptionHistory;
    private readonly TypingSoundPlayer _typingSoundPlayer;

    public TextOutputService(
        ILogger<TextOutputService> logger,
        ITextTyper textTyper,
        ITranscriptionHistory transcriptionHistory,
        TypingSoundPlayer typingSoundPlayer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _textTyper = textTyper ?? throw new ArgumentNullException(nameof(textTyper));
        _transcriptionHistory = transcriptionHistory ?? throw new ArgumentNullException(nameof(transcriptionHistory));
        _typingSoundPlayer = typingSoundPlayer ?? throw new ArgumentNullException(nameof(typingSoundPlayer));
    }

    /// <inheritdoc />
    public string? LastText => _transcriptionHistory.LastText;

    /// <inheritdoc />
    public async Task OutputTextAsync(string text, CancellationToken cancellationToken = default)
    {
        // Save to history before typing (allows repeat if pasted to wrong window)
        _transcriptionHistory.SaveText(text);
        _logger.LogDebug("Transcription saved to history");

        // Type transcribed text
        await _textTyper.TypeTextAsync(text, cancellationToken);
        _logger.LogInformation("Text typed successfully");
    }

    /// <inheritdoc />
    public async Task PlayRejectionSoundAsync()
    {
        await _typingSoundPlayer.PlayTearPaperAsync();
    }
}
