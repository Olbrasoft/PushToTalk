using PushToTalk.Data.Entities;

namespace PushToTalk.Data.EntityFrameworkCore;

/// <summary>
/// Database context for PushToTalk using PostgreSQL.
/// </summary>
public class PushToTalkDbContext : DbContext
{
    public PushToTalkDbContext(DbContextOptions<PushToTalkDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the WhisperTranscriptions DbSet.
    /// </summary>
    public DbSet<WhisperTranscription> WhisperTranscriptions => Set<WhisperTranscription>();

    /// <summary>
    /// Gets or sets the TranscriptionCorrections DbSet.
    /// Used for storing and managing ASR post-processing correction rules.
    /// </summary>
    public DbSet<TranscriptionCorrection> TranscriptionCorrections => Set<TranscriptionCorrection>();

    /// <summary>
    /// Gets or sets the LlmCorrections DbSet.
    /// Used for storing successful LLM corrections of Whisper transcriptions.
    /// </summary>
    public DbSet<LlmCorrection> LlmCorrections => Set<LlmCorrection>();

    /// <summary>
    /// Gets or sets the LlmErrors DbSet.
    /// Used for storing failed LLM correction attempts.
    /// </summary>
    public DbSet<LlmError> LlmErrors => Set<LlmError>();

    /// <summary>
    /// Gets or sets the MistralConfigs DbSet.
    /// Used for storing Mistral API configuration.
    /// </summary>
    public DbSet<MistralConfig> MistralConfigs => Set<MistralConfig>();

    /// <summary>
    /// Gets or sets the CircuitBreakerStates DbSet.
    /// Used for tracking circuit breaker state.
    /// </summary>
    public DbSet<CircuitBreakerState> CircuitBreakerStates => Set<CircuitBreakerState>();

    /// <summary>
    /// Gets or sets the Emails DbSet.
    /// Used for storing SMTP configuration for circuit breaker notifications.
    /// </summary>
    public DbSet<Email> Emails => Set<Email>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PushToTalkDbContext).Assembly);
    }
}
