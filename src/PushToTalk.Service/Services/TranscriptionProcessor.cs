using Microsoft.Extensions.Logging;
using Olbrasoft.PushToTalk.Core.Interfaces;

namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Processes audio transcription including hallucination filtering.
/// Combines speech transcription and hallucination detection into a single service.
/// </summary>
public class TranscriptionProcessor : ITranscriptionProcessor
{
    private readonly ILogger<TranscriptionProcessor> _logger;
    private readonly ISpeechTranscriber _speechTranscriber;
    private readonly IHallucinationFilter _hallucinationFilter;

    public TranscriptionProcessor(
        ILogger<TranscriptionProcessor> logger,
        ISpeechTranscriber speechTranscriber,
        IHallucinationFilter hallucinationFilter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _speechTranscriber = speechTranscriber ?? throw new ArgumentNullException(nameof(speechTranscriber));
        _hallucinationFilter = hallucinationFilter ?? throw new ArgumentNullException(nameof(hallucinationFilter));
    }

    /// <inheritdoc />
    public async Task<TranscriptionProcessorResult> ProcessAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting transcription processing...");

        var transcription = await _speechTranscriber.TranscribeAsync(audioData, cancellationToken);

        if (!transcription.Success || string.IsNullOrWhiteSpace(transcription.Text))
        {
            var errorMessage = transcription.ErrorMessage ?? "Empty transcription result";
            _logger.LogWarning("Transcription failed or empty: {Error}", errorMessage);
            return new TranscriptionProcessorResult(
                Success: false,
                Text: null,
                Confidence: 0,
                WasHallucination: false,
                ErrorMessage: errorMessage);
        }

        // Filter hallucinations
        if (!_hallucinationFilter.TryClean(transcription.Text, out var cleanedText))
        {
            _logger.LogInformation("Whisper hallucination detected and filtered: '{Text}'", transcription.Text);
            return new TranscriptionProcessorResult(
                Success: false,
                Text: null,
                Confidence: transcription.Confidence,
                WasHallucination: true,
                ErrorMessage: "Whisper hallucination filtered");
        }

        _logger.LogInformation("Transcription successful: {Text} (confidence: {Confidence:F3})",
            cleanedText, transcription.Confidence);

        return new TranscriptionProcessorResult(
            Success: true,
            Text: cleanedText,
            Confidence: transcription.Confidence,
            WasHallucination: false,
            ErrorMessage: null);
    }
}
