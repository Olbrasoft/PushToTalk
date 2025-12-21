using Olbrasoft.PushToTalk;
using Olbrasoft.PushToTalk.App;

namespace Olbrasoft.PushToTalk.App.Tests;

public class DictationOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeDictation()
    {
        // Assert
        Assert.Equal("Dictation", DictationOptions.SectionName);
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new DictationOptions();

        // Assert
        Assert.Equal("cs", options.WhisperLanguage);
        Assert.Equal("CapsLock", options.TriggerKey);
        Assert.Equal("Escape", options.CancelKey);
        Assert.Equal(22, options.IconSize);
        Assert.Equal(150, options.AnimationIntervalMs);
        Assert.True(options.ShowTranscriptionAnimation);
        Assert.Null(options.KeyboardDevice);
        Assert.Null(options.TranscriptionSoundPath);
        Assert.Null(options.TextFiltersPath);
        Assert.Null(options.IconsPath);
    }

    [Theory]
    [InlineData("CapsLock", KeyCode.CapsLock)]
    [InlineData("capslock", KeyCode.CapsLock)]
    [InlineData("CAPSLOCK", KeyCode.CapsLock)]
    [InlineData("Space", KeyCode.Space)]
    [InlineData("F8", KeyCode.F8)]
    public void GetTriggerKeyCode_ShouldParseValidKey(string keyName, KeyCode expected)
    {
        // Arrange
        var options = new DictationOptions { TriggerKey = keyName };

        // Act
        var result = options.GetTriggerKeyCode();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("InvalidKey")]
    [InlineData("")]
    [InlineData("NotAKey123")]
    public void GetTriggerKeyCode_ShouldReturnCapsLock_ForInvalidKey(string keyName)
    {
        // Arrange
        var options = new DictationOptions { TriggerKey = keyName };

        // Act
        var result = options.GetTriggerKeyCode();

        // Assert
        Assert.Equal(KeyCode.CapsLock, result);
    }

    [Theory]
    [InlineData("Escape", KeyCode.Escape)]
    [InlineData("escape", KeyCode.Escape)]
    [InlineData("Enter", KeyCode.Enter)]
    public void GetCancelKeyCode_ShouldParseValidKey(string keyName, KeyCode expected)
    {
        // Arrange
        var options = new DictationOptions { CancelKey = keyName };

        // Act
        var result = options.GetCancelKeyCode();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("InvalidKey")]
    [InlineData("")]
    public void GetCancelKeyCode_ShouldReturnEscape_ForInvalidKey(string keyName)
    {
        // Arrange
        var options = new DictationOptions { CancelKey = keyName };

        // Act
        var result = options.GetCancelKeyCode();

        // Assert
        Assert.Equal(KeyCode.Escape, result);
    }

    [Fact]
    public void GetFullTranscriptionSoundPath_WhenNull_ShouldReturnNull()
    {
        // Arrange
        var options = new DictationOptions { TranscriptionSoundPath = null };

        // Act
        var result = options.GetFullTranscriptionSoundPath();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetFullTranscriptionSoundPath_WhenEmpty_ShouldReturnNull()
    {
        // Arrange
        var options = new DictationOptions { TranscriptionSoundPath = "" };

        // Act
        var result = options.GetFullTranscriptionSoundPath();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetFullTranscriptionSoundPath_WithRelativePath_ShouldCombineWithBaseDirectory()
    {
        // Arrange
        var options = new DictationOptions { TranscriptionSoundPath = "sounds/beep.wav" };

        // Act
        var result = options.GetFullTranscriptionSoundPath();

        // Assert
        Assert.NotNull(result);
        Assert.Contains("sounds", result);
        Assert.Contains("beep.wav", result);
        Assert.True(Path.IsPathRooted(result));
    }

    [Fact]
    public void GetFullTextFiltersPath_WhenNull_ShouldReturnNull()
    {
        // Arrange
        var options = new DictationOptions { TextFiltersPath = null };

        // Act
        var result = options.GetFullTextFiltersPath();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetFullTextFiltersPath_WithAbsolutePath_ShouldReturnAsIs()
    {
        // Arrange
        var absolutePath = "/etc/filters.txt";
        var options = new DictationOptions { TextFiltersPath = absolutePath };

        // Act
        var result = options.GetFullTextFiltersPath();

        // Assert
        Assert.Equal(absolutePath, result);
    }

    [Fact]
    public void AnimationFrames_ShouldHaveDefaultValues()
    {
        // Arrange
        var options = new DictationOptions();

        // Assert
        Assert.NotNull(options.AnimationFrames);
        Assert.Equal(5, options.AnimationFrames.Length);
        Assert.Contains("document-white-frame1", options.AnimationFrames);
        Assert.Contains("document-white-frame5", options.AnimationFrames);
    }
}
