using BetterLyrics.Core.Enums;
using BetterLyrics.Core.Helpers.Lyrics.Ttml;
using BetterLyrics.Core.Models.Lyrics;
using NLanguageTag;
using System.Xml;

namespace BetterLyrics.Core.Helpers.Lyrics.ContentParser;

public partial class LyricsContentParser
{
    private void ParseTtml(string raw, LyricsProvider? provider, LyricsParseRule rule)
    {
        try
        {
            var document = TtmlParser.Parse(raw);
            if (document.Metadata.TimingMode == "None") return;

            List<LyricsLine> originalLines = [];
            Dictionary<string, List<LyricsLine>> translations = [];
            foreach (var line in document.Lines)
            {
                AddVocal(line, line.AgentId);
                if (line.BackgroundVocal is { } bg) AddVocal(bg, line.AgentId);
            }
            var original = new LyricsData(originalLines)
            {
                Provider = provider,
                LanguageTag = ParseLanguage(document.Metadata.Language)
            };
            AddLyricsData(original);
            foreach (var (language, lines) in translations)
            {
                var tag = ParseLanguage(language);
                if (rule.IsTranslationAllowed(tag))
                    AddLyricsData(new LyricsData(lines)
                    {
                        Provider = provider,
                        TrackType = LyricsTrackType.Translation,
                        LanguageTag = tag
                    });
            }

            void AddVocal(TtmlVocal vocal, string? agent)
            {
                if (!string.IsNullOrWhiteSpace(vocal.Text))
                    originalLines.Add(TtmlLyricsAdapter.ToLine(vocal, vocal.StartTime, vocal.EndTime, agent));
                if (!rule.IsTranslationEnabled) return;

                foreach (var content in vocal.Translations ?? [])
                {
                    if (string.IsNullOrWhiteSpace(content.Text)) continue;
                    var language = content.Language ?? "";
                    if (!translations.TryGetValue(language, out var lines)) translations[language] = lines = [];
                    lines.Add(TtmlLyricsAdapter.ToLine(content, vocal.StartTime, vocal.EndTime, agent));
                }
            }
        }
        catch (Exception ex) when (ex is XmlException or FormatException or OverflowException)
        {
            System.Diagnostics.Debug.WriteLine($"TTML parsing failed: {ex.Message}");
        }
    }

    private static LanguageTag? ParseLanguage(string? language) =>
        string.IsNullOrWhiteSpace(language) ? null :
        LanguageTag.TryParse(language, out var tag) ? tag : null;
}
