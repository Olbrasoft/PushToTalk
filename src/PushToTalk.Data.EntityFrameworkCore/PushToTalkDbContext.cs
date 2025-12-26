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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PushToTalkDbContext).Assembly);
    }
}
