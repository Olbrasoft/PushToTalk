namespace Olbrasoft.PushToTalk.Core.Tests.Models;

using Olbrasoft.PushToTalk.Core.Models;

public class TranscriptionResultTests
{
    [Fact]
    public void Constructor_Success_ShouldSetTextProperty()
    {
        // Arrange
        var text = "Hello World";
        var confidence = 0.95f;

        // Act
        var result = new TranscriptionResult(text, confidence);

        // Assert
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public void Constructor_Success_ShouldSetConfidenceProperty()
    {
        // Arrange
        var text = "Test transcription";
        var confidence = 0.87f;

        // Act
        var result = new TranscriptionResult(text, confidence);

        // Assert
        Assert.Equal(confidence, result.Confidence);
    }

    [Fact]
    public void Constructor_Success_ShouldSetSuccessToTrue()
    {
        // Arrange
        var text = "Successful transcription";
        var confidence = 1.0f;

        // Act
        var result = new TranscriptionResult(text, confidence);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void Constructor_Success_ShouldSetErrorMessageToNull()
    {
        // Arrange
        var text = "Some text";
        var confidence = 0.5f;

        // Act
        var result = new TranscriptionResult(text, confidence);

        // Assert
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Constructor_Failure_ShouldSetErrorMessageProperty()
    {
        // Arrange
        var errorMessage = "Transcription failed due to audio quality";

        // Act
        var result = new TranscriptionResult(errorMessage);

        // Assert
        Assert.Equal(errorMessage, result.ErrorMessage);
    }

    [Fact]
    public void Constructor_Failure_ShouldSetSuccessToFalse()
    {
        // Arrange
        var errorMessage = "Model not loaded";

        // Act
        var result = new TranscriptionResult(errorMessage);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public void Constructor_Failure_ShouldSetTextToEmpty()
    {
        // Arrange
        var errorMessage = "No audio data";

        // Act
        var result = new TranscriptionResult(errorMessage);

        // Assert
        Assert.Equal(string.Empty, result.Text);
    }

    [Fact]
    public void Constructor_Failure_ShouldSetConfidenceToZero()
    {
        // Arrange
        var errorMessage = "Timeout occurred";

        // Act
        var result = new TranscriptionResult(errorMessage);

        // Assert
        Assert.Equal(0.0f, result.Confidence);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1.0f)]
    public void Constructor_Success_ShouldAcceptVariousConfidenceValues(float confidence)
    {
        // Arrange
        var text = "Test";

        // Act
        var result = new TranscriptionResult(text, confidence);

        // Assert
        Assert.Equal(confidence, result.Confidence);
        Assert.True(result.Success);
    }

    [Fact]
    public void Constructor_Success_WithEmptyText_ShouldSucceed()
    {
        // Arrange
        var text = "";
        var confidence = 0.0f;

        // Act
        var result = new TranscriptionResult(text, confidence);

        // Assert
        Assert.Equal(string.Empty, result.Text);
        Assert.True(result.Success);
    }
}
