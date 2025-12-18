using Microsoft.Extensions.Logging;
using SkiaSharp;
using Svg.Skia;

namespace Olbrasoft.PushToTalk.App;

/// <summary>
/// Renders SVG icons to ARGB format for D-Bus StatusNotifierItem.
/// Shared utility used by both DBusTrayIcon and DBusAnimatedIcon.
/// </summary>
public class SvgIconRenderer
{
    private readonly ILogger _logger;
    private readonly string _iconsPath;
    private readonly int _iconSize;
    private readonly Dictionary<string, (int Width, int Height, byte[] ArgbData)> _cache = new();

    /// <summary>
    /// Empty pixmap used as fallback.
    /// </summary>
    public static readonly (int, int, byte[]) EmptyPixmap = (1, 1, new byte[] { 255, 0, 0, 0 });

    public SvgIconRenderer(ILogger logger, string iconsPath, int iconSize = 22)
    {
        _logger = logger;
        _iconsPath = iconsPath;
        _iconSize = iconSize;
    }

    /// <summary>
    /// Gets the icons directory path.
    /// </summary>
    public string IconsPath => _iconsPath;

    /// <summary>
    /// Gets the icon size.
    /// </summary>
    public int IconSize => _iconSize;

    /// <summary>
    /// Gets the number of cached icons.
    /// </summary>
    public int CacheCount => _cache.Count;

    /// <summary>
    /// Gets an icon from cache or renders it from SVG if not cached.
    /// </summary>
    /// <param name="iconName">Icon name without path or extension.</param>
    /// <returns>The icon pixmap, or null if not found.</returns>
    public (int Width, int Height, byte[] ArgbData)? GetIcon(string iconName)
    {
        // Check cache first
        if (_cache.TryGetValue(iconName, out var cached))
        {
            return cached;
        }

        // Load and render SVG
        var iconPath = Path.Combine(_iconsPath, $"{iconName}.svg");
        if (!File.Exists(iconPath))
        {
            _logger.LogWarning("Icon not found: {Path}", iconPath);
            return null;
        }

        var pixmap = RenderSvgToArgb(iconPath);
        if (pixmap.HasValue)
        {
            _cache[iconName] = pixmap.Value;
            _logger.LogDebug("Cached icon: {IconName} ({Width}x{Height})",
                iconName, pixmap.Value.Width, pixmap.Value.Height);
        }

        return pixmap;
    }

    /// <summary>
    /// Pre-caches multiple icons by name.
    /// </summary>
    /// <param name="iconNames">Array of icon names without path or extension.</param>
    public void PreCacheIcons(string[] iconNames)
    {
        foreach (var iconName in iconNames)
        {
            GetIcon(iconName);
        }
    }

    /// <summary>
    /// Checks if an icon is already cached.
    /// </summary>
    public bool IsCached(string iconName) => _cache.ContainsKey(iconName);

    /// <summary>
    /// Clears the icon cache.
    /// </summary>
    public void ClearCache() => _cache.Clear();

    /// <summary>
    /// Renders an SVG file to ARGB pixel data for D-Bus StatusNotifierItem.
    /// </summary>
    /// <param name="svgPath">Full path to the SVG file.</param>
    /// <returns>Tuple of (width, height, ARGB bytes) or null on failure.</returns>
    private (int Width, int Height, byte[] ArgbData)? RenderSvgToArgb(string svgPath)
    {
        try
        {
            using var svg = new SKSvg();
            if (svg.Load(svgPath) is null)
                return null;

            var picture = svg.Picture;
            if (picture is null)
                return null;

            var bounds = picture.CullRect;
            var scale = Math.Min(_iconSize / bounds.Width, _iconSize / bounds.Height);
            var width = (int)(bounds.Width * scale);
            var height = (int)(bounds.Height * scale);

            if (width <= 0 || height <= 0)
                return null;

            using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale);
            canvas.DrawPicture(picture);

            var pixels = bitmap.Bytes;
            var argbData = new byte[width * height * 4];

            // Convert RGBA to ARGB (D-Bus StatusNotifierItem uses ARGB format)
            for (int i = 0; i < width * height; i++)
            {
                var srcIdx = i * 4;
                var dstIdx = i * 4;

                byte r = pixels[srcIdx];
                byte g = pixels[srcIdx + 1];
                byte b = pixels[srcIdx + 2];
                byte a = pixels[srcIdx + 3];

                argbData[dstIdx] = a;
                argbData[dstIdx + 1] = r;
                argbData[dstIdx + 2] = g;
                argbData[dstIdx + 3] = b;
            }

            return (width, height, argbData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render SVG: {Path}", svgPath);
            return null;
        }
    }
}
