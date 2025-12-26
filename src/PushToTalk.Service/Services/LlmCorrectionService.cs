using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Olbrasoft.PushToTalk.Core.Interfaces;
using PushToTalk.Data.Entities;
using PushToTalk.Data.EntityFrameworkCore;

namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Service for correcting Whisper transcriptions using Mistral LLM with circuit breaker pattern.
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
    private const int CircuitBreakerStateId = 1; // Single record in database

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
        var circuitState = await GetOrCreateCircuitBreakerStateAsync(cancellationToken);

        if (circuitState.IsOpen)
        {
            var timeSinceOpened = DateTime.UtcNow - circuitState.OpenedAt!.Value;

            if (timeSinceOpened < TimeSpan.FromMinutes(CircuitBreakerRetryMinutes))
            {
                _logger.LogWarning("Circuit breaker is OPEN. Skipping LLM correction. Time since opened: {TimeSince}",
                    timeSinceOpened);
                return text;
            }

            // Retry after timeout
            _logger.LogInformation("Circuit breaker retry timeout elapsed. Attempting to close circuit.");
        }

        // Attempt correction
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var correctedText = await _llmProvider.CorrectTextAsync(text, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation("LLM correction succeeded for transcription {TranscriptionId}. Duration: {Duration}ms",
                transcriptionId, stopwatch.ElapsedMilliseconds);

            // Save successful correction
            var correction = new LlmCorrection
            {
                WhisperTranscriptionId = transcriptionId,
                CorrectedText = correctedText,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.LlmCorrections.Add(correction);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Close circuit breaker on success
            await CloseCircuitBreakerAsync(circuitState, cancellationToken);

            return correctedText;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "LLM correction failed for transcription {TranscriptionId}: {Error}",
                transcriptionId, ex.Message);

            // Save error
            var error = new LlmError
            {
                WhisperTranscriptionId = transcriptionId,
                ErrorMessage = ex.Message,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.LlmErrors.Add(error);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Handle circuit breaker failure
            await HandleFailureAsync(circuitState, ex.Message, cancellationToken);

            // Return original text on failure
            return text;
        }
    }

    public async Task<bool> IsCircuitOpenAsync(string providerName)
    {
        var state = await _dbContext.CircuitBreakerStates
            .FirstOrDefaultAsync(s => s.Id == CircuitBreakerStateId);

        return state?.IsOpen ?? false;
    }

    private async Task<CircuitBreakerState> GetOrCreateCircuitBreakerStateAsync(CancellationToken cancellationToken)
    {
        var state = await _dbContext.CircuitBreakerStates
            .FirstOrDefaultAsync(s => s.Id == CircuitBreakerStateId, cancellationToken);

        if (state == null)
        {
            state = new CircuitBreakerState
            {
                Id = CircuitBreakerStateId,
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

            _logger.LogWarning("Circuit breaker OPENED after {Failures} consecutive failures",
                state.ConsecutiveFailures);

            // Send notifications
            await Task.WhenAll(
                _emailNotificationService.SendCircuitOpenedNotificationAsync(
                    _llmProvider.ProviderName, state.ConsecutiveFailures, errorMessage, cancellationToken),
                _notificationClient.SendNotificationAsync(
                    $"Circuit breaker se otevřel pro Mistral po {state.ConsecutiveFailures} selháních. LLM korekce jsou dočasně pozastaveny.",
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
            _logger.LogInformation("Circuit breaker CLOSED");

            // Send notifications
            await Task.WhenAll(
                _emailNotificationService.SendCircuitClosedNotificationAsync(_llmProvider.ProviderName, cancellationToken),
                _notificationClient.SendNotificationAsync(
                    "Circuit breaker se uzavřel pro Mistral. LLM korekce jsou opět aktivní.",
                    cancellationToken)
            );
        }
    }
}
