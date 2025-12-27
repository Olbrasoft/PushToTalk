using Microsoft.Extensions.Logging;
using Olbrasoft.PushToTalk.Core.Interfaces;

namespace Olbrasoft.PushToTalk.App.Keyboard;

/// <summary>
/// Synchronizes CapsLock LED state with dictation recording state.
/// </summary>
/// <remarks>
/// Used for web remote control integration. When recording starts via web interface,
/// CapsLock LED is turned ON. When recording stops, LED is turned OFF.
/// This provides visual feedback about recording state.
/// </remarks>
public class CapsLockSynchronizer : ICapsLockSynchronizer
{
    private readonly IKeyboardMonitor _keyboardMonitor;
    private readonly IKeySimulator _keySimulator;
    private readonly ILogger<CapsLockSynchronizer> _logger;
    private readonly KeyCode _capsLockKey;
    private bool _isSynchronizing;

    /// <inheritdoc/>
    public bool IsSynchronizing => _isSynchronizing;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapsLockSynchronizer"/> class.
    /// </summary>
    /// <param name="keyboardMonitor">Keyboard monitor for reading CapsLock state.</param>
    /// <param name="keySimulator">Key simulator for toggling CapsLock.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="capsLockKey">CapsLock key code (defaults to KeyCode.CapsLock).</param>
    public CapsLockSynchronizer(
        IKeyboardMonitor keyboardMonitor,
        IKeySimulator keySimulator,
        ILogger<CapsLockSynchronizer> logger,
        KeyCode capsLockKey = KeyCode.CapsLock)
    {
        _keyboardMonitor = keyboardMonitor ?? throw new ArgumentNullException(nameof(keyboardMonitor));
        _keySimulator = keySimulator ?? throw new ArgumentNullException(nameof(keySimulator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _capsLockKey = capsLockKey;
    }

    /// <inheritdoc/>
    public async Task SynchronizeLedAsync(bool shouldBeOn, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentState = _keyboardMonitor.IsCapsLockOn();

            if (currentState == shouldBeOn)
            {
                _logger.LogDebug("CapsLock LED already in desired state: {State}", shouldBeOn ? "ON" : "OFF");
                return;
            }

            _logger.LogInformation("Synchronizing CapsLock LED: current={Current}, desired={Desired}",
                currentState ? "ON" : "OFF", shouldBeOn ? "ON" : "OFF");

            _isSynchronizing = true;
            try
            {
                // Toggle CapsLock by simulating key press
                await _keySimulator.SimulateKeyPressAsync(_capsLockKey);

                // Wait for LED state to update
                await Task.Delay(100, cancellationToken);

                _logger.LogDebug("CapsLock LED synchronized successfully");
            }
            finally
            {
                _isSynchronizing = false;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("CapsLock synchronization cancelled");
            _isSynchronizing = false;
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to synchronize CapsLock LED");
            _isSynchronizing = false;
            throw;
        }
    }
}
