using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.PushToTalk.App;

namespace Olbrasoft.PushToTalk.App.Tests;

public class IconsPathResolverTests
{
    [Fact]
    public void FindIconsPath_WithNullLogger_ShouldNotThrow()
    {
        // Act
        var result = IconsPathResolver.FindIconsPath(null);

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void FindIconsPath_ShouldReturnAbsolutePath()
    {
        // Act
        var result = IconsPathResolver.FindIconsPath(null);

        // Assert
        Assert.True(Path.IsPathRooted(result));
    }

    [Fact]
    public void FindIconsPath_ShouldReturnPathEndingWithIcons()
    {
        // Act
        var result = IconsPathResolver.FindIconsPath(null);

        // Assert
        Assert.EndsWith("icons", result);
    }

    [Fact]
    public void FindIconsPath_WithLogger_ShouldNotThrow()
    {
        // Arrange
        var loggerMock = new Mock<ILogger>();

        // Act
        var result = IconsPathResolver.FindIconsPath(loggerMock.Object);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void FindIconsPath_ShouldReturnNonEmptyPath()
    {
        // Act
        var result = IconsPathResolver.FindIconsPath(null);

        // Assert
        Assert.NotEmpty(result);
    }

    [Fact]
    public void FindIconsPath_MultipleCalls_ShouldReturnConsistentResult()
    {
        // Act
        var result1 = IconsPathResolver.FindIconsPath(null);
        var result2 = IconsPathResolver.FindIconsPath(null);

        // Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void FindIconsPath_ShouldNotReturnRelativePath()
    {
        // Act
        var result = IconsPathResolver.FindIconsPath(null);

        // Assert
        Assert.DoesNotContain("..", result.TrimEnd(Path.DirectorySeparatorChar));
    }
}
