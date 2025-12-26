using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.PushToTalk.App;

namespace Olbrasoft.PushToTalk.App.Tests;

public class TextFilterTests
{
    private readonly Mock<ILogger<TextFilter>> _loggerMock;

    public TextFilterTests()
    {
        _loggerMock = new Mock<ILogger<TextFilter>>();
    }

    [Fact]
    public void Constructor_WithNullConfigPath_ShouldDisableFiltering()
    {
        // Act
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: null);

        // Assert
        Assert.False(filter.IsEnabled);
        Assert.Equal(0, filter.PatternCount);
    }

    [Fact]
    public void Constructor_WithEmptyConfigPath_ShouldDisableFiltering()
    {
        // Act
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: "");

        // Assert
        Assert.False(filter.IsEnabled);
    }

    [Fact]
    public void Constructor_WithWhitespaceConfigPath_ShouldDisableFiltering()
    {
        // Act
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: "   ");

        // Assert
        Assert.False(filter.IsEnabled);
    }

    [Fact]
    public void Constructor_WithNonExistentPath_ShouldNotThrow()
    {
        // Arrange
        var nonExistentPath = "/tmp/non_existent_filter_config_12345.json";

        // Act
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: nonExistentPath);

        // Assert
        Assert.False(filter.IsEnabled);
    }

    [Fact]
    public void Apply_WithNullText_ShouldReturnEmptyString()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: null);

        // Act
        var result = filter.Apply(null);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Apply_WithEmptyText_ShouldReturnEmptyString()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: null);

        // Act
        var result = filter.Apply("");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Apply_WithWhitespaceText_ShouldReturnEmptyString()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: null);

        // Act
        var result = filter.Apply("   ");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Apply_WithNoFilters_ShouldReturnTrimmedText()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: null);

        // Act
        var result = filter.Apply("  Hello World  ");

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void Apply_ShouldNormalizeMultipleSpaces()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: null);

        // Act
        var result = filter.Apply("Hello    World");

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void IsEnabled_WithNoPatterns_ShouldReturnFalse()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: null);

        // Assert
        Assert.False(filter.IsEnabled);
    }

    [Fact]
    public void PatternCount_WithNoPatterns_ShouldReturnZero()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: null);

        // Assert
        Assert.Equal(0, filter.PatternCount);
    }

    [Fact]
    public void Reload_WithNoConfig_ShouldNotThrow()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: null);

        // Act & Assert - should not throw
        filter.Reload();
    }

    [Fact]
    public void Apply_WithValidText_ShouldPreserveContent()
    {
        // Arrange
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: null);
        var text = "This is a valid transcription.";

        // Act
        var result = filter.Apply(text);

        // Assert
        Assert.Equal("This is a valid transcription.", result);
    }
}

public class TextFilterWithConfigTests : IDisposable
{
    private readonly Mock<ILogger<TextFilter>> _loggerMock;
    private readonly string _tempConfigPath;

    public TextFilterWithConfigTests()
    {
        _loggerMock = new Mock<ILogger<TextFilter>>();
        _tempConfigPath = Path.Combine(Path.GetTempPath(), $"text_filter_test_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempConfigPath))
        {
            File.Delete(_tempConfigPath);
        }
    }

    [Fact]
    public void Constructor_WithValidConfig_ShouldLoadPatterns()
    {
        // Arrange
        var config = """{"remove": ["pattern1", "pattern2"]}""";
        File.WriteAllText(_tempConfigPath, config);

        // Act
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: _tempConfigPath);

        // Assert
        Assert.True(filter.IsEnabled);
        Assert.Equal(2, filter.PatternCount);
    }

    [Fact]
    public void Apply_WithMatchingPattern_ShouldRemovePattern()
    {
        // Arrange
        var config = """{"remove": ["[music]", "[applause]"]}""";
        File.WriteAllText(_tempConfigPath, config);
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: _tempConfigPath);

        // Act
        var result = filter.Apply("Hello [music] World");

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void Apply_WithCaseInsensitiveMatch_ShouldRemovePattern()
    {
        // Arrange
        var config = """{"remove": ["HELLO"]}""";
        File.WriteAllText(_tempConfigPath, config);
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: _tempConfigPath);

        // Act
        var result = filter.Apply("hello world");

