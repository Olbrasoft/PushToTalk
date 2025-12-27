namespace Olbrasoft.PushToTalk.App.Keyboard;

/// <summary>
/// Interface for synchronizing CapsLock LED state with recording state.
/// </summary>
/// <remarks>
/// Used for web remote control where CapsLock LED must reflect recording state.
/// When recording starts, LED should be ON. When recording stops, LED should be OFF.
/// </remarks>
public interface ICapsLockSynchronizer
{
    /// <summary>
    /// Gets whether CapsLock synchronization is currently in progress.
    /// </summary>
    /// <remarks>
    /// During synchronization, CapsLock key events should be ignored to prevent
    /// triggering dictation state changes.
    /// </remarks>
    bool IsSynchronizing { get; }

    /// <summary>
    /// Synchronizes CapsLock LED state with desired recording state.
    /// </summary>
    /// <param name="shouldBeOn">True if LED should be ON (recording), false if OFF (idle).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    /// <remarks>
    /// If current LED state matches desired state, does nothing.
    /// Otherwise, simulates CapsLock key press to toggle LED.
    /// Sets IsSynchronizing flag during operation to prevent event loops.
    /// </remarks>
    Task SynchronizeLedAsync(bool shouldBeOn, CancellationToken cancellationToken = default);
}
