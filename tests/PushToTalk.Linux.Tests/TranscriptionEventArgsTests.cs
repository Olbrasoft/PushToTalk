using Olbrasoft.PushToTalk;

namespace Olbrasoft.PushToTalk.Linux.Tests;

public class TranscriptionEventArgsTests
{
    [Fact]
    public void Constructor_ShouldSetTextProperty()
    {
        // Arrange
        var text = "Hello World";
        var confidence = 0.95f;
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new TranscriptionEventArgs(text, confidence, timestamp);

        // Assert
        Assert.Equal(text, args.Text);
    }

    [Fact]
    public void Constructor_ShouldSetConfidenceProperty()
    {
        // Arrange
        var text = "Test";
        var confidence = 0.87f;
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new TranscriptionEventArgs(text, confidence, timestamp);

        // Assert
        Assert.Equal(confidence, args.Confidence);
    }

    [Fact]
    public void Constructor_ShouldSetTimestampProperty()
    {
        // Arrange
        var text = "Transcribed text";
        var confidence = 1.0f;
        var timestamp = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var args = new TranscriptionEventArgs(text, confidence, timestamp);

        // Assert
        Assert.Equal(timestamp, args.Timestamp);
    }

    [Fact]
    public void Constructor_WithEmptyText_ShouldSucceed()
    {
        // Arrange
        var text = "";
        var confidence = 0.0f;
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new TranscriptionEventArgs(text, confidence, timestamp);

        // Assert
        Assert.Equal(string.Empty, args.Text);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    public void Constructor_WithVariousConfidenceValues_ShouldSucceed(float confidence)
    {
        // Arrange
        var text = "Test";
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new TranscriptionEventArgs(text, confidence, timestamp);

        // Assert
        Assert.Equal(confidence, args.Confidence);
    }

    [Fact]
    public void InheritsFromEventArgs()
    {
        // Arrange & Act
        var args = new TranscriptionEventArgs("test", 1.0f, DateTime.UtcNow);

        // Assert
        Assert.IsAssignableFrom<EventArgs>(args);
    }
}
