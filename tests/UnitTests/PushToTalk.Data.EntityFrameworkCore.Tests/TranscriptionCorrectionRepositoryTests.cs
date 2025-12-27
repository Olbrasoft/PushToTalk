using PushToTalk.Data.Entities;
using PushToTalk.Data.EntityFrameworkCore.Tests.Infrastructure;

namespace PushToTalk.Data.EntityFrameworkCore.Tests;

/// <summary>
/// Unit tests for TranscriptionCorrectionRepository using in-memory database.
/// </summary>
public class TranscriptionCorrectionRepositoryTests : DatabaseTestBase
{
    private readonly TranscriptionCorrectionRepository _repository;

    public TranscriptionCorrectionRepositoryTests()
    {
        _repository = new TranscriptionCorrectionRepository(DbContext);
    }

    #region GetActiveCorrectionsAsync Tests

    [Fact]
    public async Task GetActiveCorrectionsAsync_WithNoCorrections_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetActiveCorrectionsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetActiveCorrectionsAsync_OnlyReturnsActiveCorrections()
    {
        // Arrange
        await AddCorrectionAsync("active1", "corrected1", isActive: true);
        await AddCorrectionAsync("active2", "corrected2", isActive: true);
        await AddCorrectionAsync("inactive", "corrected3", isActive: false);

        // Act
        var result = await _repository.GetActiveCorrectionsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.True(c.IsActive));
    }

    [Fact]
    public async Task GetActiveCorrectionsAsync_OrdersByPriorityDescending()
    {
        // Arrange
        var low = await AddCorrectionAsync("low", "c1", priority: 10);
        var high = await AddCorrectionAsync("high", "c2", priority: 100);
        var medium = await AddCorrectionAsync("medium", "c3", priority: 50);

        // Act
        var result = await _repository.GetActiveCorrectionsAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(high.Id, result[0].Id); // Highest priority first
        Assert.Equal(medium.Id, result[1].Id);
        Assert.Equal(low.Id, result[2].Id);
    }

    [Fact]
    public async Task GetActiveCorrectionsAsync_WithSamePriority_OrdersByIdAscending()
    {
        // Arrange
        var first = await AddCorrectionAsync("first", "c1", priority: 50);
        var second = await AddCorrectionAsync("second", "c2", priority: 50);
        var third = await AddCorrectionAsync("third", "c3", priority: 50);

        // Act
        var result = await _repository.GetActiveCorrectionsAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(first.Id, result[0].Id);
        Assert.Equal(second.Id, result[1].Id);
        Assert.Equal(third.Id, result[2].Id);
    }

    [Fact]
    public async Task GetActiveCorrectionsAsync_ReturnsReadOnlyNoTracking()
    {
        // Arrange
        var correction = await AddCorrectionAsync("test", "corrected");

        // Act
        var result = await _repository.GetActiveCorrectionsAsync();
        result[0].CorrectText = "Modified";

        // Assert - Changes should not be tracked
        var fromDb = await DbContext.TranscriptionCorrections.FindAsync(correction.Id);
        Assert.Equal("corrected", fromDb!.CorrectText); // Original value unchanged
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsCorrection()
    {
        // Arrange
        var correction = await AddCorrectionAsync("incorrect", "correct");

        // Act
        var result = await _repository.GetByIdAsync(correction.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(correction.Id, result.Id);
        Assert.Equal("incorrect", result.IncorrectText);
        Assert.Equal("correct", result.CorrectText);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTrackedEntity()
    {
        // Arrange
        var correction = await AddCorrectionAsync("test", "corrected");

        // Act
        var result = await _repository.GetByIdAsync(correction.Id);
        result!.CorrectText = "Modified";
        await DbContext.SaveChangesAsync();

        // Assert - Changes should be tracked and saved
        var fromDb = await DbContext.TranscriptionCorrections.FindAsync(correction.Id);
        Assert.Equal("Modified", fromDb!.CorrectText);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_CreatesNewCorrection()
    {
        // Arrange
        var correction = new TranscriptionCorrection
        {
            IncorrectText = "vyspru",
            CorrectText = "Whisper",
            Priority = 100,
            IsActive = true
        };

        // Act
        await _repository.AddAsync(correction);

        // Assert
        Assert.NotEqual(0, correction.Id);
        var fromDb = await DbContext.TranscriptionCorrections.FindAsync(correction.Id);
        Assert.NotNull(fromDb);
        Assert.Equal("vyspru", fromDb.IncorrectText);
        Assert.Equal("Whisper", fromDb.CorrectText);
    }

    [Fact]
    public async Task AddAsync_SetsCreatedAtTimestamp()
    {
        // Arrange
        var correction = new TranscriptionCorrection
        {
            IncorrectText = "test",
            CorrectText = "corrected"
        };

        // Act
        var before = DateTimeOffset.UtcNow;
        await _repository.AddAsync(correction);
        var after = DateTimeOffset.UtcNow;

        // Assert
        Assert.True(correction.CreatedAt >= before);
        Assert.True(correction.CreatedAt <= after);
    }

    [Fact]
    public async Task AddAsync_SetsUpdatedAtTimestamp()
    {
        // Arrange
        var correction = new TranscriptionCorrection
        {
            IncorrectText = "test",
            CorrectText = "corrected"
        };

        // Act
        var before = DateTimeOffset.UtcNow;
        await _repository.AddAsync(correction);
        var after = DateTimeOffset.UtcNow;

        // Assert
        Assert.True(correction.UpdatedAt >= before);
        Assert.True(correction.UpdatedAt <= after);
    }

    [Fact]
    public async Task AddAsync_WithAllProperties_PreservesValues()
    {
        // Arrange
        var correction = new TranscriptionCorrection
        {
            IncorrectText = "teďkon",
            CorrectText = "teď",
            CaseSensitive = true,
            Priority = 200,
            IsActive = true,
            Notes = "Common concatenation error"
        };

        // Act
        await _repository.AddAsync(correction);

        // Assert
        var fromDb = await DbContext.TranscriptionCorrections.FindAsync(correction.Id);
        Assert.NotNull(fromDb);
        Assert.Equal("teďkon", fromDb.IncorrectText);
        Assert.Equal("teď", fromDb.CorrectText);
        Assert.True(fromDb.CaseSensitive);
        Assert.Equal(200, fromDb.Priority);
        Assert.True(fromDb.IsActive);
        Assert.Equal("Common concatenation error", fromDb.Notes);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesExistingCorrection()
    {
        // Arrange
        var correction = await AddCorrectionAsync("old", "old_correct");

        // Modify
        correction.IncorrectText = "new";
        correction.CorrectText = "new_correct";
        correction.Priority = 999;

        // Act
        await _repository.UpdateAsync(correction);

        // Assert
        var fromDb = await DbContext.TranscriptionCorrections.FindAsync(correction.Id);
        Assert.Equal("new", fromDb!.IncorrectText);
        Assert.Equal("new_correct", fromDb.CorrectText);
        Assert.Equal(999, fromDb.Priority);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesUpdatedAtTimestamp()
    {
        // Arrange
        var correction = await AddCorrectionAsync("test", "corrected");
        var originalUpdatedAt = correction.UpdatedAt;

        await Task.Delay(10); // Ensure time difference
        correction.CorrectText = "modified";

        // Act
        await _repository.UpdateAsync(correction);

        // Assert
        Assert.True(correction.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotChangeCreatedAtTimestamp()
    {
        // Arrange
        var correction = await AddCorrectionAsync("test", "corrected");
        var originalCreatedAt = correction.CreatedAt;

        await Task.Delay(10);
        correction.CorrectText = "modified";

        // Act
        await _repository.UpdateAsync(correction);

        // Assert
        Assert.Equal(originalCreatedAt, correction.CreatedAt);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WithExistingId_RemovesCorrection()
    {
        // Arrange
        var correction = await AddCorrectionAsync("test", "corrected");

        // Act
        await _repository.DeleteAsync(correction.Id);

        // Assert
        var fromDb = await DbContext.TranscriptionCorrections.FindAsync(correction.Id);
        Assert.Null(fromDb);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        await _repository.DeleteAsync(999);
    }

    [Fact]
    public async Task DeleteAsync_OnlyRemovesSpecifiedCorrection()
    {
        // Arrange
        var first = await AddCorrectionAsync("first", "c1");
        var second = await AddCorrectionAsync("second", "c2");
        var third = await AddCorrectionAsync("third", "c3");

        // Act
        await _repository.DeleteAsync(second.Id);

        // Assert
        Assert.NotNull(await DbContext.TranscriptionCorrections.FindAsync(first.Id));
        Assert.Null(await DbContext.TranscriptionCorrections.FindAsync(second.Id));
        Assert.NotNull(await DbContext.TranscriptionCorrections.FindAsync(third.Id));
    }

    #endregion

    #region TrackUsageAsync Tests

    // NOTE: TrackUsageAsync tests require real PostgreSQL database (in integration tests)
    // In-memory database doesn't support raw SQL ExecuteSqlInterpolated for inserts

    #endregion

    #region Helper Methods

    private async Task<TranscriptionCorrection> AddCorrectionAsync(
        string incorrectText,
        string correctText,
        bool caseSensitive = false,
        int priority = 50,
        bool isActive = true,
        string? notes = null)
    {
        var correction = new TranscriptionCorrection
        {
            IncorrectText = incorrectText,
            CorrectText = correctText,
            CaseSensitive = caseSensitive,
            Priority = priority,
            IsActive = isActive,
            Notes = notes,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.TranscriptionCorrections.Add(correction);
        await DbContext.SaveChangesAsync();
        return correction;
    }

    #endregion
}
