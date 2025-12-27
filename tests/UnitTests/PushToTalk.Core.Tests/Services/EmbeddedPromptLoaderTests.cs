using System.Reflection;
using Olbrasoft.PushToTalk.Core.Services;

namespace Olbrasoft.PushToTalk.Core.Tests.Services;

public class EmbeddedPromptLoaderTests
{
    [Fact]
    public void LoadPrompt_WithValidPromptName_ReturnsPromptContent()
    {
        // Arrange
        var loader = new EmbeddedPromptLoader();

        // Act
        var result = loader.LoadPrompt("MistralSystemPrompt");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("Jsi expert na opravu českých ASR", result);
        Assert.Contains("VRAŤ JEN OPRAVENOU TRANSKRIPCI", result);
    }

    [Fact]
    public void LoadPrompt_WithNullPromptName_ThrowsArgumentException()
    {
        // Arrange
        var loader = new EmbeddedPromptLoader();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => loader.LoadPrompt(null!));
        Assert.Equal("promptName", exception.ParamName);
    }

    [Fact]
    public void LoadPrompt_WithEmptyPromptName_ThrowsArgumentException()
    {
        // Arrange
        var loader = new EmbeddedPromptLoader();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => loader.LoadPrompt(""));
        Assert.Equal("promptName", exception.ParamName);
    }

    [Fact]
    public void LoadPrompt_WithWhitespacePromptName_ThrowsArgumentException()
    {
        // Arrange
        var loader = new EmbeddedPromptLoader();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => loader.LoadPrompt("   "));
        Assert.Equal("promptName", exception.ParamName);
    }

    [Fact]
    public void LoadPrompt_WithNonExistentPrompt_ThrowsFileNotFoundException()
    {
        // Arrange
        var loader = new EmbeddedPromptLoader();

        // Act & Assert
        var exception = Assert.Throws<FileNotFoundException>(() => loader.LoadPrompt("NonExistentPrompt"));
        Assert.Contains("Olbrasoft.PushToTalk.Core.Prompts.NonExistentPrompt.md", exception.Message);
    }

    [Fact]
    public void LoadPrompt_WithCustomAssemblyAndNamespace_ReturnsPromptContent()
    {
        // Arrange
        // Load from the PushToTalk.Core assembly which contains the embedded prompts
        var assembly = typeof(EmbeddedPromptLoader).Assembly;
        var loader = new EmbeddedPromptLoader(assembly, "Olbrasoft.PushToTalk.Core.Prompts");

        // Act
        var result = loader.LoadPrompt("MistralSystemPrompt");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("Jsi expert na opravu českých ASR", result);
    }

    [Fact]
    public void Constructor_WithNullAssembly_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(
            () => new EmbeddedPromptLoader(null!, "Olbrasoft.PushToTalk.Core.Prompts"));
        Assert.Equal("assembly", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullBaseNamespace_ThrowsArgumentNullException()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(
            () => new EmbeddedPromptLoader(assembly, null!));
        Assert.Equal("baseNamespace", exception.ParamName);
    }
}
