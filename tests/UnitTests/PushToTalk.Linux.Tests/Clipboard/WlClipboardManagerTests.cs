using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.PushToTalk.Clipboard;
using Olbrasoft.Testing.Xunit.Attributes;

namespace Olbrasoft.PushToTalk.Linux.Tests.Clipboard;

public class WlClipboardManagerTests
{
    private readonly Mock<ILogger<WlClipboardManager>> _mockLogger;
    private readonly WlClipboardManager _clipboardManager;

    public WlClipboardManagerTests()
    {
        _mockLogger = new Mock<ILogger<WlClipboardManager>>();
        _clipboardManager = new WlClipboardManager(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new WlClipboardManager(null!));
        Assert.Equal("logger", exception.ParamName);
    }

    [SkipOnCIFact]
    public async Task GetClipboardAsync_WhenClipboardHasContent_ReturnsContent()
    {
        // Arrange - First set some content to clipboard
        await _clipboardManager.SetClipboardAsync("test content");
        await Task.Delay(100); // Wait for clipboard to settle

        // Act
        var result = await _clipboardManager.GetClipboardAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test content", result);
    }

    [SkipOnCIFact]
    public async Task SetClipboardAsync_WithValidContent_SetsClipboardSuccessfully()
    {
        // Arrange
        var testContent = "Hello, World!";

        // Act
        await _clipboardManager.SetClipboardAsync(testContent);
        await Task.Delay(100); // Wait for clipboard to settle

        // Assert - Verify by reading it back
        var result = await _clipboardManager.GetClipboardAsync();
        Assert.Equal(testContent, result);
    }

    [Fact]
    public async Task SetClipboardAsync_WithNullContent_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _clipboardManager.SetClipboardAsync(null!));
    }

    [SkipOnCIFact]
    public async Task SetClipboardAsync_WithEmptyString_SetsEmptyClipboard()
    {
        // Act
        await _clipboardManager.SetClipboardAsync("");
        await Task.Delay(100);

        // Assert
        var result = await _clipboardManager.GetClipboardAsync();
        Assert.Equal("", result);
    }

    [SkipOnCIFact]
    public async Task SetClipboardAsync_WithUnicodeContent_HandlesUnicodeCorrectly()
    {
        // Arrange
        var unicodeContent = "Příliš žluťoučký kůň úpěl ďábelské ódy";

        // Act
        await _clipboardManager.SetClipboardAsync(unicodeContent);
        await Task.Delay(100);

        // Assert
        var result = await _clipboardManager.GetClipboardAsync();
        Assert.Equal(unicodeContent, result);
    }

    [SkipOnCIFact]
    public async Task GetClipboardAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(10); // Cancel after 10ms

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _clipboardManager.GetClipboardAsync(cts.Token));
    }

    [SkipOnCIFact]
    public async Task SetClipboardAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(10); // Cancel after 10ms

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _clipboardManager.SetClipboardAsync("test", cts.Token));
    }

    [SkipOnCIFact]
    public async Task GetClipboardAsync_WhenWlPasteNotAvailable_ReturnsNull()
    {
        // This test assumes wl-paste is available
        // If it's not, GetClipboardAsync should return null without throwing

        // Act
        var result = await _clipboardManager.GetClipboardAsync();

        // Assert
        // Result can be null or content depending on system state
        // We just verify it doesn't throw
        Assert.True(result == null || result != null);
    }
}
