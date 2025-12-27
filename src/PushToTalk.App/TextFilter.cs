using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PushToTalk.Data;
using PushToTalk.Data.Entities;

namespace Olbrasoft.PushToTalk.App;

/// <summary>
/// Configuration for text filters loaded from JSON file.
/// </summary>
public class TextFiltersConfig
{
    /// <summary>
    /// List of text patterns to remove from transcription output.
    /// </summary>
    public List<string> Remove { get; set; } = new();

    /// <summary>
    /// Dictionary of text replacements (incorrect -> correct).
    /// File-based fallback if database is not available.
    /// </summary>
    public Dictionary<string, string> Replace { get; set; } = new();

    /// <summary>
    /// Whether to enable database-driven corrections.
    /// Default is true.
    /// </summary>
    public bool EnableDatabaseCorrections { get; set; } = true;
}

/// <summary>
/// Filters unwanted text patterns from Whisper transcription output.
/// Supports both database-driven corrections and file-based filters.
/// </summary>
public class TextFilter : ITextFilter
{
    private readonly ILogger<TextFilter> _logger;
    private readonly string? _configPath;
    private readonly IServiceScopeFactory? _serviceScopeFactory;

    // File-based filters
    private List<string> _removePatterns = new();
    private Dictionary<string, string> _fileReplacements = new();
    private DateTime _lastModified;

    // Database corrections cache
    private IReadOnlyList<TranscriptionCorrection> _cachedCorrections = Array.Empty<TranscriptionCorrection>();
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly object _lock = new();

    public TextFilter(
        ILogger<TextFilter> logger,
        IServiceScopeFactory? serviceScopeFactory = null,
        string? configPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory;
        _configPath = configPath;

        if (!string.IsNullOrWhiteSpace(_configPath))
        {
            LoadFilters();
        }
        else
        {
            _logger.LogInformation("File-based text filtering disabled (no config path)");
        }

        if (_serviceScopeFactory != null)
        {
            _logger.LogInformation("Database-driven corrections enabled");
        }
    }

    /// <summary>
    /// Gets whether filtering is enabled (file-based or database).
    /// </summary>
    public bool IsEnabled => _removePatterns.Count > 0
        || _fileReplacements.Count > 0
        || _serviceScopeFactory != null;

    /// <summary>
    /// Gets the number of loaded remove patterns (file-based only).
    /// </summary>
    public int PatternCount => _removePatterns.Count;

    /// <summary>
    /// Gets the number of cached database corrections.
    /// </summary>
    public int CachedCorrectionsCount => _cachedCorrections.Count;

    /// <summary>
    /// Applies all filters to the input text.
    /// Order: Database corrections → File replacements → Remove patterns → Normalize
    /// </summary>
    /// <param name="text">Text to filter.</param>
    /// <returns>Filtered text with corrections and patterns applied.</returns>
    public string Apply(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Check for file changes (hot reload)
        CheckForUpdates();

        // Refresh database corrections cache if needed
        RefreshCorrectionsCache();

        var result = text;
        var originalLength = result.Length;

        // Step 1: Apply database corrections (highest priority)
        result = ApplyDatabaseCorrections(result);

        // Step 2: Apply file-based replacements
        result = ApplyFileReplacements(result);

        // Step 3: Apply remove patterns
        result = ApplyRemovePatterns(result);

        // Step 4: Normalize whitespace
        result = NormalizeWhitespace(result);

        if (result.Length != originalLength)
        {
            _logger.LogDebug("Text filtered: {Original} chars -> {Result} chars", originalLength, result.Length);
        }

        return result;
    }

    /// <summary>
    /// Applies database-driven corrections with priority ordering.
    /// </summary>
    private string ApplyDatabaseCorrections(string text)
    {
        if (_cachedCorrections.Count == 0)
            return text;

        var result = text;

        // Apply corrections in priority order (highest first)
        foreach (var correction in _cachedCorrections)
        {
            var comparison = correction.CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            if (result.Contains(correction.IncorrectText, comparison))
            {
                result = result.Replace(
                    correction.IncorrectText,
                    correction.CorrectText,
                    comparison);

                _logger.LogDebug(
                    "Applied DB correction: '{Incorrect}' → '{Correct}' (priority: {Priority})",
                    correction.IncorrectText,
                    correction.CorrectText,
                    correction.Priority);

                // Track usage asynchronously (don't block)
                _ = TrackCorrectionUsageAsync(correction.Id);
            }
        }

        return result;
    }

