using Olbrasoft.PushToTalk.App;

namespace Olbrasoft.PushToTalk.App.Tests;

public class SingleInstanceLockTests : IDisposable
{
    private readonly string _testLockPath;
    private readonly List<SingleInstanceLock> _locksToDispose = new();

    public SingleInstanceLockTests()
    {
        _testLockPath = Path.Combine(Path.GetTempPath(), $"test-lock-{Guid.NewGuid()}.lock");
    }

    public void Dispose()
    {
        foreach (var lockInstance in _locksToDispose)
        {
            lockInstance.Dispose();
        }

        if (File.Exists(_testLockPath))
        {
            try { File.Delete(_testLockPath); }
            catch { /* ignore cleanup errors */ }
        }
    }

    [Fact]
    public void TryAcquire_FirstInstance_ShouldAcquireLock()
    {
        // Arrange & Act
        var lockInstance = SingleInstanceLock.TryAcquire(_testLockPath);
        _locksToDispose.Add(lockInstance);

        // Assert
        Assert.True(lockInstance.IsAcquired);
    }

    [Fact]
    public void TryAcquire_SecondInstance_ShouldNotAcquireLock()
    {
        // Arrange
        var firstLock = SingleInstanceLock.TryAcquire(_testLockPath);
        _locksToDispose.Add(firstLock);

        // Act
        var secondLock = SingleInstanceLock.TryAcquire(_testLockPath);
        _locksToDispose.Add(secondLock);

        // Assert
        Assert.True(firstLock.IsAcquired);
        Assert.False(secondLock.IsAcquired);
    }

    [Fact]
    public void TryAcquire_AfterFirstDisposed_ShouldAcquireLock()
    {
        // Arrange
        var firstLock = SingleInstanceLock.TryAcquire(_testLockPath);
        firstLock.Dispose();

        // Act
        var secondLock = SingleInstanceLock.TryAcquire(_testLockPath);
        _locksToDispose.Add(secondLock);

        // Assert
        Assert.True(secondLock.IsAcquired);
    }

    [Fact]
    public void Dispose_ShouldDeleteLockFile()
    {
        // Arrange
        var lockInstance = SingleInstanceLock.TryAcquire(_testLockPath);

        // Act
        lockInstance.Dispose();

        // Assert
        Assert.False(File.Exists(_testLockPath));
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var lockInstance = SingleInstanceLock.TryAcquire(_testLockPath);

        // Act & Assert (should not throw)
        lockInstance.Dispose();
        lockInstance.Dispose();
        lockInstance.Dispose();
    }

    [Fact]
    public void TryAcquire_CreatesFileWithPid()
    {
        // Arrange & Act
        var lockInstance = SingleInstanceLock.TryAcquire(_testLockPath);
        _locksToDispose.Add(lockInstance);

        // Assert
        Assert.True(File.Exists(_testLockPath));
    }
}
