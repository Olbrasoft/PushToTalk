using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.PushToTalk.Core.Configuration;
using Olbrasoft.PushToTalk.Core.Interfaces;

namespace Olbrasoft.PushToTalk.Core.Services;

/// <summary>
/// Mistral AI provider for correcting Czech ASR transcriptions.
/// </summary>
public class MistralProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly MistralOptions _options;
    private readonly ILogger<MistralProvider> _logger;
    private readonly string _systemPrompt;
    private Dictionary<string, string> _lastRateLimitHeaders = new();

    public string ProviderName => "mistral";
    public string ModelName => _options.Model;

    public MistralProvider(
        HttpClient httpClient,
        IOptions<MistralOptions> options,
        IPromptLoader promptLoader,
        ILogger<MistralProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        _systemPrompt = promptLoader.LoadPrompt("MistralSystemPrompt");
    }

    public async Task<string> CorrectTextAsync(string text, CancellationToken cancellationToken = default)
    {
        // Skip LLM correction for short texts
        if (text.Length < _options.MinTextLengthForCorrection)
        {
            _logger.LogDebug("Skipping LLM correction - text length {Length} < {MinLength}",
                text.Length, _options.MinTextLengthForCorrection);
            return text;
        }

        try
        {
            var request = new
            {
                model = _options.Model,
                messages = new[]
                {
                    new { role = "system", content = _systemPrompt },
                    new { role = "user", content = text }
                },
                temperature = _options.Temperature,
                max_tokens = _options.MaxTokens
            };

            var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", request, cancellationToken);

            // Capture rate limit headers
            _lastRateLimitHeaders = new Dictionary<string, string>();
            foreach (var header in response.Headers.Where(h => h.Key.StartsWith("x-ratelimit-", StringComparison.OrdinalIgnoreCase)))
            {
                _lastRateLimitHeaders[header.Key] = string.Join(", ", header.Value);
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<MistralResponse>(cancellationToken);

            if (result?.Choices == null || result.Choices.Length == 0)
            {
                throw new InvalidOperationException("Mistral API returned empty response");
            }

            var correctedText = result.Choices[0].Message.Content.Trim();

            _logger.LogInformation("Mistral correction completed. Original length: {OriginalLength}, Corrected length: {CorrectedLength}",
                text.Length, correctedText.Length);

            return correctedText;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Mistral API: {Message}", ex.Message);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Mistral API request timeout");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Mistral API: {Message}", ex.Message);
            throw;
        }
    }

    public Dictionary<string, string> GetLastRateLimitHeaders()
    {
        return new Dictionary<string, string>(_lastRateLimitHeaders);
    }

    private class MistralResponse
    {
        public MistralChoice[] Choices { get; set; } = Array.Empty<MistralChoice>();
    }

    private class MistralChoice
    {
        public MistralMessage Message { get; set; } = new();
    }

    private class MistralMessage
    {
        public string Content { get; set; } = string.Empty;
    }
}
