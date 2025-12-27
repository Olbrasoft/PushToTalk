namespace Olbrasoft.PushToTalk.Core.Tests.Models;

using Olbrasoft.PushToTalk.Core.Models;

public class KeyCodeTests
{
    [Theory]
    [InlineData(KeyCode.Unknown, 0)]
    [InlineData(KeyCode.Escape, 1)]
    [InlineData(KeyCode.CapsLock, 58)]
    [InlineData(KeyCode.ScrollLock, 70)]
    [InlineData(KeyCode.NumLock, 69)]
    [InlineData(KeyCode.LeftControl, 29)]
    [InlineData(KeyCode.RightControl, 97)]
    [InlineData(KeyCode.LeftShift, 42)]
    [InlineData(KeyCode.RightShift, 54)]
    [InlineData(KeyCode.LeftAlt, 56)]
    [InlineData(KeyCode.RightAlt, 100)]
    [InlineData(KeyCode.Space, 57)]
    [InlineData(KeyCode.Enter, 28)]
    [InlineData(KeyCode.C, 46)]
    [InlineData(KeyCode.V, 47)]
    [InlineData(KeyCode.F8, 66)]
    public void KeyCode_ShouldHaveCorrectEvdevValue(KeyCode keyCode, int expectedValue)
    {
        // Assert
        Assert.Equal(expectedValue, (int)keyCode);
    }

    [Fact]
    public void KeyCode_Unknown_ShouldBeDefaultValue()
    {
        // Arrange
        KeyCode defaultKey = default;

        // Assert
        Assert.Equal(KeyCode.Unknown, defaultKey);
    }

    [Fact]
    public void KeyCode_ShouldBeConvertibleToInt()
    {
        // Arrange
        var capsLock = KeyCode.CapsLock;

        // Act
        int value = (int)capsLock;

        // Assert
        Assert.Equal(58, value);
    }

    [Fact]
    public void KeyCode_ShouldBeCreatableFromInt()
    {
        // Arrange
        int capsLockValue = 58;

        // Act
        var keyCode = (KeyCode)capsLockValue;

        // Assert
        Assert.Equal(KeyCode.CapsLock, keyCode);
    }

    [Fact]
    public void KeyCode_AllModifierKeys_ShouldBeDistinct()
    {
        // Arrange
        var modifierKeys = new[]
        {
            KeyCode.LeftControl,
            KeyCode.RightControl,
            KeyCode.LeftShift,
            KeyCode.RightShift,
            KeyCode.LeftAlt,
            KeyCode.RightAlt
        };

        // Act
        var distinctCount = modifierKeys.Distinct().Count();

        // Assert
        Assert.Equal(modifierKeys.Length, distinctCount);
    }

    [Fact]
    public void KeyCode_LockKeys_ShouldBeDistinct()
    {
        // Arrange
        var lockKeys = new[]
        {
            KeyCode.CapsLock,
            KeyCode.NumLock,
            KeyCode.ScrollLock
        };

        // Act
        var distinctCount = lockKeys.Distinct().Count();

        // Assert
        Assert.Equal(lockKeys.Length, distinctCount);
    }
}
