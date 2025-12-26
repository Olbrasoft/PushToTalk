namespace PushToTalk.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for MistralConfig entity with PostgreSQL snake_case naming.
/// </summary>
public class MistralConfigConfiguration : IEntityTypeConfiguration<MistralConfig>
{
    public void Configure(EntityTypeBuilder<MistralConfig> builder)
    {
        builder.ToTable("mistral_configs");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id");

        builder.Property(m => m.ApiKey)
            .HasColumnName("api_key")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(m => m.Model)
            .HasColumnName("model")
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue("mistral-large-latest");

        builder.Property(m => m.BaseUrl)
            .HasColumnName("base_url")
            .IsRequired()
            .HasMaxLength(200)
            .HasDefaultValue("https://api.mistral.ai");

        builder.Property(m => m.TimeoutSeconds)
            .HasColumnName("timeout_seconds")
            .IsRequired()
            .HasDefaultValue(30);

        builder.Property(m => m.MaxTokens)
            .HasColumnName("max_tokens")
            .IsRequired()
            .HasDefaultValue(1000);

        builder.Property(m => m.Temperature)
            .HasColumnName("temperature")
            .IsRequired()
            .HasDefaultValue(0.3);

        builder.Property(m => m.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(m => m.Label)
            .HasColumnName("label")
            .HasMaxLength(200);

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Index for querying active configuration
        builder.HasIndex(m => m.IsActive);
    }
}
