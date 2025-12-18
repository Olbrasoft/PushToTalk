using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Filters out common Whisper hallucinations from transcription results.
/// Whisper model sometimes hallucinates common phrases when given silent or very short audio.
/// </summary>
public partial class WhisperHallucinationFilter : IHallucinationFilter
{
    private readonly ILogger<WhisperHallucinationFilter> _logger;
    private readonly HallucinationFilterOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhisperHallucinationFilter"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="options">Filter configuration options.</param>
    public WhisperHallucinationFilter(
        ILogger<WhisperHallucinationFilter> logger,
        IOptions<HallucinationFilterOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public bool TryClean(string? text, out string cleanedText)
    {
        cleanedText = string.Empty;

        // Empty or whitespace
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogDebug("Transcription validation failed: empty or whitespace");
            return false;
        }

        var result = text.Trim();

        // Remove all known hallucinations from the text (case-insensitive)
        foreach (var hallucination in _options.Patterns)
        {
            var index = result.IndexOf(hallucination, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                _logger.LogDebug("Removing hallucination '{Hallucination}' from position {Index}", hallucination, index);
                result = result.Remove(index, hallucination.Length);
                index = result.IndexOf(hallucination, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Clean up any resulting double spaces or leading/trailing whitespace
        result = MultipleSpacesRegex().Replace(result, " ").Trim();

        // Check if anything remains after cleaning
        if (string.IsNullOrWhiteSpace(result))
        {
            _logger.LogInformation("Transcription was entirely hallucination, nothing remains after cleaning");
            return false;
        }

        // Too short (likely noise)
        if (result.Length < _options.MinimumTextLength)
        {
            _logger.LogDebug("Transcription validation failed: too short ({Length} chars) after cleaning", result.Length);
            return false;
        }

        cleanedText = result;
        return true;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpacesRegex();
}
