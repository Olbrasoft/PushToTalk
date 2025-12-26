namespace Olbrasoft.PushToTalk.Core.Interfaces;

/// <summary>
/// Service for sending email notifications.
/// </summary>
public interface IEmailNotificationService
{
    /// <summary>
    /// Sends an email notification that the circuit breaker has opened.
    /// </summary>
    /// <param name="providerName">Provider name that failed</param>
    /// <param name="failureCount">Number of consecutive failures</param>
    /// <param name="lastError">Last error message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendCircuitOpenedNotificationAsync(string providerName, int failureCount, string lastError, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an email notification that the circuit breaker has closed.
    /// </summary>
    /// <param name="providerName">Provider name that recovered</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendCircuitClosedNotificationAsync(string providerName, CancellationToken cancellationToken = default);
}
