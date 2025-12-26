using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.PushToTalk.Core.Interfaces;
using PushToTalk.Data;

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
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public TranscriptionProcessor(
        ILogger<TranscriptionProcessor> logger,
        ISpeechTranscriber speechTranscriber,
        IHallucinationFilter hallucinationFilter,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _speechTranscriber = speechTranscriber ?? throw new ArgumentNullException(nameof(speechTranscriber));
        _hallucinationFilter = hallucinationFilter ?? throw new ArgumentNullException(nameof(hallucinationFilter));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
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

        // Save transcription to database and run LLM correction (background task, don't block)
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ITranscriptionRepository>();
                var llmCorrectionService = scope.ServiceProvider.GetRequiredService<ILlmCorrectionService>();

                // Save Whisper transcription first
                var transcription = await repository.SaveAsync(
                    text: cleanedText,
                    durationMs: null, // Duration not available in Service
                    ct: CancellationToken.None); // Use None since this is background task

                _logger.LogDebug("Transcription saved to database with ID: {TranscriptionId}", transcription.Id);

                // Run LLM correction (async, non-blocking for dictation workflow)
                var correctedText = await llmCorrectionService.CorrectTranscriptionAsync(
                    transcription.Id,
                    cleanedText,
                    CancellationToken.None);

                if (correctedText != cleanedText)
                {
                    _logger.LogInformation("LLM corrected text from '{Original}' to '{Corrected}'",
                        cleanedText, correctedText);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save transcription or run LLM correction");
            }
        }, cancellationToken);

        return new TranscriptionProcessorResult(
            Success: true,
            Text: cleanedText,
            Confidence: transcription.Confidence,
            WasHallucination: false,
            ErrorMessage: null);
    }
}
