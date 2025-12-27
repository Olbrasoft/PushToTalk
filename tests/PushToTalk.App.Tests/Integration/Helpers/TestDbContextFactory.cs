using Microsoft.EntityFrameworkCore;
using PushToTalk.Data.EntityFrameworkCore;

namespace PushToTalk.App.Tests.Helpers;

/// <summary>
/// Factory for creating DbContext instances for integration tests.
/// Uses real PostgreSQL database: push_to_talk_tests
/// </summary>
public class TestDbContextFactory
{
    private const string TestConnectionString = "Host=localhost;Port=5432;Database=push_to_talk_tests;Username=postgres;Password=postgres";

    /// <summary>
    /// Creates a new DbContext instance configured for the test database.
    /// WARNING: Uses push_to_talk_tests database - NOT the production push_to_talk database!
    /// </summary>
    public static PushToTalkDbContext Create()
    {
        var options = new DbContextOptionsBuilder<PushToTalkDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        return new PushToTalkDbContext(options);
    }

    /// <summary>
    /// Recreates the test database (drops and recreates all tables).
    /// WARNING: This DELETES ALL DATA in push_to_talk_tests database!
    /// </summary>
    public static async Task RecreateDatabase()
    {
        using var context = Create();

        // Delete database (removes all data)
        await context.Database.EnsureDeletedAsync();

        // Create database with all migrations applied
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Verifies we're using the test database (safety check).
    /// </summary>
    public static void VerifyTestDatabase()
    {
        using var context = Create();
        var connectionString = context.Database.GetConnectionString();

        if (!connectionString!.Contains("push_to_talk_tests"))
        {
            throw new InvalidOperationException(
                $"DANGER: Not using test database! Connection string: {connectionString}");
        }
    }
}
