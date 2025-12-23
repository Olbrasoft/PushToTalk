using Microsoft.Extensions.Logging;
using PushToTalk.Data;

namespace PushToTalk.Data.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core implementation of ITranscriptionRepository.
/// </summary>
public class TranscriptionRepository : ITranscriptionRepository
{
    private readonly PushToTalkDbContext _dbContext;
    private readonly ILogger<TranscriptionRepository> _logger;

    public TranscriptionRepository(PushToTalkDbContext dbContext, ILogger<TranscriptionRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<WhisperTranscription> SaveAsync(
        string text,
        int? durationMs = null,
        CancellationToken ct = default)
    {
        var transcription = new WhisperTranscription
        {
            TranscribedText = text,
            AudioDurationMs = durationMs,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.WhisperTranscriptions.Add(transcription);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug(
            "Saved transcription {Id}: '{Text}' (duration: {DurationMs}ms)",
            transcription.Id,
            text.Length > 50 ? text[..50] + "..." : text,
            durationMs);

        return transcription;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WhisperTranscription>> GetRecentAsync(int count = 50, CancellationToken ct = default)
    {
        return await _dbContext.WhisperTranscriptions
            .OrderByDescending(t => t.CreatedAt)
            .Take(count)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WhisperTranscription>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetRecentAsync(50, ct);
        }

        return await _dbContext.WhisperTranscriptions
            .Where(t => EF.Functions.ILike(t.TranscribedText, $"%{query}%"))
            .OrderByDescending(t => t.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
    }
}
