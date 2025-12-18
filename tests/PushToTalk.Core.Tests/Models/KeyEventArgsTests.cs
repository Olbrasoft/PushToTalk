namespace Olbrasoft.PushToTalk.Core.Tests.Models;

using Olbrasoft.PushToTalk.Core.Models;

public class KeyEventArgsTests
{
    [Fact]
    public void Constructor_ShouldSetKeyProperty()
    {
        // Arrange
        var key = KeyCode.CapsLock;
        var rawKeyCode = 58;
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new KeyEventArgs(key, rawKeyCode, timestamp);

        // Assert
        Assert.Equal(key, args.Key);
    }

    [Fact]
    public void Constructor_ShouldSetRawKeyCodeProperty()
    {
        // Arrange
        var key = KeyCode.Enter;
        var rawKeyCode = 28;
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new KeyEventArgs(key, rawKeyCode, timestamp);

        // Assert
        Assert.Equal(rawKeyCode, args.RawKeyCode);
    }

    [Fact]
    public void Constructor_ShouldSetTimestampProperty()
    {
        // Arrange
        var key = KeyCode.Space;
        var rawKeyCode = 57;
        var timestamp = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var args = new KeyEventArgs(key, rawKeyCode, timestamp);

        // Assert
        Assert.Equal(timestamp, args.Timestamp);
    }

    [Fact]
    public void Constructor_WithUnknownKeyCode_ShouldSucceed()
    {
        // Arrange
        var key = KeyCode.Unknown;
        var rawKeyCode = 0;
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new KeyEventArgs(key, rawKeyCode, timestamp);

        // Assert
        Assert.Equal(KeyCode.Unknown, args.Key);
        Assert.Equal(0, args.RawKeyCode);
    }

    [Theory]
    [InlineData(KeyCode.CapsLock, 58)]
    [InlineData(KeyCode.Escape, 1)]
    [InlineData(KeyCode.Enter, 28)]
    [InlineData(KeyCode.Space, 57)]
    [InlineData(KeyCode.LeftControl, 29)]
    public void Constructor_WithVariousKeyCodes_ShouldSetCorrectValues(KeyCode key, int rawCode)
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new KeyEventArgs(key, rawCode, timestamp);

        // Assert
        Assert.Equal(key, args.Key);
        Assert.Equal(rawCode, args.RawKeyCode);
    }

    [Fact]
    public void InheritsFromEventArgs()
    {
        // Arrange & Act
        var args = new KeyEventArgs(KeyCode.Enter, 28, DateTime.UtcNow);

        // Assert
        Assert.IsAssignableFrom<EventArgs>(args);
    }
}
