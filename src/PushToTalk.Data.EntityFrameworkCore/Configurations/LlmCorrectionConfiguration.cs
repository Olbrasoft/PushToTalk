namespace PushToTalk.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for LlmCorrection entity with PostgreSQL snake_case naming.
/// </summary>
public class LlmCorrectionConfiguration : IEntityTypeConfiguration<LlmCorrection>
{
    public void Configure(EntityTypeBuilder<LlmCorrection> builder)
    {
        builder.ToTable("llm_corrections");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id");

        builder.Property(c => c.WhisperTranscriptionId)
            .HasColumnName("whisper_transcription_id")
            .IsRequired();

        builder.Property(c => c.ModelName)
            .HasColumnName("model_name")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Provider)
            .HasColumnName("provider")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.CorrectedText)
            .HasColumnName("corrected_text");

        builder.Property(c => c.DurationMs)
            .HasColumnName("duration_ms")
            .IsRequired();

        builder.Property(c => c.Success)
            .HasColumnName("success")
            .IsRequired();

        builder.Property(c => c.ErrorMessage)
            .HasColumnName("error_message");

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Foreign key relationship
        builder.HasOne(c => c.WhisperTranscription)
            .WithMany()
            .HasForeignKey(c => c.WhisperTranscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common queries
        builder.HasIndex(c => c.WhisperTranscriptionId);
        builder.HasIndex(c => c.CreatedAt);
        builder.HasIndex(c => new { c.Provider, c.Success });
    }
}
