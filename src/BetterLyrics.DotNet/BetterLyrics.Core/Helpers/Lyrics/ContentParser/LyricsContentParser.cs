// 2025/6/23 by Zhe Fang

using BetterLyrics.Core.Enums;
using BetterLyrics.Core.Extensions;
using BetterLyrics.Core.Interfaces.Providers;
using BetterLyrics.Core.Interfaces.Services;
using BetterLyrics.Core.Models.Entities;
using BetterLyrics.Core.Models.Lyrics;
using CommunityToolkit.Mvvm.DependencyInjection;
using Lyricify.Lyrics.Helpers.Optimization;
using Lyricify.Lyrics.Parsers;
using Microsoft.Extensions.Logging;

namespace BetterLyrics.Core.Helpers.Lyrics.ContentParser;

public partial class LyricsContentParser
{
    private static readonly ILogger<LyricsContentParser> _logger =
        Ioc.Default.GetRequiredService<ILogger<LyricsContentParser>>();

    private static readonly ISettingsService _settingsService =
        Ioc.Default.GetRequiredService<ISettingsService>();

    private static readonly IAppUIThreadProvider _appUIThreadProvider =
        Ioc.Default.GetRequiredService<IAppUIThreadProvider>();

    public List<LyricsData> LyricsDataArr { get; private set; } = [];

    private void PreParse(LyricsCacheItem? lyricsSearchResult, LyricsParseRule rule)
    {
        LyricsDataArr = [];
        if (string.IsNullOrWhiteSpace(lyricsSearchResult?.Raw))
        {
            AddLyricsData(LyricsDataExtensions.NotFoundPlaceholder);
        }
        else
        {
            switch (lyricsSearchResult.Raw.DetectFormat())
            {
                case LyricsFormat.Lrc:
                case LyricsFormat.Eslrc:
                    ParseLrc(lyricsSearchResult.Raw, lyricsSearchResult?.Provider, rule);
                    break;
                case LyricsFormat.Qrc:
                    ParseQrcKrc(QrcParser.Parse(lyricsSearchResult.Raw).Lines, lyricsSearchResult?.Provider);
                    break;
                case LyricsFormat.Krc:
                    ParseQrcKrc(KrcParser.Parse(lyricsSearchResult.Raw).Lines, lyricsSearchResult?.Provider);
                    break;
                case LyricsFormat.Ttml:
                    ParseTtml(lyricsSearchResult.Raw, lyricsSearchResult?.Provider, rule);
                    break;
            }

            if (LyricsDataArr.Count == 0) AddLyricsData(LyricsDataExtensions.NotFoundPlaceholder);
        }

        LoadTranslation(lyricsSearchResult, rule);

        EnsureSyllables(lyricsSearchResult?.Duration);
        EnsureEndMs(lyricsSearchResult?.Duration);
    }

    private void AddLyricsData(LyricsData data)
    {
        if (LyricsDataArr.Count == 0)
        {
            if (data != LyricsDataExtensions.NotFoundPlaceholder)
            {
                data.IsProviderSameAsOriginal = true;
            }
        }
        else
        {
            var original = LyricsDataArr[0];
            data.IsProviderSameAsOriginal = data.Provider == original.Provider;
        }
        LyricsDataArr.Add(data);
    }

    public LyricsData Parse(LyricsCacheItem? lyricsSearchResult, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var settings = _settingsService.AppSettings.TranslationSettings;
        var rule = new LyricsParseRule(settings);

        if (lyricsSearchResult != null)
        {
            _appUIThreadProvider.Execute(() =>
            {
                lyricsSearchResult.TransliterationProvider = null;
                lyricsSearchResult.TranslationProvider = null;
            });
        }

        PreParse(lyricsSearchResult, rule);

        var original = LyricsDataArr.First();

        // 歌词过滤
        if (rule.IsFilterEnabled)
            original.LyricsLines.RemoveAll(x =>
                InfoLines.IsInfoLine(x.PrimaryText));

        // 应用翻译
        var targetTranslationLangTag = rule.TargetTranslationTag;
        var isOriginalAlreadyInTargetLanguage = LanguageHelper.IsLanguageMatch(original.LanguageTag, targetTranslationLangTag, true);
        if (rule.IsTranslationEnabled && !isOriginalAlreadyInTargetLanguage)
        {
            var found = LyricsDataArr.Where(x => x.TrackType == LyricsTrackType.Translation &&
                                              LanguageHelper.IsLanguageMatch(x.LanguageTag, targetTranslationLangTag, true))
                .OrderByDescending(x => x.LyricsLines.Count).FirstOrDefault();
            if (found != null)
            {
                original.SetTranslatedText(found);
                _appUIThreadProvider.Execute(() =>
                {
                    lyricsSearchResult?.TranslationProvider = found.Provider;
                });
            }
        }

        // 应用简体中文/繁体中文
        if (rule.ChineseConversion != ChineseConversion.Unspecified)
        {
            var convertFunc = rule.ChineseConversion == ChineseConversion.S2T
                ? (Func<string, string>)LanguageHelper.ConvertSCToTC
                : LanguageHelper.ConvertTCToSC;

            bool isOriginalChinese = LanguageHelper.IsLanguageMatch(original.LanguageTag, LanguageHelper.MandarinChineseCode, true);
            bool isTranslationChinese = LanguageHelper.IsLanguageMatch(rule.TargetTranslationTag, LanguageHelper.MandarinChineseCode, true);

            if (isOriginalChinese || isTranslationChinese)
            {
                foreach (var item in original.LyricsLines)
                {
                    if (isOriginalChinese)
                    {
                        if (item.PrimaryText != null) item.PrimaryText = convertFunc(item.PrimaryText);
                        if (item.PrimarySyllables != null)
                        {
                            foreach (var s in item.PrimarySyllables)
                                if (s.Text != null) s.Text = convertFunc(s.Text);
                        }
                    }
                    if (isTranslationChinese)
                    {
                        if (item.SecondaryText != null) item.SecondaryText = convertFunc(item.SecondaryText);
                    }
                }
            }
        }

        return original;
    }

