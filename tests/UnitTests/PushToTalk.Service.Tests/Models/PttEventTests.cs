using Olbrasoft.PushToTalk.Service.Models;

namespace Olbrasoft.PushToTalk.Service.Tests.Models;

public class PttEventTests
{
    [Fact]
    public void EventType_ShouldBeSettable()
    {
        // Act
        var pttEvent = new PttEvent { EventType = PttEventType.RecordingStarted };

        // Assert
        Assert.Equal(PttEventType.RecordingStarted, pttEvent.EventType);
    }

    [Fact]
    public void Timestamp_Default_ShouldBeCloseToNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var pttEvent = new PttEvent();

        // Assert
        var after = DateTime.UtcNow;
        Assert.InRange(pttEvent.Timestamp, before, after);
    }

    [Fact]
    public void Text_ShouldBeSettable()
    {
        // Act
        var pttEvent = new PttEvent { Text = "Hello World" };

        // Assert
        Assert.Equal("Hello World", pttEvent.Text);
    }

    [Fact]
    public void Text_Default_ShouldBeNull()
    {
        // Act
        var pttEvent = new PttEvent();

        // Assert
        Assert.Null(pttEvent.Text);
    }

    [Fact]
    public void Confidence_ShouldBeSettable()
    {
        // Act
        var pttEvent = new PttEvent { Confidence = 0.95f };

        // Assert
        Assert.Equal(0.95f, pttEvent.Confidence);
    }

    [Fact]
    public void Confidence_Default_ShouldBeNull()
    {
        // Act
        var pttEvent = new PttEvent();

        // Assert
        Assert.Null(pttEvent.Confidence);
    }

    [Fact]
    public void DurationSeconds_ShouldBeSettable()
    {
        // Act
        var pttEvent = new PttEvent { DurationSeconds = 5.5 };

        // Assert
        Assert.Equal(5.5, pttEvent.DurationSeconds);
    }

    [Fact]
    public void DurationSeconds_Default_ShouldBeNull()
    {
        // Act
        var pttEvent = new PttEvent();

        // Assert
        Assert.Null(pttEvent.DurationSeconds);
    }

    [Fact]
    public void ErrorMessage_ShouldBeSettable()
    {
        // Act
        var pttEvent = new PttEvent { ErrorMessage = "Transcription failed" };

        // Assert
        Assert.Equal("Transcription failed", pttEvent.ErrorMessage);
    }

    [Fact]
    public void ErrorMessage_Default_ShouldBeNull()
    {
        // Act
        var pttEvent = new PttEvent();

        // Assert
        Assert.Null(pttEvent.ErrorMessage);
    }

    [Fact]
    public void ServiceVersion_Default_ShouldBe1_0_0()
    {
        // Act
        var pttEvent = new PttEvent();

        // Assert
        Assert.Equal("1.0.0", pttEvent.ServiceVersion);
    }

    [Fact]
    public void ServiceVersion_ShouldBeSettable()
    {
        // Act
        var pttEvent = new PttEvent { ServiceVersion = "2.0.0" };

        // Assert
        Assert.Equal("2.0.0", pttEvent.ServiceVersion);
    }

    [Fact]
    public void TranscriptionCompletedEvent_ShouldHaveTextAndConfidence()
    {
        // Act
        var pttEvent = new PttEvent
        {
            EventType = PttEventType.TranscriptionCompleted,
            Text = "Transcribed text",
            Confidence = 0.92f,
            DurationSeconds = 3.2
        };

        // Assert
        Assert.Equal(PttEventType.TranscriptionCompleted, pttEvent.EventType);
        Assert.Equal("Transcribed text", pttEvent.Text);
        Assert.Equal(0.92f, pttEvent.Confidence);
        Assert.Equal(3.2, pttEvent.DurationSeconds);
    }

    [Fact]
    public void TranscriptionFailedEvent_ShouldHaveErrorMessage()
    {
        // Act
        var pttEvent = new PttEvent
        {
            EventType = PttEventType.TranscriptionFailed,
            ErrorMessage = "Model not loaded"
        };

        // Assert
        Assert.Equal(PttEventType.TranscriptionFailed, pttEvent.EventType);
        Assert.Equal("Model not loaded", pttEvent.ErrorMessage);
    }

    [Fact]
    public void IsRecord()
    {
        // Act
        var pttEvent1 = new PttEvent { EventType = PttEventType.RecordingStarted };
        var pttEvent2 = new PttEvent { EventType = PttEventType.RecordingStarted };

        // Assert - records with same values should be equal
        Assert.Equal(pttEvent1.EventType, pttEvent2.EventType);
    }
}

public class PttEventTypeTests
{
    [Fact]
    public void RecordingStarted_ShouldBeDefaultValue()
    {
        // Arrange
        PttEventType defaultType = default;

        // Assert
        Assert.Equal(PttEventType.RecordingStarted, defaultType);
    }

    [Theory]
    [InlineData(PttEventType.RecordingStarted)]
    [InlineData(PttEventType.RecordingStopped)]
    [InlineData(PttEventType.TranscriptionStarted)]
    [InlineData(PttEventType.TranscriptionCompleted)]
    [InlineData(PttEventType.TranscriptionFailed)]
    [InlineData(PttEventType.ManualMuteOn)]
    [InlineData(PttEventType.ManualMuteOff)]
    public void AllEventTypes_ShouldBeValid(PttEventType eventType)
    {
        // Assert
        Assert.True(Enum.IsDefined(eventType));
    }

    [Fact]
    public void AllEventTypes_ShouldBeDistinct()
    {
        // Arrange
        var allTypes = Enum.GetValues<PttEventType>();

        // Act
        var distinctCount = allTypes.Distinct().Count();

        // Assert
        Assert.Equal(allTypes.Length, distinctCount);
    }

    [Fact]
    public void TotalEventTypes_ShouldBeSeven()
    {
        // Arrange
        var allTypes = Enum.GetValues<PttEventType>();

        // Assert
        Assert.Equal(7, allTypes.Length);
    }
}
