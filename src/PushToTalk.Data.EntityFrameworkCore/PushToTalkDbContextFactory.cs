using Microsoft.EntityFrameworkCore.Design;

namespace PushToTalk.Data.EntityFrameworkCore;

/// <summary>
/// Design-time factory for creating PushToTalkDbContext during migrations.
/// </summary>
public class PushToTalkDbContextFactory : IDesignTimeDbContextFactory<PushToTalkDbContext>
{
    public PushToTalkDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PushToTalkDbContext>();

        // Use a default connection string for migrations
        // This will be replaced by actual connection string from configuration at runtime
        optionsBuilder.UseNpgsql("Host=localhost;Database=push_to_talk;Username=postgres;Password=postgres");

        return new PushToTalkDbContext(optionsBuilder.Options);
    }
}
