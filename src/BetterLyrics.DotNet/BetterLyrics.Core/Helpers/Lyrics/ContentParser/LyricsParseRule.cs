using BetterLyrics.Core.Enums;
using BetterLyrics.Core.Models.Settings;
using NLanguageTag;

namespace BetterLyrics.Core.Helpers.Lyrics.ContentParser;

public class LyricsParseRule
{
    public bool IsTranslationEnabled { get; }
    public LanguageTag? TargetTranslationTag { get; }

    public ChineseConversion ChineseConversion { get; }
    public bool IsFilterEnabled { get; }

    public LyricsParseRule(TranslationSettings settings)
    {
        IsTranslationEnabled = settings.IsTranslationEnabled;
        if (IsTranslationEnabled && !string.IsNullOrEmpty(settings.SelectedTargetLanguageCode))
        {
            LanguageTag.TryParse(settings.SelectedTargetLanguageCode, out var tag);
            TargetTranslationTag = tag;
        }

        ChineseConversion = settings.ChineseConversion;
        IsFilterEnabled = settings.IsFilterEnabled;
    }

    public bool IsTranslationAllowed(LanguageTag? tag)
    {
        if (!IsTranslationEnabled) return false;
        if (tag == null) return true; // Accept translations without explicit tags as fallback
        return TargetTranslationTag == null || LanguageHelper.IsLanguageMatch(TargetTranslationTag, tag, true);
    }
}