    private void LoadTranslation(LyricsCacheItem? lyricsSearchResult, LyricsParseRule rule)
    {
        if (rule.IsTranslationEnabled)
        {
            if (!string.IsNullOrWhiteSpace(lyricsSearchResult?.Translation))
            {
                switch (lyricsSearchResult.Provider)
                {
                    case LyricsProvider.QQ:
                    case LyricsProvider.Kugou:
                    case LyricsProvider.Netease:
                        ParseLrc(lyricsSearchResult.Translation, lyricsSearchResult.Provider, rule);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// 基于已经处理好的音节，确保整句话的 EndMs
    /// Invoke this after <see cref="EnsureSyllables" />
    /// </summary>
    private void EnsureEndMs(double? duration)
    {
        foreach (var lyricsData in LyricsDataArr)
        {
            if (lyricsData?.LyricsLines == null) continue;
            var lines = lyricsData.LyricsLines;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null) continue;

                var isLastLine = i + 1 >= lines.Count;
                // 如果是最后一句，使用歌曲总长作为参考
                var nextLineStartMs = isLastLine ? (int)(duration ?? 0) * 1000 : lines[i + 1].StartMs;

                // 确保基础的 EndMs（基于最后的音节）
                if (line.EndMs == null)
                {
                    if (line.PrimarySyllables.Count > 0)
                        line.EndMs = line.PrimarySyllables.Last().EndMs;
                    else
                        line.EndMs = line.StartMs >= nextLineStartMs ? line.StartMs + 1000 : nextLineStartMs;
                }
            }
        }
    }

    /// <summary>
    /// 优先确保音节的完整性（补全缺失的 EndMs，或者为纯文本歌词生成平均音节）
    /// </summary>
    private void EnsureSyllables(double? duration)
    {
        foreach (var lyricsData in LyricsDataArr)
        {
            if (lyricsData?.LyricsLines == null) continue;
            var lines = lyricsData.LyricsLines;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null) continue;

                // 预先获取下一句的 StartMs（如果是最后一句，用歌曲总长或者 0 代替）
                var nextLineStartMs = i + 1 < lines.Count ? lines[i + 1].StartMs : (int)(duration ?? 0) * 1000;

                // 1. 如果已经有音节，按照新逻辑修复音节的 EndMs
                if (line.PrimarySyllables.Count > 0)
                {
                    for (var j = 0; j < line.PrimarySyllables.Count; j++)
                    {
                        var syllable = line.PrimarySyllables[j];
                        if (syllable.EndMs == null)
                        {
                            if (j < line.PrimarySyllables.Count - 1)
                            {
                                // 不是最后一个音节：取后一个音节的 StartMs
                                syllable.EndMs = line.PrimarySyllables[j + 1].StartMs;
                            }
                            else
                            {
                                // 最后一个音节
                                if (syllable.StartMs >= nextLineStartMs)
                                    // 背景歌词特殊情况：起唱时间已超过下一句，直接默认持续1秒
                                    syllable.EndMs = syllable.StartMs + 1000;
                                else
                                    // 默认持续1秒，和下一句的 StartMs 比较，取较小的
                                    syllable.EndMs = Math.Min(syllable.StartMs + 1000, nextLineStartMs);
                            }
                        }
                    }
                }
                // 2. 如果没有音节（如 LRC 格式），则基于预估的整句时间自动生成平均分布的音节
                else if (!line.IsPrimaryHasRealSyllableInfo)
                {
                    var content = line.PrimaryText;
                    var length = content.Length;
                    if (length == 0) continue;

                    // 预估整句话的 EndMs（此时 EnsureEndMs 还没跑，需要临时计算用于平分时间）
                    var tempLineEndMs = line.EndMs ??
                                        (line.StartMs >= nextLineStartMs ? line.StartMs + 1000 : nextLineStartMs);
                    var durationMs = tempLineEndMs - line.StartMs;

                    if (durationMs <= 0) continue;

                    var avgSyllableDuration = durationMs / length;
                    if (avgSyllableDuration == 0) continue;

                    for (var j = 0; j < length; j++)
                        line.PrimarySyllables.Add(new BaseLyrics
                        {
                            Text = content[j].ToString(),
                            StartIndex = j,
                            StartMs = line.StartMs + avgSyllableDuration * j,
                            EndMs = line.StartMs + avgSyllableDuration * (j + 1)
                        });
                }
            }
        }
    }
}