    /// <summary>
    /// Applies file-based text replacements.
    /// </summary>
    private string ApplyFileReplacements(string text)
    {
        if (_fileReplacements.Count == 0)
            return text;

        var result = text;

        lock (_lock)
        {
            foreach (var (incorrect, correct) in _fileReplacements)
            {
                if (result.Contains(incorrect, StringComparison.OrdinalIgnoreCase))
                {
                    result = result.Replace(incorrect, correct, StringComparison.OrdinalIgnoreCase);
                    _logger.LogDebug("Applied file replacement: '{Incorrect}' → '{Correct}'", incorrect, correct);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Applies remove patterns from file configuration.
    /// </summary>
    private string ApplyRemovePatterns(string text)
    {
        if (_removePatterns.Count == 0)
            return text;

        var result = text;

        lock (_lock)
        {
            foreach (var pattern in _removePatterns)
            {
                if (result.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    result = result.Replace(pattern, "", StringComparison.OrdinalIgnoreCase);
                    _logger.LogDebug("Removed pattern: '{Pattern}'", pattern);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Normalizes whitespace (trim + collapse multiple spaces).
    /// </summary>
    private static string NormalizeWhitespace(string text)
    {
        var result = text.Trim();

        // Collapse multiple spaces to single space
        while (result.Contains("  "))
        {
            result = result.Replace("  ", " ");
        }

        return result;
    }

    /// <summary>
    /// Refreshes the database corrections cache if expired.
    /// </summary>
    private void RefreshCorrectionsCache()
    {
        if (_serviceScopeFactory == null)
            return;

        if (DateTime.UtcNow < _cacheExpiry)
            return;

        lock (_lock)
        {
            // Double-check after acquiring lock
            if (DateTime.UtcNow < _cacheExpiry)
                return;

            try
            {
                // Create a new scope to get scoped repository
                using var scope = _serviceScopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetService<ITranscriptionCorrectionRepository>();

                if (repository == null)
                {
                    _logger.LogWarning("ITranscriptionCorrectionRepository not available");
                    _cacheExpiry = DateTime.UtcNow.AddSeconds(30);
                    return;
                }

                // Synchronous call is acceptable for cache refresh
                _cachedCorrections = repository
                    .GetActiveCorrectionsAsync()
                    .GetAwaiter()
                    .GetResult();

                _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);

                _logger.LogInformation(
                    "Refreshed corrections cache: {Count} active corrections (expires: {Expiry})",
                    _cachedCorrections.Count,
                    _cacheExpiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh corrections cache");

                // Retry in 30 seconds on error
                _cacheExpiry = DateTime.UtcNow.AddSeconds(30);
            }
        }
    }

    /// <summary>
    /// Tracks correction usage asynchronously for analytics.
    /// </summary>
    private async Task TrackCorrectionUsageAsync(int correctionId)
    {
        if (_serviceScopeFactory == null)
            return;

        try
        {
            // Create a new scope to get scoped repository
            using var scope = _serviceScopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetService<ITranscriptionCorrectionRepository>();

            if (repository != null)
            {
                await repository.TrackUsageAsync(correctionId);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the correction if tracking fails
            _logger.LogWarning(ex, "Failed to track correction usage for ID {CorrectionId}", correctionId);
        }
    }

    /// <summary>
    /// Reloads filters from the configuration file.
    /// </summary>
    public void Reload()
    {
        LoadFilters();
    }

    private void LoadFilters()
    {
        if (string.IsNullOrWhiteSpace(_configPath))
            return;

        try
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogWarning("Text filters config not found: {Path}", _configPath);
                return;
            }

            var fileInfo = new FileInfo(_configPath);

            lock (_lock)
            {
                _lastModified = fileInfo.LastWriteTimeUtc;

                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<TextFiltersConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config != null)
                {
                    // Load remove patterns
                    _removePatterns = config.Remove
                        ?.Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList() ?? new();

                    // Load replace mappings
                    _fileReplacements = config.Replace ?? new();

                    _logger.LogInformation(
                        "Loaded text filters from {Path}: {RemoveCount} remove patterns, {ReplaceCount} replacements",
                        _configPath,
                        _removePatterns.Count,
                        _fileReplacements.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load text filters from {Path}", _configPath);
        }
    }

    private void CheckForUpdates()
    {
        if (string.IsNullOrWhiteSpace(_configPath) || !File.Exists(_configPath))
            return;

        try
        {
            var fileInfo = new FileInfo(_configPath);
            if (fileInfo.LastWriteTimeUtc > _lastModified)
            {
                _logger.LogInformation("Text filters config changed, reloading...");
                LoadFilters();
            }
        }
        catch
        {
            // Ignore file access errors during check
        }
    }
}
