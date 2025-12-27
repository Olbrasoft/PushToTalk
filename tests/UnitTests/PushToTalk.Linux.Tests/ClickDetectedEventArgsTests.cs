using Olbrasoft.PushToTalk;

namespace Olbrasoft.PushToTalk.Linux.Tests;

public class ClickDetectedEventArgsTests
{
    [Theory]
    [InlineData(ClickResult.Pending)]
    [InlineData(ClickResult.SingleClick)]
    [InlineData(ClickResult.DoubleClick)]
    [InlineData(ClickResult.TripleClick)]
    public void Constructor_ShouldSetResultProperty(ClickResult result)
    {
        // Act
        var args = new ClickDetectedEventArgs(result);

        // Assert
        Assert.Equal(result, args.Result);
    }

    [Fact]
    public void InheritsFromEventArgs()
    {
        // Arrange & Act
        var args = new ClickDetectedEventArgs(ClickResult.SingleClick);

        // Assert
        Assert.IsAssignableFrom<EventArgs>(args);
    }
}

public class ClickResultTests
{
    [Fact]
    public void Pending_ShouldBeDefaultValue()
    {
        // Arrange
        ClickResult defaultResult = default;

        // Assert
        Assert.Equal(ClickResult.Pending, defaultResult);
    }

    [Theory]
    [InlineData(ClickResult.Pending, 0)]
    [InlineData(ClickResult.SingleClick, 1)]
    [InlineData(ClickResult.DoubleClick, 2)]
    [InlineData(ClickResult.TripleClick, 3)]
    public void ClickResult_ShouldHaveExpectedValues(ClickResult result, int expectedValue)
    {
        // Assert
        Assert.Equal(expectedValue, (int)result);
    }

    [Fact]
    public void AllClickResults_ShouldBeDistinct()
    {
        // Arrange
        var allResults = Enum.GetValues<ClickResult>();

        // Act
        var distinctCount = allResults.Distinct().Count();

        // Assert
        Assert.Equal(allResults.Length, distinctCount);
    }
}
