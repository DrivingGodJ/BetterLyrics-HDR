using BetterLyrics.Core.Collections;
using BetterLyrics.Core.Enums;
using BetterLyrics.Core.Messages.LyricsWindow;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Text.Json.Serialization;

namespace BetterLyrics.Core.Models.Settings;

public partial class LyricsStyleSettings : ObservableObject, ICloneable
{
    public const double UnplayedOriginalLyricsOpacity = 0.30;
    public const double TranslatedLyricsOpacity = 0.60;
    public const double PhoneticLyricsOpacity = 0.60;

    [ObservableProperty]
    public partial TextAlignmentType LyricsAlignmentType { get; set; } = TextAlignmentType.Left;

    [ObservableProperty]
    public partial bool UseInternalLyricsAlignment { get; set; } = true;

    [ObservableProperty]
    public partial LyricsLayoutOrientation LyricsLayoutOrientation { get; set; } = LyricsLayoutOrientation.Horizontal;

    [ObservableProperty]
    public partial FullyObservableCollection<LyricsLayerConfig> LyricsLayerOrder { get; set; } = new()
    {
        new LyricsLayerConfig(LyricsLayerType.Tertiary), new LyricsLayerConfig(LyricsLayerType.Primary),
        new LyricsLayerConfig(LyricsLayerType.Secondary)
    };

    [ObservableProperty]
    public partial bool AutoWrap { get; set; } = true;

    [ObservableProperty]
    public partial LyricsFontWeight LyricsFontWeight { get; set; } = LyricsFontWeight.Bold;

    [ObservableProperty]
    [JsonPropertyName("LyricsLineSpacingFactor")]
    public partial double LyricsLineOverallSpacingFactor { get; set; } = 0.5;

    [ObservableProperty]
    public partial double LyricsLineInnerSpacingFactor { get; set; } = 0.1;

    [ObservableProperty]
    public partial string LyricsCJKFontFamily { get; set; } = "Arial";

    [ObservableProperty]
    public partial string LyricsWesternFontFamily { get; set; } = "Arial";

    [ObservableProperty]
    public partial int PlayingLineTopOffset { get; set; } = 50; // 50 %

    partial void OnLyricsCJKFontFamilyChanged(string value) =>
        WeakReferenceMessenger.Default.Send(new LyricsFontFamilyChangedMessage(this));

    partial void OnLyricsWesternFontFamilyChanged(string value) =>
        WeakReferenceMessenger.Default.Send(new LyricsFontFamilyChangedMessage(this));

    partial void OnAutoWrapChanged(bool value) =>
        WeakReferenceMessenger.Default.Send(new LyricsStyleRelayoutChangedMessage(this));

    partial void OnUseInternalLyricsAlignmentChanged(bool value) =>
        WeakReferenceMessenger.Default.Send(new LyricsStyleRelayoutChangedMessage(this));

    partial void OnLyricsLineOverallSpacingFactorChanged(double value) =>
        WeakReferenceMessenger.Default.Send(new LyricsStyleRelayoutChangedMessage(this));

    partial void OnLyricsLineInnerSpacingFactorChanged(double value) =>
        WeakReferenceMessenger.Default.Send(new LyricsStyleRelayoutChangedMessage(this));

    partial void OnPlayingLineTopOffsetChanged(int value) =>
        WeakReferenceMessenger.Default.Send(new LyricsStyleRelayoutChangedMessage(this));

    partial void OnLyricsFontWeightChanged(LyricsFontWeight value) =>
        WeakReferenceMessenger.Default.Send(new LyricsStyleRelayoutChangedMessage(this));

    partial void OnLyricsLayoutOrientationChanged(LyricsLayoutOrientation value) =>
        WeakReferenceMessenger.Default.Send(new LyricsStyleRelayoutChangedMessage(this));

    partial void OnLyricsAlignmentTypeChanged(TextAlignmentType value) =>
        WeakReferenceMessenger.Default.Send(new LyricsStyleRelayoutChangedMessage(this));

    public object Clone()
    {
        return new LyricsStyleSettings
        {
            LyricsAlignmentType = LyricsAlignmentType,
            UseInternalLyricsAlignment = UseInternalLyricsAlignment,
            LyricsLayoutOrientation = LyricsLayoutOrientation,
            LyricsLayerOrder =
                new FullyObservableCollection<LyricsLayerConfig>(
                    LyricsLayerOrder.Select(p => (LyricsLayerConfig)p.Clone())),

            AutoWrap = AutoWrap,
            LyricsFontWeight = LyricsFontWeight,
            LyricsLineOverallSpacingFactor = LyricsLineOverallSpacingFactor,
            LyricsLineInnerSpacingFactor = LyricsLineInnerSpacingFactor,
            LyricsCJKFontFamily = LyricsCJKFontFamily,
            LyricsWesternFontFamily = LyricsWesternFontFamily,

            PlayingLineTopOffset = PlayingLineTopOffset
        };
    }
}
