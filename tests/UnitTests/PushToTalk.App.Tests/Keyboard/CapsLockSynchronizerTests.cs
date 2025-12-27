using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.PushToTalk.App;
using Olbrasoft.PushToTalk.App.Keyboard;
using Olbrasoft.PushToTalk.Core.Interfaces;

namespace PushToTalk.App.Tests.Keyboard;

public class CapsLockSynchronizerTests
{
    private readonly Mock<IKeyboardMonitor> _mockKeyboardMonitor;
    private readonly Mock<IKeySimulator> _mockKeySimulator;
    private readonly Mock<ILogger<CapsLockSynchronizer>> _mockLogger;
    private readonly CapsLockSynchronizer _synchronizer;

    public CapsLockSynchronizerTests()
    {
        _mockKeyboardMonitor = new Mock<IKeyboardMonitor>();
        _mockKeySimulator = new Mock<IKeySimulator>();
        _mockLogger = new Mock<ILogger<CapsLockSynchronizer>>();
        _synchronizer = new CapsLockSynchronizer(
            _mockKeyboardMonitor.Object,
            _mockKeySimulator.Object,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullKeyboardMonitor_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new CapsLockSynchronizer(null!, _mockKeySimulator.Object, _mockLogger.Object));
        Assert.Equal("keyboardMonitor", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullKeySimulator_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new CapsLockSynchronizer(_mockKeyboardMonitor.Object, null!, _mockLogger.Object));
        Assert.Equal("keySimulator", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new CapsLockSynchronizer(_mockKeyboardMonitor.Object, _mockKeySimulator.Object, null!));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Assert
        Assert.NotNull(_synchronizer);
        Assert.False(_synchronizer.IsSynchronizing);
    }

    #endregion

    #region SynchronizeLedAsync - Already in desired state

    [Fact]
    public async Task SynchronizeLedAsync_WhenLedAlreadyOn_DoesNotToggle()
    {
        // Arrange
        _mockKeyboardMonitor.Setup(m => m.IsCapsLockOn()).Returns(true);

        // Act
        await _synchronizer.SynchronizeLedAsync(shouldBeOn: true);

        // Assert
        _mockKeySimulator.Verify(
            s => s.SimulateKeyPressAsync(It.IsAny<KeyCode>()),
            Times.Never);
    }

    [Fact]
    public async Task SynchronizeLedAsync_WhenLedAlreadyOff_DoesNotToggle()
    {
        // Arrange
        _mockKeyboardMonitor.Setup(m => m.IsCapsLockOn()).Returns(false);

        // Act
        await _synchronizer.SynchronizeLedAsync(shouldBeOn: false);

        // Assert
        _mockKeySimulator.Verify(
            s => s.SimulateKeyPressAsync(It.IsAny<KeyCode>()),
            Times.Never);
    }

    #endregion

    #region SynchronizeLedAsync - Needs synchronization

    [Fact]
    public async Task SynchronizeLedAsync_WhenLedOffButShouldBeOn_TogglesLed()
    {
        // Arrange
        _mockKeyboardMonitor.Setup(m => m.IsCapsLockOn()).Returns(false);
        _mockKeySimulator.Setup(s => s.SimulateKeyPressAsync(KeyCode.CapsLock))
            .Returns(Task.CompletedTask);

        // Act
        await _synchronizer.SynchronizeLedAsync(shouldBeOn: true);

        // Assert
        _mockKeySimulator.Verify(
            s => s.SimulateKeyPressAsync(KeyCode.CapsLock),
            Times.Once);
    }

    [Fact]
    public async Task SynchronizeLedAsync_WhenLedOnButShouldBeOff_TogglesLed()
    {
        // Arrange
        _mockKeyboardMonitor.Setup(m => m.IsCapsLockOn()).Returns(true);
        _mockKeySimulator.Setup(s => s.SimulateKeyPressAsync(KeyCode.CapsLock))
            .Returns(Task.CompletedTask);

        // Act
        await _synchronizer.SynchronizeLedAsync(shouldBeOn: false);

        // Assert
        _mockKeySimulator.Verify(
            s => s.SimulateKeyPressAsync(KeyCode.CapsLock),
            Times.Once);
    }

    #endregion

    #region IsSynchronizing Flag

