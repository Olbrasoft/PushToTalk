using Microsoft.EntityFrameworkCore;

namespace PushToTalk.Data.EntityFrameworkCore.Tests.Infrastructure;

/// <summary>
/// Base class for database tests using in-memory EF Core database.
/// Provides fresh DbContext for each test to ensure isolation.
/// </summary>
public abstract class DatabaseTestBase : IDisposable
{
    protected PushToTalkDbContext DbContext { get; private set; }

    protected DatabaseTestBase()
    {
        DbContext = CreateDbContext();
    }

    /// <summary>
    /// Creates a new DbContext with in-memory database.
    /// Uses unique database name per test to ensure isolation.
    /// </summary>
    private PushToTalkDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PushToTalkDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        return new PushToTalkDbContext(options);
    }

    /// <summary>
    /// Seeds test data into the database.
    /// Override in derived classes to add custom test data.
    /// </summary>
    protected virtual void SeedTestData()
    {
        // Override in derived classes
    }

    /// <summary>
    /// Recreates DbContext (useful if testing disposal scenarios).
    /// </summary>
    protected void RecreateDbContext()
    {
        DbContext?.Dispose();
        DbContext = CreateDbContext();
    }

    public void Dispose()
    {
        DbContext?.Dispose();
        GC.SuppressFinalize(this);
    }
}
