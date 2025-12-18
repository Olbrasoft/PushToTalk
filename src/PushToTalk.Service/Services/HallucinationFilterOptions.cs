namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Configuration options for the hallucination filter.
/// </summary>
public class HallucinationFilterOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "HallucinationFilter";

    /// <summary>
    /// Minimum text length after cleaning to be considered valid.
    /// Texts shorter than this are likely noise.
    /// </summary>
    public int MinimumTextLength { get; set; } = 2;

    /// <summary>
    /// Known hallucination patterns to filter out.
    /// These patterns are removed from transcription text (case-insensitive).
    /// </summary>
    public HashSet<string> Patterns { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        // Czech Whisper hallucinations
        "titulky vytvořil johnyx",
        "titulky vytvořil johny x",
        "titulky vytvořil johnny x",
        "titulky vytvořil johnyx.",
        "titulky vytvořil johny",
        "titulky vytvořil johnny",
        "titulky vytvoril johnyx",
        "titulky vytvoril johny x",
        "titulky vytvoril johnny x",
        "titulky vytvoril johny",
        "titulky vytvoril johnny",
        "titulky:",
        "titulky :",
        // English Whisper hallucinations
        "subtitles by johnyx",
        "subtitles created by johnyx",
        "subtitles by johny x",
        "subtitles by johnny x",
        "thank you for watching",
        "thanks for watching",
        "thanks for watching!",
        "thank you for watching!",
        "please subscribe",
        "please like and subscribe",
        "like and subscribe"
    };
}
