namespace PushToTalk.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for LlmApiKey entity with PostgreSQL snake_case naming.
/// </summary>
public class LlmApiKeyConfiguration : IEntityTypeConfiguration<LlmApiKey>
{
    public void Configure(EntityTypeBuilder<LlmApiKey> builder)
    {
        builder.ToTable("llm_api_keys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id)
            .HasColumnName("id");

        builder.Property(k => k.Provider)
            .HasColumnName("provider")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(k => k.KeyHash)
            .HasColumnName("key_hash")
            .IsRequired()
            .HasMaxLength(64); // SHA256 hash length

        builder.Property(k => k.Label)
            .HasColumnName("label")
            .HasMaxLength(200);

        builder.Property(k => k.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(k => k.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.Property(k => k.RateLimitHitCount)
            .HasColumnName("rate_limit_hit_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(k => k.KeyCreatedAt)
            .HasColumnName("key_created_at");

        builder.Property(k => k.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(k => k.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Unique constraint on provider + key hash
        builder.HasIndex(k => new { k.Provider, k.KeyHash })
            .IsUnique();

        // Index for active keys
        builder.HasIndex(k => new { k.Provider, k.IsActive });
    }
}
