namespace PushToTalk.Data.Entities;

/// <summary>
/// Tracks API keys for rotation and rate limit management.
/// NOTE: Actual keys are stored in configuration, this is just for tracking usage.
/// </summary>
public class LlmApiKey
{
    /// <summary>
    /// Unique identifier for this API key record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Provider name ('mistral', 'groq', 'cerebras').
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash of the API key (for identification, NOT the actual key).
    /// </summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Human-readable label (e.g., "Firefox account", "Edge account").
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Is this key currently active?
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last time this key was used.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Number of times rate limit was hit with this key.
    /// </summary>
    public int RateLimitHitCount { get; set; } = 0;

    /// <summary>
    /// When the API key was created on the provider's platform.
    /// NULL if unknown (keys created before this tracking was implemented).
    /// </summary>
    public DateTime? KeyCreatedAt { get; set; }

    /// <summary>
    /// When the API key expires (if applicable).
    /// NULL if no expiration set or unknown.
    /// Mistral supports optional expiration dates.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// When this database record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