        // Assert
        Assert.Equal("world", result);
    }

    [Fact]
    public void Apply_WithMultiplePatterns_ShouldRemoveAll()
    {
        // Arrange
        var config = """{"remove": ["[music]", "[laughter]", "[applause]"]}""";
        File.WriteAllText(_tempConfigPath, config);
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: _tempConfigPath);

        // Act
        var result = filter.Apply("[music] Hello [laughter] World [applause]");

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void Apply_WithNoMatchingPattern_ShouldReturnOriginal()
    {
        // Arrange
        var config = """{"remove": ["[music]"]}""";
        File.WriteAllText(_tempConfigPath, config);
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: _tempConfigPath);

        // Act
        var result = filter.Apply("Hello World");

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void Reload_ShouldReloadPatterns()
    {
        // Arrange
        var config1 = """{"remove": ["pattern1"]}""";
        File.WriteAllText(_tempConfigPath, config1);
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: _tempConfigPath);
        Assert.Equal(1, filter.PatternCount);

        // Act - update config and reload
        var config2 = """{"remove": ["pattern1", "pattern2", "pattern3"]}""";
        File.WriteAllText(_tempConfigPath, config2);
        filter.Reload();

        // Assert
        Assert.Equal(3, filter.PatternCount);
    }

    [Fact]
    public void Constructor_WithEmptyRemoveArray_ShouldHaveZeroPatterns()
    {
        // Arrange
        var config = """{"remove": []}""";
        File.WriteAllText(_tempConfigPath, config);

        // Act
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: _tempConfigPath);

        // Assert
        Assert.False(filter.IsEnabled);
        Assert.Equal(0, filter.PatternCount);
    }

    [Fact]
    public void Constructor_WithWhitespacePatterns_ShouldFilterThem()
    {
        // Arrange
        var config = """{"remove": ["valid", "", "  ", "another"]}""";
        File.WriteAllText(_tempConfigPath, config);

        // Act
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: _tempConfigPath);

        // Assert
        Assert.Equal(2, filter.PatternCount);
    }

    [Fact]
    public void Constructor_WithInvalidJson_ShouldNotThrow()
    {
        // Arrange
        File.WriteAllText(_tempConfigPath, "invalid json {{{");

        // Act
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: _tempConfigPath);

        // Assert
        Assert.False(filter.IsEnabled);
    }

    [Fact]
    public void Apply_ShouldNormalizeSpacesAfterFiltering()
    {
        // Arrange
        var config = """{"remove": ["[pause]"]}""";
        File.WriteAllText(_tempConfigPath, config);
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: _tempConfigPath);

        // Act
        var result = filter.Apply("Hello    [pause]    World");

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void Apply_WithPatternAtStart_ShouldRemoveAndTrim()
    {
        // Arrange
        var config = """{"remove": ["[intro]"]}""";
        File.WriteAllText(_tempConfigPath, config);
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: _tempConfigPath);

        // Act
        var result = filter.Apply("[intro] Welcome everyone");

        // Assert
        Assert.Equal("Welcome everyone", result);
    }

    [Fact]
    public void Apply_WithPatternAtEnd_ShouldRemoveAndTrim()
    {
        // Arrange
        var config = """{"remove": ["[outro]"]}""";
        File.WriteAllText(_tempConfigPath, config);
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: _tempConfigPath);

        // Act
        var result = filter.Apply("Goodbye everyone [outro]");

        // Assert
        Assert.Equal("Goodbye everyone", result);
    }

    [Fact]
    public void Apply_EntireTextIsPattern_ShouldReturnEmpty()
    {
        // Arrange
        var config = """{"remove": ["[silence]"]}""";
        File.WriteAllText(_tempConfigPath, config);
        var filter = new TextFilter(_loggerMock.Object, correctionRepository: null, configPath: _tempConfigPath);

        // Act
        var result = filter.Apply("[silence]");

        // Assert
        Assert.Equal(string.Empty, result);
    }
}

public class TextFiltersConfigTests
{
    [Fact]
    public void Remove_Default_ShouldBeEmptyList()
    {
        // Act
        var config = new TextFiltersConfig();

        // Assert
        Assert.NotNull(config.Remove);
        Assert.Empty(config.Remove);
    }

    [Fact]
    public void Remove_ShouldBeSettable()
    {
        // Arrange
        var config = new TextFiltersConfig();

        // Act
        config.Remove = new List<string> { "pattern1", "pattern2" };

        // Assert
        Assert.Equal(2, config.Remove.Count);
    }
}
