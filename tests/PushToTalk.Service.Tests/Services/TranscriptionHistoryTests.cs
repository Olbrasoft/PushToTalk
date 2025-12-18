using Olbrasoft.PushToTalk.Service.Services;

namespace Olbrasoft.PushToTalk.Service.Tests.Services;

public class TranscriptionHistoryTests
{
    private readonly TranscriptionHistory _history;

    public TranscriptionHistoryTests()
    {
        _history = new TranscriptionHistory();
    }

    [Fact]
    public void LastText_Initially_ShouldBeNull()
    {
        // Assert
        Assert.Null(_history.LastText);
    }

    [Fact]
    public void SaveText_ShouldStoreText()
    {
        // Arrange
        var text = "Hello World";

        // Act
        _history.SaveText(text);

        // Assert
        Assert.Equal(text, _history.LastText);
    }

    [Fact]
    public void SaveText_ShouldOverwritePreviousText()
    {
        // Arrange
        _history.SaveText("First text");

        // Act
        _history.SaveText("Second text");

        // Assert
        Assert.Equal("Second text", _history.LastText);
    }

    [Fact]
    public void SaveText_WithNull_ShouldNotSave()
    {
        // Arrange
        _history.SaveText("Initial text");

        // Act
        _history.SaveText(null!);

        // Assert
        Assert.Equal("Initial text", _history.LastText);
    }

    [Fact]
    public void SaveText_WithEmptyString_ShouldNotSave()
    {
        // Arrange
        _history.SaveText("Initial text");

        // Act
        _history.SaveText("");

        // Assert
        Assert.Equal("Initial text", _history.LastText);
    }

    [Fact]
    public void SaveText_WithWhitespace_ShouldNotSave()
    {
        // Arrange
        _history.SaveText("Initial text");

        // Act
        _history.SaveText("   ");

        // Assert
        Assert.Equal("Initial text", _history.LastText);
    }

    [Fact]
    public void Clear_ShouldSetLastTextToNull()
    {
        // Arrange
        _history.SaveText("Some text");

        // Act
        _history.Clear();

        // Assert
        Assert.Null(_history.LastText);
    }

    [Fact]
    public void Clear_WhenEmpty_ShouldNotThrow()
    {
        // Act & Assert - should not throw
        _history.Clear();
        Assert.Null(_history.LastText);
    }

    [Fact]
    public void ImplementsITranscriptionHistory()
    {
        // Assert
        Assert.IsAssignableFrom<ITranscriptionHistory>(_history);
    }

    [Fact]
    public async Task SaveText_IsThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var texts = Enumerable.Range(1, 100).Select(i => $"Text {i}").ToList();

        // Act
        foreach (var text in texts)
        {
            tasks.Add(Task.Run(() => _history.SaveText(text)));
        }

        await Task.WhenAll(tasks);

        // Assert - should have some text (last one wins, but no exceptions)
        Assert.NotNull(_history.LastText);
        Assert.StartsWith("Text ", _history.LastText);
    }

    [Fact]
    public async Task LastText_IsThreadSafe()
    {
        // Arrange
        _history.SaveText("Concurrent text");
        var tasks = new List<Task<string?>>();

        // Act - multiple concurrent reads
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => _history.LastText));
        }

        await Task.WhenAll(tasks);

        // Assert - all reads should return the same value
        Assert.All(tasks, t => Assert.Equal("Concurrent text", t.Result));
    }
}