    [Fact]
    public async Task SynchronizeLedAsync_DuringSynchronization_SetsFlagToTrue()
    {
        // Arrange
        _mockKeyboardMonitor.Setup(m => m.IsCapsLockOn()).Returns(false);

        var flagWasSetToTrue = false;
        _mockKeySimulator.Setup(s => s.SimulateKeyPressAsync(KeyCode.CapsLock))
            .Callback(() =>
            {
                // Check flag during simulation
                flagWasSetToTrue = _synchronizer.IsSynchronizing;
            })
            .Returns(Task.CompletedTask);

        // Act
        await _synchronizer.SynchronizeLedAsync(shouldBeOn: true);

        // Assert
        Assert.True(flagWasSetToTrue, "IsSynchronizing should be true during simulation");
        Assert.False(_synchronizer.IsSynchronizing); // Should be false after completion
    }

    [Fact]
    public async Task SynchronizeLedAsync_AfterCompletion_ResetsFlagToFalse()
    {
        // Arrange
        _mockKeyboardMonitor.Setup(m => m.IsCapsLockOn()).Returns(false);
        _mockKeySimulator.Setup(s => s.SimulateKeyPressAsync(KeyCode.CapsLock))
            .Returns(Task.CompletedTask);

        // Act
        await _synchronizer.SynchronizeLedAsync(shouldBeOn: true);

        // Assert
        Assert.False(_synchronizer.IsSynchronizing);
    }

    [Fact]
    public async Task SynchronizeLedAsync_WhenExceptionOccurs_ResetsFlagToFalse()
    {
        // Arrange
        _mockKeyboardMonitor.Setup(m => m.IsCapsLockOn()).Returns(false);
        _mockKeySimulator.Setup(s => s.SimulateKeyPressAsync(KeyCode.CapsLock))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _synchronizer.SynchronizeLedAsync(shouldBeOn: true));

        Assert.False(_synchronizer.IsSynchronizing);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task SynchronizeLedAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        _mockKeyboardMonitor.Setup(m => m.IsCapsLockOn()).Returns(false);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        _mockKeySimulator.Setup(s => s.SimulateKeyPressAsync(KeyCode.CapsLock))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _synchronizer.SynchronizeLedAsync(shouldBeOn: true, cts.Token));

        Assert.False(_synchronizer.IsSynchronizing);
    }

    #endregion

    #region Custom KeyCode Tests

    [Fact]
    public async Task SynchronizeLedAsync_WithCustomKeyCode_UsesCustomKey()
    {
        // Arrange
        var customKey = KeyCode.ScrollLock;
        var customSynchronizer = new CapsLockSynchronizer(
            _mockKeyboardMonitor.Object,
            _mockKeySimulator.Object,
            _mockLogger.Object,
            customKey);

        _mockKeyboardMonitor.Setup(m => m.IsCapsLockOn()).Returns(false);
        _mockKeySimulator.Setup(s => s.SimulateKeyPressAsync(customKey))
            .Returns(Task.CompletedTask);

        // Act
        await customSynchronizer.SynchronizeLedAsync(shouldBeOn: true);

        // Assert
        _mockKeySimulator.Verify(
            s => s.SimulateKeyPressAsync(customKey),
            Times.Once);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task SynchronizeLedAsync_MultipleSequentialCalls_EachSynchronizesCorrectly()
    {
        // Arrange
        _mockKeyboardMonitor.SetupSequence(m => m.IsCapsLockOn())
            .Returns(false) // First call: OFF, should toggle to ON
            .Returns(true)  // Second call: ON, should toggle to OFF
            .Returns(false);// Third call: OFF, should toggle to ON

        _mockKeySimulator.Setup(s => s.SimulateKeyPressAsync(KeyCode.CapsLock))
            .Returns(Task.CompletedTask);

        // Act
        await _synchronizer.SynchronizeLedAsync(shouldBeOn: true);  // Toggle ON
        await _synchronizer.SynchronizeLedAsync(shouldBeOn: false); // Toggle OFF
        await _synchronizer.SynchronizeLedAsync(shouldBeOn: true);  // Toggle ON

        // Assert
        _mockKeySimulator.Verify(
            s => s.SimulateKeyPressAsync(KeyCode.CapsLock),
            Times.Exactly(3));
    }

    #endregion
}
