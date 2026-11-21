using BetterLyrics.Core.Enums;
using BetterLyrics.Core.Messages.LyricsWindow;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace BetterLyrics.Core.Models.Settings;

public partial class LyricsEffectSettings : ObservableObject, ICloneable
{
    [ObservableProperty]
    public partial WordByWordEffectMode WordByWordEffectMode { get; set; } = WordByWordEffectMode.Auto;

    [ObservableProperty]
    public partial bool IsLyricsBlurEffectEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLyricsFadeOutEffectEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLyricsEdgeFeatheringEffectEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLyricsOutOfSightEffectEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLyricsGlowEffectEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLyricsHdrGlowEnabled { get; set; }

    [ObservableProperty]
    public partial LyricsEffectScope LyricsGlowEffectScope { get; set; } = LyricsEffectScope.LongDurationSyllable;

    [ObservableProperty]
    public partial int LyricsGlowEffectLongSyllableDuration { get; set; } = 700; // 700ms

    [ObservableProperty]
    public partial bool IsLyricsGlowEffectAmountAutoAdjust { get; set; } = true;

    [ObservableProperty]
    public partial int LyricsGlowEffectAmount { get; set; } = 8;

    [ObservableProperty]
    public partial bool IsLyricsShadowEffectEnabled { get; set; } = false;

    [ObservableProperty]
    public partial bool IsLyricsScaleEffectEnabled { get; set; } = true;

    [ObservableProperty]
    public partial int LyricsScaleEffectLongSyllableDuration { get; set; } = 700; // 700ms

    [ObservableProperty]
    public partial bool IsLyricsScaleEffectAmountAutoAdjust { get; set; } = true;

    [ObservableProperty]
    public partial int LyricsScaleEffectAmount { get; set; } = 115; // 115%

    [ObservableProperty]
    public partial bool IsLyricsFloatAnimationEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLyricsFloatAnimationAmountAutoAdjust { get; set; } = true;

    [ObservableProperty]
    public partial int LyricsFloatAnimationAmount { get; set; } = 8;

    [ObservableProperty]
    public partial int LyricsFloatAnimationDuration { get; set; } = 450; // 450ms

    [ObservableProperty]
    public partial int LyricsScrollDuration { get; set; } = 500;

    [ObservableProperty]
    public partial int LyricsScrollTopDuration { get; set; } = 500;

    [ObservableProperty]
    public partial int LyricsScrollBottomDuration { get; set; } = 500;

    [ObservableProperty]
    public partial int LyricsScrollTopDelay { get; set; } = 0;

    [ObservableProperty]
    public partial int LyricsScrollBottomDelay { get; set; } = 50;

    [ObservableProperty]
    public partial bool IsLyricsBrethingEffectEnabled { get; set; } = false;

    [ObservableProperty]
    public partial int LyricsBreathingIntensity { get; set; } = 80;

    [ObservableProperty]
    public partial bool IsLyricsGapDotsEnabled { get; set; } = true;

    [ObservableProperty]
    public partial int LyricsGapDotsMinimumDuration { get; set; } = 6000; // 6000ms

    partial void OnIsLyricsBlurEffectEnabledChanged(bool value) =>
        WeakReferenceMessenger.Default.Send(new LyricsEffectRelayoutChangedMessage(this));

    partial void OnIsLyricsFadeOutEffectEnabledChanged(bool value) =>
        WeakReferenceMessenger.Default.Send(new LyricsEffectRelayoutChangedMessage(this));

    partial void OnIsLyricsOutOfSightEffectEnabledChanged(bool value) =>
        WeakReferenceMessenger.Default.Send(new LyricsEffectRelayoutChangedMessage(this));

    partial void OnLyricsScrollDurationChanged(int value) =>
        WeakReferenceMessenger.Default.Send(new LyricsEffectRelayoutChangedMessage(this));

    partial void OnLyricsScrollTopDurationChanged(int value) =>
        WeakReferenceMessenger.Default.Send(new LyricsEffectRelayoutChangedMessage(this));

