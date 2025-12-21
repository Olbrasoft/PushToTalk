using Microsoft.Extensions.Logging;
using Olbrasoft.PushToTalk.App.Configuration;
using Olbrasoft.SystemTray.Linux;

namespace Olbrasoft.PushToTalk.App.Tray;

/// <summary>
/// Wrapper for PushToTalk tray icons using Olbrasoft.SystemTray.Linux.
/// Manages main icon and animated icon using TrayIconManager.
/// </summary>
public class PushToTalkTrayService : IDisposable
{
    private readonly TrayIconManager _manager;
    private readonly ILogger<PushToTalkTrayService> _logger;
    private readonly string _iconsPath;
    private readonly string[] _animationFrames;
    private readonly int _animationIntervalMs;
    private readonly ITrayMenuHandler? _menuHandler;

    private ITrayIcon? _mainIcon;
    private ITrayIcon? _animatedIcon;
    private string? _currentIconPath;
    private string? _currentTooltip;

    // Events for menu integration (forwarded from DBusMenuHandler)
    public event Action? OnClicked;
    public event Action? OnQuitRequested;
    public event Action? OnAboutRequested;
    public event Action? OnStartSpeechToTextRequested;
    public event Action? OnStopSpeechToTextRequested;

    public bool IsActive => _mainIcon != null;

    public PushToTalkTrayService(
        ILogger<PushToTalkTrayService> logger,
        TrayIconManager manager,
        string iconsPath,
        TrayIconOptions trayIconOptions,
        ITrayMenuHandler? menuHandler = null)
    {
        _logger = logger;
        _manager = manager;
        _iconsPath = iconsPath;
        _menuHandler = menuHandler;
        _animationIntervalMs = trayIconOptions.Animation.IntervalMs;

        _animationFrames = trayIconOptions.Animation.Frames
            .Select(frame => Path.Combine(iconsPath, frame))
            .ToArray();

        // Connect menu handler events to our events
        if (_menuHandler is DBusMenuHandler dbusMenuHandler)
        {
            dbusMenuHandler.OnQuitRequested += () => OnQuitRequested?.Invoke();
            dbusMenuHandler.OnAboutRequested += () => OnAboutRequested?.Invoke();
            dbusMenuHandler.OnStartSpeechToTextRequested += () => OnStartSpeechToTextRequested?.Invoke();
            dbusMenuHandler.OnStopSpeechToTextRequested += () => OnStopSpeechToTextRequested?.Invoke();
        }
    }

    public async Task InitializeMainIconAsync()
    {
        try
        {
            var iconPath = Path.Combine(_iconsPath, "push-to-talk.svg");
            _mainIcon = await _manager.CreateIconAsync("push-to-talk-main", iconPath, "Push To Talk", _menuHandler);
            _currentIconPath = iconPath;
            _currentTooltip = "Push To Talk";

            if (_mainIcon != null)
            {
                _mainIcon.Clicked += (sender, e) => OnClicked?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize main tray icon");
        }
    }

    public void SetIcon(string iconName)
    {
        if (_mainIcon == null) return;

        _currentIconPath = Path.Combine(_iconsPath, $"{iconName}.svg");
        _mainIcon.SetIcon(_currentIconPath, _currentTooltip);
    }

    public void SetTooltip(string tooltip)
    {
        if (_mainIcon == null) return;

        _currentTooltip = tooltip;
        _mainIcon.SetIcon(_currentIconPath ?? "", tooltip);
    }

    public async Task ShowAnimatedIconAsync()
    {
        if (_animatedIcon != null) return; // Already showing

        try
        {
            _animatedIcon = await _manager.CreateIconAsync(
                "push-to-talk-animated",
                _animationFrames[0],
                "Transcribing...",
                null
            );

            if (_animatedIcon != null)
            {
                _animatedIcon.StartAnimation(_animationFrames, intervalMs: _animationIntervalMs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create animated tray icon");
        }
    }

    public void HideAnimatedIcon()
    {
        if (_animatedIcon == null) return;

        _animatedIcon.StopAnimation();
        _manager.RemoveIcon("push-to-talk-animated");
        _animatedIcon = null;
    }

    public void UpdateSpeechToTextStatus(bool isRunning, string version)
    {
        _logger.LogInformation("SpeechToText status updated: Running={IsRunning}, Version={Version}", isRunning, version);
        if (_menuHandler is DBusMenuHandler dbusMenuHandler)
        {
            dbusMenuHandler.UpdateSpeechToTextStatus(isRunning, version);
        }
    }

    public void Dispose()
    {
        HideAnimatedIcon();

        if (_mainIcon != null)
        {
            _manager.RemoveIcon("push-to-talk-main");
            _mainIcon = null;
        }
    }
}
