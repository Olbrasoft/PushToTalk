namespace PushToTalk.Data.Entities;

/// <summary>
/// Stores Mistral API configuration.
/// </summary>
public class MistralConfig
{
    /// <summary>
    /// Unique identifier for this configuration.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Mistral API key.
    /// SECURITY: Stored in database, never in config files.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model name to use (e.g., 'mistral-large-latest').
    /// </summary>
    public string Model { get; set; } = "mistral-large-latest";

    /// <summary>
    /// API base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.mistral.ai";

    /// <summary>
    /// API timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum tokens in response.
    /// </summary>
    public int MaxTokens { get; set; } = 1000;

    /// <summary>
    /// Temperature for generation (0.0-1.0).
    /// </summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// Is this configuration currently active?
    /// Only one configuration should be active at a time.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional label for this configuration.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// When this configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
