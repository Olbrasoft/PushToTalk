namespace Olbrasoft.PushToTalk.App.Configuration;

/// <summary>
/// Configuration options for the system tray icon.
/// </summary>
public class TrayIconOptions
{
    /// <summary>
    /// Animation configuration for the tray icon.
    /// </summary>
    public AnimationOptions Animation { get; set; } = new();
}

/// <summary>
/// Animation configuration for the tray icon.
/// </summary>
public class AnimationOptions
{
    /// <summary>
    /// List of animation frame filenames (without path).
    /// </summary>
    public string[] Frames { get; set; } = new[]
    {
        "document-white-frame1.svg",
        "document-white-frame2.svg",
        "document-white-frame3.svg",
        "document-white-frame4.svg",
        "document-white-frame5.svg"
    };

    /// <summary>
    /// Animation interval in milliseconds.
    /// </summary>
    public int IntervalMs { get; set; } = 150;
}
