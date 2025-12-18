using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// File-based speech lock service implementation.
/// Creates/deletes a lock file to signal TTS services to pause.
/// </summary>
public class SpeechLockService : ISpeechLockService
{
    private readonly ILogger<SpeechLockService> _logger;
    private readonly string _lockFilePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpeechLockService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="configuration">Configuration for lock file path.</param>
    public SpeechLockService(
        ILogger<SpeechLockService> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lockFilePath = configuration?.GetValue<string>("SystemPaths:SpeechLockFile")
            ?? "/tmp/speech-lock";
    }

    /// <inheritdoc/>
    public bool IsLocked => File.Exists(_lockFilePath);

    /// <inheritdoc/>
    public void CreateLock(string reason)
    {
        try
        {
            File.WriteAllText(_lockFilePath, reason);

            // Verify the file was actually created
            if (File.Exists(_lockFilePath))
            {
                _logger.LogInformation("Speech lock file CREATED: {Path} (reason: {Reason})", _lockFilePath, reason);
            }
            else
            {
                _logger.LogError("Speech lock file NOT created despite no exception: {Path}", _lockFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create speech lock file: {Path}", _lockFilePath);
        }
    }

    /// <inheritdoc/>
    public void ReleaseLock()
    {
        try
        {
            if (File.Exists(_lockFilePath))
            {
                File.Delete(_lockFilePath);
                _logger.LogInformation("Speech lock file DELETED: {Path}", _lockFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete speech lock file: {Path}", _lockFilePath);
        }
    }
}
