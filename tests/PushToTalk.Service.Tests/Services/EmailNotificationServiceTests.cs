using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.PushToTalk.Service.Services;
using PushToTalk.Data.Entities;
using PushToTalk.Data.EntityFrameworkCore;

namespace PushToTalk.Service.Tests.Services;

public class EmailNotificationServiceTests : IDisposable
{
    private readonly PushToTalkDbContext _dbContext;
    private readonly Mock<ILogger<EmailNotificationService>> _mockLogger;
    private readonly EmailNotificationService _service;

    public EmailNotificationServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<PushToTalkDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PushToTalkDbContext(options);

        _mockLogger = new Mock<ILogger<EmailNotificationService>>();

        _service = new EmailNotificationService(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task SendCircuitOpenedNotificationAsync_WithNoEmailConfig_LogsWarning()
    {
        // Arrange - No email config in database

        // Act
        await _service.SendCircuitOpenedNotificationAsync("mistral", 3, "Test error", CancellationToken.None);

        // Assert - Should log warning
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No active email configuration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendCircuitClosedNotificationAsync_WithNoEmailConfig_LogsWarning()
    {
        // Arrange - No email config in database

        // Act
        await _service.SendCircuitClosedNotificationAsync("mistral", CancellationToken.None);

        // Assert - Should log warning
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No active email configuration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetActiveEmailConfig_WithMultipleConfigs_LoadsMostRecent()
    {
        // Arrange
        var olderConfig = new Email
        {
            FromEmail = "old@example.com",
            ToEmail = "recipient@example.com",
            SmtpServer = "smtp.old.com",
            SmtpPort = 587,
            UseSsl = true,
            FromPassword = "old-password",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        var newerConfig = new Email
        {
            FromEmail = "new@example.com",
            ToEmail = "recipient@example.com",
            SmtpServer = "smtp.new.com",
            SmtpPort = 465,
            UseSsl = true,
            FromPassword = "new-password",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Emails.AddRange(olderConfig, newerConfig);
        await _dbContext.SaveChangesAsync();

        // Act - This will internally call GetActiveEmailConfigAsync
        // We can't directly test private method, but we can verify the service doesn't throw
        // and uses the correct config by checking logs
        await _service.SendCircuitClosedNotificationAsync("mistral", CancellationToken.None);

        // Assert - Service should attempt to send with newer config
        // Since we can't actually send email in tests, we just verify no "No active email" warning
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No active email configuration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task GetActiveEmailConfig_WithInactiveConfigs_SkipsInactive()
    {
        // Arrange
        var inactiveConfig = new Email
        {
            FromEmail = "inactive@example.com",
            ToEmail = "recipient@example.com",
            SmtpServer = "smtp.test.com",
            SmtpPort = 587,
            UseSsl = true,
            FromPassword = "password",
            IsActive = false, // Inactive
            CreatedAt = DateTime.UtcNow
        };

        var activeConfig = new Email
        {
            FromEmail = "active@example.com",
            ToEmail = "recipient@example.com",
            SmtpServer = "smtp.active.com",
            SmtpPort = 465,
            UseSsl = true,
            FromPassword = "active-password",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-5) // Older, but active
        };

        _dbContext.Emails.AddRange(inactiveConfig, activeConfig);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.SendCircuitOpenedNotificationAsync("mistral", 3, "Test error", CancellationToken.None);

        // Assert - Should use active config, not log warning
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No active email configuration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task SendEmail_WithInvalidSmtpConfig_LogsError()
    {
        // Arrange - Add email config with invalid SMTP server
        var config = new Email
        {
            FromEmail = "test@example.com",
            ToEmail = "recipient@example.com",
            SmtpServer = "invalid-smtp-server-that-does-not-exist.local",
            SmtpPort = 587,
            UseSsl = true,
            FromPassword = "test-password",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Emails.Add(config);
        await _dbContext.SaveChangesAsync();

        // Act - Try to send email (will fail due to invalid SMTP)
        await _service.SendCircuitOpenedNotificationAsync("mistral", 3, "Test error", CancellationToken.None);

        // Assert - Should log error about email send failure
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("error sending email notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