    partial void OnLyricsScrollBottomDurationChanged(int value) =>
        WeakReferenceMessenger.Default.Send(new LyricsEffectRelayoutChangedMessage(this));

    partial void OnLyricsScrollTopDelayChanged(int value) =>
        WeakReferenceMessenger.Default.Send(new LyricsEffectRelayoutChangedMessage(this));

    partial void OnLyricsScrollBottomDelayChanged(int value) =>
        WeakReferenceMessenger.Default.Send(new LyricsEffectRelayoutChangedMessage(this));

    partial void OnIsLyricsScaleEffectEnabledChanged(bool value) =>
        WeakReferenceMessenger.Default.Send(new LyricsEffectReloadChangedMessage(this));

    partial void OnIsLyricsGlowEffectEnabledChanged(bool value) =>
        WeakReferenceMessenger.Default.Send(new LyricsEffectReloadChangedMessage(this));

    partial void OnLyricsScaleEffectLongSyllableDurationChanged(int value) =>
        WeakReferenceMessenger.Default.Send(new LyricsEffectReloadChangedMessage(this));

    partial void OnLyricsGlowEffectLongSyllableDurationChanged(int value) =>
        WeakReferenceMessenger.Default.Send(new LyricsEffectReloadChangedMessage(this));

    public object Clone()
    {
        return new LyricsEffectSettings()
        {
            WordByWordEffectMode = WordByWordEffectMode,

            IsLyricsBlurEffectEnabled = IsLyricsBlurEffectEnabled,
            IsLyricsFadeOutEffectEnabled = IsLyricsFadeOutEffectEnabled,
            IsLyricsEdgeFeatheringEffectEnabled = IsLyricsEdgeFeatheringEffectEnabled,
            IsLyricsOutOfSightEffectEnabled = IsLyricsOutOfSightEffectEnabled,

            IsLyricsGlowEffectEnabled = IsLyricsGlowEffectEnabled,
            IsLyricsHdrGlowEnabled = IsLyricsHdrGlowEnabled,
            LyricsGlowEffectLongSyllableDuration = LyricsGlowEffectLongSyllableDuration,
            IsLyricsGlowEffectAmountAutoAdjust = IsLyricsGlowEffectAmountAutoAdjust,
            LyricsGlowEffectAmount = LyricsGlowEffectAmount,
            LyricsGlowEffectScope = LyricsGlowEffectScope,

            IsLyricsShadowEffectEnabled = IsLyricsShadowEffectEnabled,

            IsLyricsScaleEffectEnabled = IsLyricsScaleEffectEnabled,
            LyricsScaleEffectLongSyllableDuration = LyricsScaleEffectLongSyllableDuration,
            IsLyricsScaleEffectAmountAutoAdjust = IsLyricsScaleEffectAmountAutoAdjust,
            LyricsScaleEffectAmount = LyricsScaleEffectAmount,

            IsLyricsFloatAnimationEnabled = IsLyricsFloatAnimationEnabled,
            IsLyricsFloatAnimationAmountAutoAdjust = IsLyricsFloatAnimationAmountAutoAdjust,
            LyricsFloatAnimationAmount = LyricsFloatAnimationAmount,
            LyricsFloatAnimationDuration = LyricsFloatAnimationDuration,
            LyricsScrollDuration = LyricsScrollDuration,
            LyricsScrollTopDuration = LyricsScrollTopDuration,
            LyricsScrollBottomDuration = LyricsScrollBottomDuration,
            LyricsScrollTopDelay = LyricsScrollTopDelay,
            LyricsScrollBottomDelay = LyricsScrollBottomDelay,

            IsLyricsBrethingEffectEnabled = IsLyricsBrethingEffectEnabled,
            LyricsBreathingIntensity = LyricsBreathingIntensity,

            IsLyricsGapDotsEnabled = IsLyricsGapDotsEnabled,
            LyricsGapDotsMinimumDuration = LyricsGapDotsMinimumDuration
        };
    }
}
