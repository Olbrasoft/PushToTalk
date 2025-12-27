using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.PushToTalk.App;
using Olbrasoft.PushToTalk.App.StateMachine;

namespace PushToTalk.App.Tests.StateMachine;

public class DictationStateMachineTests
{
    private readonly Mock<ILogger<DictationStateMachine>> _mockLogger;
    private readonly DictationStateMachine _stateMachine;

    public DictationStateMachineTests()
    {
        _mockLogger = new Mock<ILogger<DictationStateMachine>>();
        _stateMachine = new DictationStateMachine(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new DictationStateMachine(null!));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act & Assert
        Assert.NotNull(_stateMachine);
        Assert.Equal(DictationState.Idle, _stateMachine.CurrentState);
    }

    [Fact]
    public void CurrentState_InitiallyIdle()
    {
        // Assert
        Assert.Equal(DictationState.Idle, _stateMachine.CurrentState);
    }

    #region Valid Transitions

    [Fact]
    public void CanTransitionTo_IdleToRecording_ReturnsTrue()
    {
        // Assert
        Assert.True(_stateMachine.CanTransitionTo(DictationState.Recording));
    }

    [Fact]
    public void CanTransitionTo_RecordingToTranscribing_ReturnsTrue()
    {
        // Arrange
        _stateMachine.TransitionTo(DictationState.Recording);

        // Act & Assert
        Assert.True(_stateMachine.CanTransitionTo(DictationState.Transcribing));
    }

    [Fact]
    public void CanTransitionTo_RecordingToIdle_ReturnsTrue()
    {
        // Arrange
        _stateMachine.TransitionTo(DictationState.Recording);

        // Act & Assert
        Assert.True(_stateMachine.CanTransitionTo(DictationState.Idle));
    }

    [Fact]
    public void CanTransitionTo_TranscribingToIdle_ReturnsTrue()
    {
        // Arrange
        _stateMachine.TransitionTo(DictationState.Recording);
        _stateMachine.TransitionTo(DictationState.Transcribing);

        // Act & Assert
        Assert.True(_stateMachine.CanTransitionTo(DictationState.Idle));
    }

    [Theory]
    [InlineData(DictationState.Idle)]
    [InlineData(DictationState.Recording)]
    [InlineData(DictationState.Transcribing)]
    public void CanTransitionTo_SameState_ReturnsTrue(DictationState state)
    {
        // Arrange - transition to target state
        if (state == DictationState.Recording)
        {
            _stateMachine.TransitionTo(DictationState.Recording);
        }
        else if (state == DictationState.Transcribing)
        {
            _stateMachine.TransitionTo(DictationState.Recording);
            _stateMachine.TransitionTo(DictationState.Transcribing);
        }

        // Act & Assert - transitioning to same state should be allowed (idempotent)
        Assert.True(_stateMachine.CanTransitionTo(state));
    }

    #endregion

    #region Invalid Transitions

    [Fact]
    public void CanTransitionTo_IdleToTranscribing_ReturnsFalse()
    {
        // Assert - cannot transcribe without recording first
        Assert.False(_stateMachine.CanTransitionTo(DictationState.Transcribing));
    }

    [Fact]
    public void CanTransitionTo_TranscribingToRecording_ReturnsFalse()
    {
        // Arrange
        _stateMachine.TransitionTo(DictationState.Recording);
        _stateMachine.TransitionTo(DictationState.Transcribing);

        // Act & Assert - cannot start recording while transcribing
        Assert.False(_stateMachine.CanTransitionTo(DictationState.Recording));
    }

    #endregion

    #region TransitionTo Tests

    [Fact]
    public void TransitionTo_IdleToRecording_ChangesState()
    {
        // Act
        _stateMachine.TransitionTo(DictationState.Recording);

        // Assert
        Assert.Equal(DictationState.Recording, _stateMachine.CurrentState);
    }

    [Fact]
    public void TransitionTo_RecordingToTranscribing_ChangesState()
    {
        // Arrange
        _stateMachine.TransitionTo(DictationState.Recording);

        // Act
        _stateMachine.TransitionTo(DictationState.Transcribing);

        // Assert
        Assert.Equal(DictationState.Transcribing, _stateMachine.CurrentState);
    }

    [Fact]
    public void TransitionTo_RecordingToIdle_ChangesState()
    {
        // Arrange
        _stateMachine.TransitionTo(DictationState.Recording);

        // Act - emergency cancel
        _stateMachine.TransitionTo(DictationState.Idle);

        // Assert
        Assert.Equal(DictationState.Idle, _stateMachine.CurrentState);
    }

    [Fact]
    public void TransitionTo_TranscribingToIdle_ChangesState()
    {
        // Arrange
        _stateMachine.TransitionTo(DictationState.Recording);
        _stateMachine.TransitionTo(DictationState.Transcribing);

        // Act
        _stateMachine.TransitionTo(DictationState.Idle);

        // Assert
        Assert.Equal(DictationState.Idle, _stateMachine.CurrentState);
    }

