using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.PushToTalk.Core.Configuration;
using Olbrasoft.PushToTalk.Service.Configuration;
using PushToTalk.Data.Entities;
using PushToTalk.Data.EntityFrameworkCore;

namespace PushToTalk.Service.Tests.Configuration;

public class DatabaseMistralOptionsSetupTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly string _databaseName;

    public DatabaseMistralOptionsSetupTests()
    {
        _databaseName = Guid.NewGuid().ToString();

        // Setup in-memory database
        var services = new ServiceCollection();

        services.AddDbContext<PushToTalkDbContext>(options =>
        {
            options.UseInMemoryDatabase(_databaseName);
        });

        services.AddLogging(builder => builder.AddConsole());

        // Register DatabaseMistralOptionsSetup
        services.ConfigureOptions<DatabaseMistralOptionsSetup>();

        _serviceProvider = services.BuildServiceProvider();
    }

    private PushToTalkDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PushToTalkDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;
        return new PushToTalkDbContext(options);
    }

    [Fact]
    public void Configure_WithActiveConfig_LoadsOptionsFromDatabase()
    {
        // Arrange
        using (var dbContext = CreateDbContext())
        {
            var config = new MistralConfig
            {
                ApiKey = "test-api-key-123",
                Model = "mistral-large-latest",
                BaseUrl = "https://api.mistral.ai",
                TimeoutSeconds = 45,
                MaxTokens = 1500,
                Temperature = 0.5,
                IsActive = true,
                Label = "Test Config",
                CreatedAt = DateTime.UtcNow
            };

            dbContext.MistralConfigs.Add(config);
            dbContext.SaveChanges();
        }

        // Act
        var options = _serviceProvider.GetRequiredService<IOptions<MistralOptions>>().Value;

        // Assert
        Assert.Equal("test-api-key-123", options.ApiKey);
        Assert.Equal("mistral-large-latest", options.Model);
        Assert.Equal("https://api.mistral.ai", options.BaseUrl);
        Assert.Equal(45, options.TimeoutSeconds);
        Assert.Equal(1500, options.MaxTokens);
        Assert.Equal(0.5, options.Temperature);
    }

    [Fact]
    public void Configure_WithMultipleConfigs_LoadsMostRecentActive()
    {
        // Arrange
        using (var dbContext = CreateDbContext())
        {
            // Add older config
            var olderConfig = new MistralConfig
            {
                ApiKey = "old-key",
                Model = "mistral-medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            // Add newer config
            var newerConfig = new MistralConfig
            {
                ApiKey = "new-key",
                Model = "mistral-large-latest",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.MistralConfigs.AddRange(olderConfig, newerConfig);
            dbContext.SaveChanges();
        }

        // Act
        var options = _serviceProvider.GetRequiredService<IOptions<MistralOptions>>().Value;

        // Assert - Should load newer config
        Assert.Equal("new-key", options.ApiKey);
        Assert.Equal("mistral-large-latest", options.Model);
    }

    [Fact]
    public void Configure_WithInactiveConfig_SkipsInactive()
    {
        // Arrange
        using (var dbContext = CreateDbContext())
        {
            var inactiveConfig = new MistralConfig
            {
                ApiKey = "inactive-key",
                Model = "mistral-small",
                IsActive = false,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            };

            var activeConfig = new MistralConfig
            {
                ApiKey = "active-key",
                Model = "mistral-large-latest",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-10) // Older, but active
            };

            dbContext.MistralConfigs.AddRange(inactiveConfig, activeConfig);
            dbContext.SaveChanges();
        }

        // Act
        var options = _serviceProvider.GetRequiredService<IOptions<MistralOptions>>().Value;

        // Assert - Should load active config even though it's older
        Assert.Equal("active-key", options.ApiKey);
        Assert.Equal("mistral-large-latest", options.Model);
    }

    [Fact]
    public void Configure_WithNoActiveConfig_ThrowsException()
    {
        // Arrange - Add only inactive config
        using (var dbContext = CreateDbContext())
        {
            var config = new MistralConfig
            {
                ApiKey = "test-key",
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.MistralConfigs.Add(config);
            dbContext.SaveChanges();
        }

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var options = _serviceProvider.GetRequiredService<IOptions<MistralOptions>>().Value;
        });

        Assert.Contains("No active Mistral configuration found", exception.Message);
    }

    [Fact]
    public void Configure_WithEmptyDatabase_ThrowsException()
    {
        // Arrange - No configs in database

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var options = _serviceProvider.GetRequiredService<IOptions<MistralOptions>>().Value;
        });

        Assert.Contains("No active Mistral configuration found", exception.Message);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
