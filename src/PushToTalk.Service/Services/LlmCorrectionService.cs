using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Olbrasoft.PushToTalk.Core.Interfaces;
using PushToTalk.Data.Entities;
using PushToTalk.Data.EntityFrameworkCore;

namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Service for correcting Whisper transcriptions using LLM with circuit breaker pattern.
/// </summary>
public class LlmCorrectionService : ILlmCorrectionService
{
    private readonly ILlmProvider _llmProvider;
    private readonly PushToTalkDbContext _dbContext;
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly INotificationClient _notificationClient;
    private readonly ILogger<LlmCorrectionService> _logger;

    private const int MinTextLength = 30;
    private const int CircuitBreakerFailureThreshold = 3;
    private const int CircuitBreakerRetryMinutes = 5;

    public LlmCorrectionService(
        ILlmProvider llmProvider,
        PushToTalkDbContext dbContext,
        IEmailNotificationService emailNotificationService,
        INotificationClient notificationClient,
        ILogger<LlmCorrectionService> logger)
    {
        _llmProvider = llmProvider;
        _dbContext = dbContext;
        _emailNotificationService = emailNotificationService;
        _notificationClient = notificationClient;
        _logger = logger;
    }

    public async Task<string> CorrectTranscriptionAsync(int transcriptionId, string text, CancellationToken cancellationToken = default)
    {
        // Skip short texts
        if (text.Length < MinTextLength)
        {
            _logger.LogDebug("Skipping LLM correction for text shorter than {MinLength} characters: {Length}",
                MinTextLength, text.Length);
            return text;
        }

        // Check circuit breaker state
        var circuitState = await GetOrCreateCircuitBreakerStateAsync(_llmProvider.ProviderName, cancellationToken);

        if (circuitState.IsOpen)
        {
            var timeSinceOpened = DateTime.UtcNow - circuitState.OpenedAt!.Value;

            if (timeSinceOpened < TimeSpan.FromMinutes(CircuitBreakerRetryMinutes))
            {
                _logger.LogWarning("Circuit breaker is OPEN for {Provider}. Skipping LLM correction. Time since opened: {TimeSince}",
                    _llmProvider.ProviderName, timeSinceOpened);
                return text;
            }

            // Retry after timeout
            _logger.LogInformation("Circuit breaker retry timeout elapsed for {Provider}. Attempting to close circuit.",
                _llmProvider.ProviderName);
        }

        // Attempt correction
        var stopwatch = Stopwatch.StartNew();
        string? correctedText = null;
        string? errorMessage = null;
        bool success = false;

        try
        {
            correctedText = await _llmProvider.CorrectTextAsync(text, cancellationToken);
            success = true;
            stopwatch.Stop();

            _logger.LogInformation("LLM correction succeeded for transcription {TranscriptionId}. Duration: {Duration}ms",
                transcriptionId, stopwatch.ElapsedMilliseconds);

            // Close circuit breaker on success
            await CloseCircuitBreakerAsync(circuitState, cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            success = false;
            errorMessage = ex.Message;

            _logger.LogError(ex, "LLM correction failed for transcription {TranscriptionId}: {Error}",
                transcriptionId, ex.Message);

            // Open circuit breaker on failure
            await HandleFailureAsync(circuitState, errorMessage, cancellationToken);

            // Return original text on failure
            correctedText = text;
        }

        // Save correction record
        var correction = new LlmCorrection
        {
            WhisperTranscriptionId = transcriptionId,
            ModelName = _llmProvider.ModelName,
            Provider = _llmProvider.ProviderName,
            CorrectedText = success ? correctedText : null,
            DurationMs = (int)stopwatch.ElapsedMilliseconds,
            Success = success,
            ErrorMessage = errorMessage,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.LlmCorrections.Add(correction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return correctedText ?? text;
    }

    public async Task<bool> IsCircuitOpenAsync(string providerName)
    {
        var state = await _dbContext.CircuitBreakerStates
            .FirstOrDefaultAsync(s => s.Provider == providerName);

        return state?.IsOpen ?? false;
    }

    private async Task<CircuitBreakerState> GetOrCreateCircuitBreakerStateAsync(string providerName, CancellationToken cancellationToken)
    {
        var state = await _dbContext.CircuitBreakerStates
            .FirstOrDefaultAsync(s => s.Provider == providerName, cancellationToken);

        if (state == null)
        {
            state = new CircuitBreakerState
            {
                Provider = providerName,
                IsOpen = false,
                ConsecutiveFailures = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.CircuitBreakerStates.Add(state);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return state;
    }

    private async Task HandleFailureAsync(CircuitBreakerState state, string errorMessage, CancellationToken cancellationToken)
    {
        state.ConsecutiveFailures++;
        state.UpdatedAt = DateTime.UtcNow;

        if (state.ConsecutiveFailures >= CircuitBreakerFailureThreshold && !state.IsOpen)
        {
            // Open circuit
            state.IsOpen = true;
            state.OpenedAt = DateTime.UtcNow;

            _logger.LogWarning("Circuit breaker OPENED for {Provider} after {Failures} consecutive failures",
                state.Provider, state.ConsecutiveFailures);

            // Send notifications
            await Task.WhenAll(
                _emailNotificationService.SendCircuitOpenedNotificationAsync(
                    state.Provider, state.ConsecutiveFailures, errorMessage, cancellationToken),
                _notificationClient.SendNotificationAsync(
                    $"Circuit breaker se otevřel pro {state.Provider} po {state.ConsecutiveFailures} selháních. LLM korekce jsou dočasně pozastaveny.",
                    cancellationToken)
            );
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CloseCircuitBreakerAsync(CircuitBreakerState state, CancellationToken cancellationToken)
    {
        if (!state.IsOpen && state.ConsecutiveFailures == 0)
        {
            return; // Already closed
        }

        var wasOpen = state.IsOpen;

        state.IsOpen = false;
        state.OpenedAt = null;
        state.ConsecutiveFailures = 0;
        state.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (wasOpen)
        {
            _logger.LogInformation("Circuit breaker CLOSED for {Provider}", state.Provider);

            // Send notifications
            await Task.WhenAll(
                _emailNotificationService.SendCircuitClosedNotificationAsync(state.Provider, cancellationToken),
                _notificationClient.SendNotificationAsync(
                    $"Circuit breaker se uzavřel pro {state.Provider}. LLM korekce jsou opět aktivní.",
                    cancellationToken)
            );
        }
    }
}
