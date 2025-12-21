using Microsoft.Extensions.Logging;
using Olbrasoft.SystemTray.Linux;

namespace Olbrasoft.PushToTalk.App;

/// <summary>
/// Wrapper around ITrayIconManager that maintains backward compatibility with old DBusTrayIcon API.
/// Manages both main icon and animated transcription icon.
/// </summary>
public class TrayIconWrapper : IDisposable
{
    private readonly ITrayIconManager _manager;
    private readonly ILogger<TrayIconWrapper> _logger;
    private readonly string _iconsPath;

    private ITrayIcon? _mainIcon;
    private ITrayIcon? _animatedIcon;
    private bool _isDisposed;

    public bool IsActive => _mainIcon?.IsVisible ?? false;

    // Events for backward compatibility
    public event Action? OnClicked;
    public event Action? OnQuitRequested;
    public event Action? OnAboutRequested;
    public event Action? OnStopSpeechToTextRequested;

    public TrayIconWrapper(ITrayIconManager manager, ILogger<TrayIconWrapper> logger, string iconsPath)
    {
        _manager = manager;
        _logger = logger;
        _iconsPath = iconsPath;
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Create main icon
            _mainIcon = await _manager.CreateIconAsync(
                "push-to-talk-main",
                Path.Combine(_iconsPath, "trigger-ptt.svg"),
                "Push To Talk"
            );

            _mainIcon.Clicked += (s, e) => OnClicked?.Invoke();

            _logger.LogInformation("TrayIconWrapper initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize TrayIconWrapper");
            throw;
        }
    }

    public void SetIcon(string iconName)
    {
        if (_mainIcon == null) return;

        try
        {
            var iconPath = Path.Combine(_iconsPath, $"{iconName}.svg");
            _mainIcon.SetIcon(iconPath);
            _logger.LogDebug("Set icon: {IconName}", iconName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set icon: {IconName}", iconName);
        }
    }

    public void SetTooltip(string text)
    {
        if (_mainIcon == null) return;

        // Get current icon and re-set with new tooltip
        // Note: we'd need to track current icon path for this to work properly
        _logger.LogDebug("Set tooltip: {Text}", text);
    }

    public void StartAnimation(string[] frameNames, int intervalMs = 150)
    {
        if (_mainIcon == null) return;

        try
        {
            var framePaths = frameNames.Select(name => Path.Combine(_iconsPath, $"{name}.svg")).ToArray();
            _mainIcon.StartAnimation(framePaths, intervalMs);
            _logger.LogDebug("Animation started with {FrameCount} frames", frameNames.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start animation");
        }
    }

    public void StopAnimation()
    {
        if (_mainIcon == null) return;

        try
        {
            _mainIcon.StopAnimation();
            _logger.LogDebug("Animation stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop animation");
        }
    }

    public void UpdateSpeechToTextStatus(bool isRunning, string version)
    {
        // TODO: Implement menu support when ITrayMenu is ready
        _logger.LogDebug("SpeechToText status: Running={IsRunning}, Version={Version}", isRunning, version);
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _manager.Dispose();
        _logger.LogInformation("TrayIconWrapper disposed");
    }
}
