namespace PushToTalk.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for CircuitBreakerState entity with PostgreSQL snake_case naming.
/// </summary>
public class CircuitBreakerStateConfiguration : IEntityTypeConfiguration<CircuitBreakerState>
{
    public void Configure(EntityTypeBuilder<CircuitBreakerState> builder)
    {
        builder.ToTable("circuit_breaker_states");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.IsOpen)
            .HasColumnName("is_open")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.OpenedAt)
            .HasColumnName("opened_at");

        builder.Property(s => s.ConsecutiveFailures)
            .HasColumnName("consecutive_failures")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Single record in database (ID=1) for Mistral circuit breaker
        // No indexes needed for single-record table
    }
}
