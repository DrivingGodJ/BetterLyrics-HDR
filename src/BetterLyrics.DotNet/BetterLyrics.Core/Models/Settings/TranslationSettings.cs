using BetterLyrics.Core.Enums;
using BetterLyrics.Core.Helpers;
using BetterLyrics.Core.Messages.AppSettings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace BetterLyrics.Core.Models.Settings;

public partial class TranslationSettings : ObservableObject
{
    [ObservableProperty]
    public partial bool IsTranslationEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string SelectedTargetLanguageCode { get; set; } =
        LanguageHelper.GetDefaultTargetTranslationLanguageCode();

    [ObservableProperty]
    public partial ChineseConversion ChineseConversion { get; set; } = ChineseConversion.Unspecified;

    [ObservableProperty]
    public partial bool IsFilterEnabled { get; set; } = false;

    partial void OnIsTranslationEnabledChanged(bool value) =>
        WeakReferenceMessenger.Default.Send(new TranslationSettingsChangedMessage(nameof(IsTranslationEnabled)));

    partial void OnSelectedTargetLanguageCodeChanged(string value) =>
        WeakReferenceMessenger.Default.Send(new TranslationSettingsChangedMessage(nameof(SelectedTargetLanguageCode)));

    partial void OnChineseConversionChanged(ChineseConversion value) =>
        WeakReferenceMessenger.Default.Send(new TranslationSettingsChangedMessage(nameof(ChineseConversion)));

    partial void OnIsFilterEnabledChanged(bool value) =>
        WeakReferenceMessenger.Default.Send(new TranslationSettingsChangedMessage(nameof(IsFilterEnabled)));
}