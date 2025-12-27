using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.PushToTalk.Service.Services;

namespace Olbrasoft.PushToTalk.Service.Tests.Services;

public class WhisperHallucinationFilterTests
{
    private readonly Mock<ILogger<WhisperHallucinationFilter>> _loggerMock;
    private readonly HallucinationFilterOptions _options;
    private readonly WhisperHallucinationFilter _filter;

    public WhisperHallucinationFilterTests()
    {
        _loggerMock = new Mock<ILogger<WhisperHallucinationFilter>>();
        _options = new HallucinationFilterOptions();
        var optionsMock = new Mock<IOptions<HallucinationFilterOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_options);

        _filter = new WhisperHallucinationFilter(_loggerMock.Object, optionsMock.Object);
    }

    [Fact]
    public void TryClean_WithNull_ReturnsFalse()
    {
        // Act
        var result = _filter.TryClean(null, out var cleaned);

        // Assert
        Assert.False(result);
        Assert.Empty(cleaned);
    }

    [Fact]
    public void TryClean_WithEmptyString_ReturnsFalse()
    {
        // Act
        var result = _filter.TryClean("", out var cleaned);

        // Assert
        Assert.False(result);
        Assert.Empty(cleaned);
    }

    [Fact]
    public void TryClean_WithWhitespace_ReturnsFalse()
    {
        // Act
        var result = _filter.TryClean("   ", out var cleaned);

        // Assert
        Assert.False(result);
        Assert.Empty(cleaned);
    }

    [Fact]
    public void TryClean_WithValidText_ReturnsTrue()
    {
        // Act
        var result = _filter.TryClean("Hello world", out var cleaned);

        // Assert
        Assert.True(result);
        Assert.Equal("Hello world", cleaned);
    }

    [Fact]
    public void TryClean_WithKnownHallucination_ReturnsFalse()
    {
        // Act
        var result = _filter.TryClean("titulky vytvořil johnyx", out var cleaned);

        // Assert
        Assert.False(result);
        Assert.Empty(cleaned);
    }

    [Fact]
    public void TryClean_WithHallucinationInText_RemovesHallucination()
    {
        // Act
        var result = _filter.TryClean("Hello titulky vytvořil johnyx world", out var cleaned);

        // Assert
        Assert.True(result);
        Assert.Equal("Hello world", cleaned);
    }

    [Fact]
    public void TryClean_CaseInsensitive()
    {
        // Act
        var result = _filter.TryClean("TITULKY VYTVOŘIL JOHNYX", out var cleaned);

        // Assert
        Assert.False(result);
        Assert.Empty(cleaned);
    }

    [Fact]
    public void TryClean_WithMultipleHallucinations_RemovesAll()
    {
        // Act
        var result = _filter.TryClean("Hello thank you for watching world please subscribe test", out var cleaned);

        // Assert
        Assert.True(result);
        Assert.Equal("Hello world test", cleaned);
    }

    [Fact]
    public void TryClean_WithTooShortResult_ReturnsFalse()
    {
        // Arrange - set minimum length to 5
        _options.MinimumTextLength = 5;

        // Act
        var result = _filter.TryClean("Hi", out var cleaned);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryClean_TrimsWhitespace()
    {
        // Act
        var result = _filter.TryClean("  Hello world  ", out var cleaned);

        // Assert
        Assert.True(result);
        Assert.Equal("Hello world", cleaned);
    }

    [Fact]
    public void TryClean_NormalizesMultipleSpaces()
    {
        // Arrange - text with hallucination that leaves double spaces
        var text = "Hello  titulky:  world";

        // Act
        var result = _filter.TryClean(text, out var cleaned);

        // Assert
        Assert.True(result);
        Assert.DoesNotContain("  ", cleaned);
    }

    [Fact]
    public void TryClean_WithEnglishHallucination_RemovesIt()
    {
        // Act
        var result = _filter.TryClean("Great video Thanks for watching", out var cleaned);

        // Assert
        Assert.True(result);
        Assert.Equal("Great video", cleaned);
    }

    [Theory]
    [InlineData("subtitles by johnyx")]
    [InlineData("thank you for watching")]
    [InlineData("please subscribe")]
    [InlineData("like and subscribe")]
    public void TryClean_FiltersAllKnownHallucinations(string hallucination)
    {
        // Act
        var result = _filter.TryClean(hallucination, out var cleaned);

        // Assert
        Assert.False(result);
        Assert.Empty(cleaned);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var optionsMock = new Mock<IOptions<HallucinationFilterOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new HallucinationFilterOptions());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new WhisperHallucinationFilter(null!, optionsMock.Object));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new WhisperHallucinationFilter(_loggerMock.Object, null!));
    }
}
