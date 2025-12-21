namespace Olbrasoft.PushToTalk.App.Configuration;

/// <summary>
/// Validates configuration options on application startup.
/// </summary>
public static class ConfigurationValidator
{
    /// <summary>
    /// Validates web server configuration.
    /// </summary>
    public static void ValidateWebServerOptions(WebServerOptions options)
    {
        if (options.Port <= 0 || options.Port > 65535)
        {
            throw new InvalidOperationException($"Invalid HTTP port: {options.Port}. Must be between 1 and 65535.");
        }

        if (options.Https != null)
        {
            if (options.Https.Port <= 0 || options.Https.Port > 65535)
            {
                throw new InvalidOperationException($"Invalid HTTPS port: {options.Https.Port}. Must be between 1 and 65535.");
            }

            if (string.IsNullOrWhiteSpace(options.Https.CertificatePath))
            {
                throw new InvalidOperationException("HTTPS certificate path cannot be empty.");
            }
        }
    }

    /// <summary>
    /// Validates tray icon configuration.
    /// </summary>
    public static void ValidateTrayIconOptions(TrayIconOptions options)
    {
        if (options.Animation.Frames == null || options.Animation.Frames.Length == 0)
        {
            throw new InvalidOperationException("Animation frames cannot be empty.");
        }

        if (options.Animation.IntervalMs <= 0)
        {
            throw new InvalidOperationException($"Invalid animation interval: {options.Animation.IntervalMs}ms. Must be greater than 0.");
        }
    }
}
