using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.PushToTalk.Service.Services;
using Olbrasoft.PushToTalk.Services;

namespace Olbrasoft.PushToTalk.Service.Tests.Services;

public class ManualMuteServiceTests
{
    private readonly Mock<ILogger<ManualMuteService>> _loggerMock;
    private readonly ManualMuteService _service;

    public ManualMuteServiceTests()
    {
        _loggerMock = new Mock<ILogger<ManualMuteService>>();
        _service = new ManualMuteService(_loggerMock.Object);
    }

    [Fact]
    public void IsMuted_Initially_ShouldBeFalse()
    {
        // Assert
        Assert.False(_service.IsMuted);
    }

    [Fact]
    public void Toggle_WhenUnmuted_ShouldReturnTrue()
    {
        // Act
        var result = _service.Toggle();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Toggle_WhenUnmuted_ShouldSetIsMutedToTrue()
    {
        // Act
        _service.Toggle();

        // Assert
        Assert.True(_service.IsMuted);
    }

    [Fact]
    public void Toggle_WhenMuted_ShouldReturnFalse()
    {
        // Arrange
        _service.Toggle(); // Mute

        // Act
        var result = _service.Toggle(); // Unmute

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Toggle_TwiceShouldReturnToOriginalState()
    {
        // Act
        _service.Toggle();
        _service.Toggle();

        // Assert
        Assert.False(_service.IsMuted);
    }

    [Fact]
    public void SetMuted_ToTrue_ShouldMute()
    {
        // Act
        _service.SetMuted(true);

        // Assert
        Assert.True(_service.IsMuted);
    }

    [Fact]
    public void SetMuted_ToFalse_ShouldUnmute()
    {
        // Arrange
        _service.SetMuted(true);

        // Act
        _service.SetMuted(false);

        // Assert
        Assert.False(_service.IsMuted);
    }

    [Fact]
    public void SetMuted_ToSameValue_ShouldNotRaiseEvent()
    {
        // Arrange
        var eventRaised = false;
        _service.MuteStateChanged += (_, _) => eventRaised = true;

        // Already unmuted, set to unmuted
        // Act
        _service.SetMuted(false);

        // Assert
        Assert.False(eventRaised);
    }

    [Fact]
    public void Toggle_ShouldRaiseMuteStateChangedEvent()
    {
        // Arrange
        bool? eventValue = null;
        _service.MuteStateChanged += (_, value) => eventValue = value;

        // Act
        _service.Toggle();

        // Assert
        Assert.True(eventValue);
    }

    [Fact]
    public void SetMuted_ShouldRaiseMuteStateChangedEvent()
    {
        // Arrange
        bool? eventValue = null;
        _service.MuteStateChanged += (_, value) => eventValue = value;

        // Act
        _service.SetMuted(true);

        // Assert
        Assert.True(eventValue);
    }

    [Fact]
    public void MuteStateChanged_ShouldProvideCorrectValue()
    {
        // Arrange
        var receivedValues = new List<bool>();
        _service.MuteStateChanged += (_, value) => receivedValues.Add(value);

        // Act
        _service.Toggle(); // Mute
        _service.Toggle(); // Unmute

        // Assert
        Assert.Equal(2, receivedValues.Count);
        Assert.True(receivedValues[0]);
        Assert.False(receivedValues[1]);
    }

    [Fact]
    public void ImplementsIManualMuteService()
    {
        // Assert
        Assert.IsAssignableFrom<IManualMuteService>(_service);
    }

    [Fact]
    public async Task IsMuted_IsThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - concurrent toggles
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => _service.Toggle()));
        }

        await Task.WhenAll(tasks);

        // Assert - even number of toggles should end up at original state
        Assert.False(_service.IsMuted);
    }
}
