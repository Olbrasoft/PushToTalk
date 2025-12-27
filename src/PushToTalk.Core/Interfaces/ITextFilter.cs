namespace Olbrasoft.PushToTalk.Core.Interfaces;

/// <summary>
/// Interface for applying text filters to transcriptions.
/// Filters include database corrections, file-based replacements, and pattern removal.
/// </summary>
public interface ITextFilter
{
    /// <summary>
    /// Applies all filters to the input text.
    /// Order: Database corrections → File replacements → Remove patterns → Normalize
    /// </summary>
    /// <param name="text">Text to filter.</param>
    /// <returns>Filtered text with corrections and patterns applied.</returns>
    string Apply(string? text);

    /// <summary>
    /// Gets whether filtering is enabled (file-based or database).
    /// </summary>
    bool IsEnabled { get; }
}
