namespace PushToTalk.Data.Entities;

/// <summary>
/// Stores SMTP email configuration for circuit breaker notifications.
/// </summary>
public class Email
{
    /// <summary>
    /// Unique identifier for this email configuration.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// SMTP server address (e.g., 'smtp.seznam.cz').
    /// </summary>
    public string SmtpServer { get; set; } = string.Empty;

    /// <summary>
    /// SMTP server port (e.g., 587 for TLS, 465 for SSL).
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Whether to use SSL/TLS encryption.
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// From email address (e.g., 'olbrasoft@email.cz').
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>
    /// SMTP password for authentication.
    /// SECURITY: Stored in database, never in config files.
    /// </summary>
    public string FromPassword { get; set; } = string.Empty;

    /// <summary>
    /// Recipient email address for notifications.
    /// </summary>
    public string ToEmail { get; set; } = string.Empty;

    /// <summary>
    /// Is this email configuration currently active?
    /// Only one configuration should be active at a time.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional label for this configuration (e.g., 'Circuit breaker notifications').
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// When this configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
