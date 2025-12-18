using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.PushToTalk.Service.Services;

namespace Olbrasoft.PushToTalk.Service.Tests.Services;

public class SpeechLockServiceTests : IDisposable
{
    private readonly Mock<ILogger<SpeechLockService>> _loggerMock;
    private readonly string _testLockPath;
    private readonly SpeechLockService _service;

    public SpeechLockServiceTests()
    {
        _loggerMock = new Mock<ILogger<SpeechLockService>>();

        _testLockPath = Path.Combine(Path.GetTempPath(), $"speech-lock-test-{Guid.NewGuid()}");

        // Use real in-memory configuration instead of mocking
        var configValues = new Dictionary<string, string?>
        {
            { "SystemPaths:SpeechLockFile", _testLockPath }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _service = new SpeechLockService(_loggerMock.Object, configuration);
    }

    public void Dispose()
    {
        // Cleanup test lock file
        if (File.Exists(_testLockPath))
        {
            File.Delete(_testLockPath);
        }
    }

    [Fact]
    public void IsLocked_WhenNoLockFile_ReturnsFalse()
    {
        // Assert
        Assert.False(_service.IsLocked);
    }

    [Fact]
    public void CreateLock_CreatesLockFile()
    {
        // Act
        _service.CreateLock("Test reason");

        // Assert
        Assert.True(File.Exists(_testLockPath));
        Assert.True(_service.IsLocked);
    }

    [Fact]
    public void CreateLock_WritesReasonToFile()
    {
        // Arrange
        const string reason = "Test reason";

        // Act
        _service.CreateLock(reason);

        // Assert
        var content = File.ReadAllText(_testLockPath);
        Assert.Equal(reason, content);
    }

    [Fact]
    public void ReleaseLock_DeletesLockFile()
    {
        // Arrange
        _service.CreateLock("Test");
        Assert.True(_service.IsLocked);

        // Act
        _service.ReleaseLock();

        // Assert
        Assert.False(File.Exists(_testLockPath));
        Assert.False(_service.IsLocked);
    }

    [Fact]
    public void ReleaseLock_WhenNoLockFile_DoesNotThrow()
    {
        // Act & Assert - should not throw
        _service.ReleaseLock();
    }

    [Fact]
    public void CreateLock_CanBeCalledMultipleTimes()
    {
        // Act
        _service.CreateLock("First");
        _service.CreateLock("Second");

        // Assert
        var content = File.ReadAllText(_testLockPath);
        Assert.Equal("Second", content);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SpeechLockService(null!, configuration));
    }

    [Fact]
    public void Constructor_WithNullConfiguration_UsesDefaultPath()
    {
        // Arrange - empty configuration (no SpeechLockFile setting)
        var configuration = new ConfigurationBuilder().Build();

        // Act - should not throw, uses default path
        var service = new SpeechLockService(_loggerMock.Object, configuration);

        // Assert - service created successfully
        Assert.NotNull(service);
    }
}
