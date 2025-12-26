namespace PushToTalk.Data.Entities;

/// <summary>
/// Tracks circuit breaker state for LLM providers.
/// Prevents cascading failures by opening circuit after consecutive errors.
/// </summary>
public class CircuitBreakerState
{
    /// <summary>
    /// Unique identifier for this circuit breaker state.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Provider name ('mistral', 'groq', 'cerebras').
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Is the circuit currently open (blocking requests)?
    /// </summary>
    public bool IsOpen { get; set; } = false;

    /// <summary>
    /// When the circuit was opened (UTC).
    /// NULL if circuit is closed.
    /// </summary>
    public DateTime? OpenedAt { get; set; }

    /// <summary>
    /// Number of consecutive failures before circuit opened.
    /// Reset to 0 when circuit closes or request succeeds.
    /// </summary>
    public int ConsecutiveFailures { get; set; } = 0;

    /// <summary>
    /// When this record was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