    [Fact]
    public void TransitionTo_SameState_DoesNotChangeState()
    {
        // Arrange
        _stateMachine.TransitionTo(DictationState.Recording);

        // Act - idempotent transition
        _stateMachine.TransitionTo(DictationState.Recording);

        // Assert - state unchanged
        Assert.Equal(DictationState.Recording, _stateMachine.CurrentState);
    }

    [Fact]
    public void TransitionTo_InvalidTransition_ThrowsInvalidOperationException()
    {
        // Act & Assert - cannot transcribe without recording
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _stateMachine.TransitionTo(DictationState.Transcribing));

        Assert.Contains("Invalid state transition", exception.Message);
        Assert.Contains("Idle → Transcribing", exception.Message);
    }

    [Fact]
    public void TransitionTo_TranscribingToRecording_ThrowsInvalidOperationException()
    {
        // Arrange
        _stateMachine.TransitionTo(DictationState.Recording);
        _stateMachine.TransitionTo(DictationState.Transcribing);

        // Act & Assert - cannot start recording while transcribing
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _stateMachine.TransitionTo(DictationState.Recording));

        Assert.Contains("Invalid state transition", exception.Message);
        Assert.Contains("Transcribing → Recording", exception.Message);
    }

    #endregion

    #region StateChanged Event Tests

    [Fact]
    public void TransitionTo_ValidTransition_RaisesStateChangedEvent()
    {
        // Arrange
        DictationState? raisedState = null;
        _stateMachine.StateChanged += (sender, state) => raisedState = state;

        // Act
        _stateMachine.TransitionTo(DictationState.Recording);

        // Assert
        Assert.Equal(DictationState.Recording, raisedState);
    }

    [Fact]
    public void TransitionTo_SameState_DoesNotRaiseStateChangedEvent()
    {
        // Arrange
        _stateMachine.TransitionTo(DictationState.Recording);

        var eventRaised = false;
        _stateMachine.StateChanged += (sender, state) => eventRaised = true;

        // Act - idempotent transition
        _stateMachine.TransitionTo(DictationState.Recording);

        // Assert
        Assert.False(eventRaised);
    }

    [Fact]
    public void TransitionTo_MultipleTransitions_RaisesEventForEach()
    {
        // Arrange
        var raisedStates = new List<DictationState>();
        _stateMachine.StateChanged += (sender, state) => raisedStates.Add(state);

        // Act
        _stateMachine.TransitionTo(DictationState.Recording);
        _stateMachine.TransitionTo(DictationState.Transcribing);
        _stateMachine.TransitionTo(DictationState.Idle);

        // Assert
        Assert.Equal(3, raisedStates.Count);
        Assert.Equal(DictationState.Recording, raisedStates[0]);
        Assert.Equal(DictationState.Transcribing, raisedStates[1]);
        Assert.Equal(DictationState.Idle, raisedStates[2]);
    }

    [Fact]
    public void TransitionTo_InvalidTransition_DoesNotRaiseStateChangedEvent()
    {
        // Arrange
        var eventRaised = false;
        _stateMachine.StateChanged += (sender, state) => eventRaised = true;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _stateMachine.TransitionTo(DictationState.Transcribing));

        Assert.False(eventRaised);
    }

    #endregion

    #region Complete Workflow Tests

    [Fact]
    public void CompleteWorkflow_NormalFlow_AllTransitionsValid()
    {
        // Act & Assert - simulate complete dictation workflow
        // 1. Start recording
        _stateMachine.TransitionTo(DictationState.Recording);
        Assert.Equal(DictationState.Recording, _stateMachine.CurrentState);

        // 2. Stop recording, start transcribing
        _stateMachine.TransitionTo(DictationState.Transcribing);
        Assert.Equal(DictationState.Transcribing, _stateMachine.CurrentState);

        // 3. Transcription done, back to idle
        _stateMachine.TransitionTo(DictationState.Idle);
        Assert.Equal(DictationState.Idle, _stateMachine.CurrentState);
    }

    [Fact]
    public void CompleteWorkflow_EmergencyCancel_RecordingToIdle()
    {
        // Act & Assert - simulate emergency cancel during recording
        // 1. Start recording
        _stateMachine.TransitionTo(DictationState.Recording);
        Assert.Equal(DictationState.Recording, _stateMachine.CurrentState);

        // 2. Emergency cancel (user toggles CapsLock ON again)
        _stateMachine.TransitionTo(DictationState.Idle);
        Assert.Equal(DictationState.Idle, _stateMachine.CurrentState);
    }

    [Fact]
    public void CompleteWorkflow_TranscriptionCancel_TranscribingToIdle()
    {
        // Act & Assert - simulate cancellation during transcription
        // 1. Start recording
        _stateMachine.TransitionTo(DictationState.Recording);

        // 2. Stop recording, start transcribing
        _stateMachine.TransitionTo(DictationState.Transcribing);

        // 3. User cancels transcription (Escape key)
        _stateMachine.TransitionTo(DictationState.Idle);
        Assert.Equal(DictationState.Idle, _stateMachine.CurrentState);
    }

    #endregion
}
