using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PushToTalk.Data.Entities;

namespace PushToTalk.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// Entity Framework Core configuration for TranscriptionCorrection entity.
/// </summary>
public class TranscriptionCorrectionConfiguration : IEntityTypeConfiguration<TranscriptionCorrection>
{
    public void Configure(EntityTypeBuilder<TranscriptionCorrection> builder)
    {
        builder.ToTable("transcription_corrections");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IncorrectText)
            .HasColumnName("incorrect_text")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.CorrectText)
            .HasColumnName("correct_text")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.CaseSensitive)
            .HasColumnName("case_sensitive")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.Priority)
            .HasColumnName("priority")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.IncorrectText)
            .IsUnique()
            .HasDatabaseName("IX_transcription_corrections_incorrect_text");

        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("IX_transcription_corrections_is_active")
            .HasFilter("is_active = true");
    }
}
