namespace Olbrasoft.PushToTalk.Core.Tests.Models;

using Olbrasoft.PushToTalk.Core.Models;

public class AudioDataEventArgsTests
{
    [Fact]
    public void Constructor_ShouldSetDataProperty()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new AudioDataEventArgs(data, timestamp);

        // Assert
        Assert.Equal(data, args.Data);
    }

    [Fact]
    public void Constructor_ShouldSetTimestampProperty()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3 };
        var timestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var args = new AudioDataEventArgs(data, timestamp);

        // Assert
        Assert.Equal(timestamp, args.Timestamp);
    }

    [Fact]
    public void Constructor_WithEmptyData_ShouldSucceed()
    {
        // Arrange
        var data = Array.Empty<byte>();
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new AudioDataEventArgs(data, timestamp);

        // Assert
        Assert.Empty(args.Data);
    }

    [Fact]
    public void Data_ShouldReturnSameReferenceAsProvided()
    {
        // Arrange
        var data = new byte[] { 10, 20, 30 };
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new AudioDataEventArgs(data, timestamp);

        // Assert
        Assert.Same(data, args.Data);
    }

    [Fact]
    public void InheritsFromEventArgs()
    {
        // Arrange & Act
        var args = new AudioDataEventArgs(new byte[] { 1 }, DateTime.UtcNow);

        // Assert
        Assert.IsAssignableFrom<EventArgs>(args);
    }
}
