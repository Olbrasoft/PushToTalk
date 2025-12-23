namespace PushToTalk.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for WhisperTranscription entity with PostgreSQL snake_case naming.
/// </summary>
public class WhisperTranscriptionConfiguration : IEntityTypeConfiguration<WhisperTranscription>
{
    public void Configure(EntityTypeBuilder<WhisperTranscription> builder)
    {
        builder.ToTable("whisper_transcriptions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id");

        builder.Property(t => t.TranscribedText)
            .HasColumnName("transcribed_text")
            .IsRequired();

        builder.Property(t => t.SourceApplication)
            .HasColumnName("source_application")
            .HasMaxLength(255);

        builder.Property(t => t.AudioDurationMs)
            .HasColumnName("audio_duration_ms");

        builder.Property(t => t.ModelName)
            .HasColumnName("model_name")
            .HasMaxLength(100);

        builder.Property(t => t.Language)
            .HasColumnName("language")
            .HasMaxLength(10);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(t => t.CreatedAt);
    }
}
