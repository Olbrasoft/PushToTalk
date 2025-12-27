using Olbrasoft.PushToTalk;

namespace Olbrasoft.PushToTalk.Linux.Tests;

public class MultiClickDetectorTests : IDisposable
{
    private readonly MultiClickDetector _detector;
    private readonly List<ClickResult> _detectedClicks;

    public MultiClickDetectorTests()
    {
        _detector = new MultiClickDetector("TEST");
        _detectedClicks = new List<ClickResult>();
        _detector.ClickDetected += (_, e) => _detectedClicks.Add(e.Result);
    }

    public void Dispose()
    {
        _detector.Dispose();
    }

    [Fact]
    public void Constructor_ShouldSetDefaultClickThreshold()
    {
        // Assert
        Assert.Equal(MultiClickDetector.DefaultClickThresholdMs, _detector.ClickThresholdMs);
    }

    [Fact]
    public void Constructor_ShouldSetDefaultClickDebounce()
    {
        // Assert
        Assert.Equal(MultiClickDetector.DefaultClickDebounceMs, _detector.ClickDebounceMs);
    }

    [Fact]
    public void Constructor_ShouldSetMaxClickCountToThree()
    {
        // Assert
        Assert.Equal(3, _detector.MaxClickCount);
    }

    [Fact]
    public void Constructor_WithCustomMaxClickCount_ShouldSetValue()
    {
        // Arrange & Act
        using var detector = new MultiClickDetector("TEST", maxClickCount: 2);

        // Assert
        Assert.Equal(2, detector.MaxClickCount);
    }

    [Fact]
    public void ClickThresholdMs_ShouldBeSettable()
    {
        // Act
        _detector.ClickThresholdMs = 500;

        // Assert
        Assert.Equal(500, _detector.ClickThresholdMs);
    }

    [Fact]
    public void ClickDebounceMs_ShouldBeSettable()
    {
        // Act
        _detector.ClickDebounceMs = 100;

        // Assert
        Assert.Equal(100, _detector.ClickDebounceMs);
    }

    [Fact]
    public void MaxClickCount_ShouldBeSettable()
    {
        // Act
        _detector.MaxClickCount = 5;

        // Assert
        Assert.Equal(5, _detector.MaxClickCount);
    }

    [Fact]
    public async Task RegisterClick_TripleClick_ShouldFireImmediately()
    {
        // Arrange
        _detector.ClickThresholdMs = 1000;
        _detector.ClickDebounceMs = 0;

        // Act - three rapid clicks
        _detector.RegisterClick();
        await Task.Delay(60);
        _detector.RegisterClick();
        await Task.Delay(60);
        _detector.RegisterClick();

        // Assert - should fire immediately for triple click
        await Task.Delay(100);
        Assert.Contains(ClickResult.TripleClick, _detectedClicks);
    }

    [Fact]
    public async Task RegisterClick_SingleClick_ShouldFireAfterTimeout()
    {
        // Arrange
        _detector.ClickThresholdMs = 200;

        // Act
        _detector.RegisterClick();

        // Wait for timeout + buffer
        await Task.Delay(350);

        // Assert
        Assert.Contains(ClickResult.SingleClick, _detectedClicks);
    }

    [Fact]
    public void Reset_ShouldClearState()
    {
        // Arrange
        _detector.RegisterClick();

        // Act
        _detector.Reset();

        // Assert - no exception means success
        _detector.RegisterClick(); // Should start fresh sequence
    }

    [Fact]
    public void RegisterClick_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _detector.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _detector.RegisterClick());
    }

    [Fact]
    public void Dispose_MultipleCalls_ShouldNotThrow()
    {
        // Act & Assert - should not throw
        _detector.Dispose();
        _detector.Dispose();
    }

    [Fact]
    public void DefaultClickThresholdMs_ShouldBe800()
    {
        // Assert
        Assert.Equal(800, MultiClickDetector.DefaultClickThresholdMs);
    }

    [Fact]
    public void DefaultClickDebounceMs_ShouldBe50()
    {
        // Assert
        Assert.Equal(50, MultiClickDetector.DefaultClickDebounceMs);
    }

    [Fact]
    public void KeySimulationDelayMs_ShouldBe100()
    {
        // Assert
        Assert.Equal(100, MultiClickDetector.KeySimulationDelayMs);
    }
}
