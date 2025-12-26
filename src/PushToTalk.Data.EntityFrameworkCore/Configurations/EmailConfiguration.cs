namespace PushToTalk.Data.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core configuration for Email entity with PostgreSQL snake_case naming.
/// </summary>
public class EmailConfiguration : IEntityTypeConfiguration<Email>
{
    public void Configure(EntityTypeBuilder<Email> builder)
    {
        builder.ToTable("emails");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.SmtpServer)
            .HasColumnName("smtp_server")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.SmtpPort)
            .HasColumnName("smtp_port")
            .IsRequired()
            .HasDefaultValue(587);

        builder.Property(e => e.UseSsl)
            .HasColumnName("use_ssl")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.FromEmail)
            .HasColumnName("from_email")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.FromPassword)
            .HasColumnName("from_password")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.ToEmail)
            .HasColumnName("to_email")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.Label)
            .HasColumnName("label")
            .HasMaxLength(200);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Index for finding active email configuration
        builder.HasIndex(e => e.IsActive);
    }
}
