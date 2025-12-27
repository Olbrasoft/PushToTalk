using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.PushToTalk.Clipboard;
using Olbrasoft.PushToTalk.Core.Interfaces;
using Olbrasoft.PushToTalk.TextInput;
using Olbrasoft.PushToTalk.WindowManagement;

namespace PushToTalk.Linux.Tests.TextInput;

/// <summary>
/// Unit tests for DotoolTextTyper using mocked dependencies.
/// Following engineering-handbook/testing/clipboard-testing.md guidelines:
/// - Mock IClipboardManager instead of calling real clipboard
/// - Mock ITerminalDetector instead of checking real windows
/// - Test logic without system side effects
/// </summary>
public class DotoolTextTyperTests : IDisposable
{
    private readonly Mock<IClipboardManager> _mockClipboardManager;
    private readonly Mock<ITerminalDetector> _mockTerminalDetector;
    private readonly Mock<ILogger<DotoolTextTyper>> _mockLogger;
    private readonly DotoolTextTyper _typer;

    public DotoolTextTyperTests()
    {
        _mockClipboardManager = new Mock<IClipboardManager>();
        _mockTerminalDetector = new Mock<ITerminalDetector>();
        _mockLogger = new Mock<ILogger<DotoolTextTyper>>();

        _typer = new DotoolTextTyper(
            _mockClipboardManager.Object,
            _mockTerminalDetector.Object,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullClipboardManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DotoolTextTyper(null!, _mockTerminalDetector.Object, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullTerminalDetector_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DotoolTextTyper(_mockClipboardManager.Object, null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DotoolTextTyper(_mockClipboardManager.Object, _mockTerminalDetector.Object, null!));
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var typer = new DotoolTextTyper(
            _mockClipboardManager.Object,
            _mockTerminalDetector.Object,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(typer);
        Assert.IsAssignableFrom<ITextTyper>(typer);
    }

    #endregion

    #region Clipboard Save/Restore Logic Tests

    [Fact]
    public async Task TypeTextAsync_WithEmptyText_DoesNothing()
    {
        // Arrange
        var emptyText = "";

        // Act
        await _typer.TypeTextAsync(emptyText);

        // Assert - Should not call clipboard at all
        _mockClipboardManager.Verify(
            c => c.GetClipboardAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        _mockClipboardManager.Verify(
            c => c.SetClipboardAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TypeTextAsync_WithWhitespaceText_DoesNothing()
    {
        // Arrange
        var whitespaceText = "   \t\n  ";

        // Act
        await _typer.TypeTextAsync(whitespaceText);

        // Assert - Should not call clipboard at all
        _mockClipboardManager.Verify(
            c => c.GetClipboardAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // NOTE: Tests for TypeTextAsync with valid text would require mocking Process.Start
    // which is not straightforward. These tests verify the abstraction layer works correctly.
    // Actual dotool execution should be tested in integration tests, not unit tests.

    #endregion

    #region Dependency Injection Tests

    [Fact]
    public void TyperUsesDependencies_ClipboardManager_InjectedCorrectly()
    {
        // Arrange
        var mockClipboard = new Mock<IClipboardManager>();
        var mockTerminal = new Mock<ITerminalDetector>();
        var mockLogger = new Mock<ILogger<DotoolTextTyper>>();

        // Act
        var typer = new DotoolTextTyper(mockClipboard.Object, mockTerminal.Object, mockLogger.Object);

        // Assert - Constructor doesn't throw, dependencies accepted
        Assert.NotNull(typer);
    }

    [Fact]
    public void TyperUsesDependencies_TerminalDetector_InjectedCorrectly()
    {
        // Arrange
        var mockClipboard = new Mock<IClipboardManager>();
        var mockTerminal = new Mock<ITerminalDetector>();
        var mockLogger = new Mock<ILogger<DotoolTextTyper>>();

        // Act
        var typer = new DotoolTextTyper(mockClipboard.Object, mockTerminal.Object, mockLogger.Object);

        // Assert - Constructor doesn't throw, dependencies accepted
        Assert.NotNull(typer);
    }

    #endregion

    #region SendKeyAsync Tests

    [Fact]
    public async Task SendKeyAsync_WithEmptyKey_DoesNothing()
    {
        // Arrange
        var emptyKey = "";

        // Act
        await _typer.SendKeyAsync(emptyKey);

        // Assert - Should complete without errors
        // (Actual behavior depends on implementation - this tests it doesn't throw)
    }

    // NOTE: Full SendKeyAsync tests would require Process mocking
    // Integration tests should verify actual key sending behavior

    #endregion

    public void Dispose()
    {
        // Cleanup if needed (mocks don't require disposal)
        GC.SuppressFinalize(this);
    }
}
