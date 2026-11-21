using BetterLyrics.Core.Constants;
using BetterLyrics.Core.Enums;
using BetterLyrics.Core.Events;
using BetterLyrics.Core.Helpers;
using BetterLyrics.Core.Interfaces.Providers;
using BetterLyrics.Core.Interfaces.Services;
using BetterLyrics.Core.Interfaces.ViewModels;
using BetterLyrics.Core.Messages.SettingsNavigation;
using BetterLyrics.Core.Models;
using BetterLyrics.Core.Models.Settings;
using BetterLyrics.Core.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json;

namespace BetterLyrics.Core.ViewModels;

/// <summary>
/// Transient ViewModel
/// </summary>
public partial class PlaybackSettingsControlViewModel : ObservableRecipient, ITransientViewModel,
    IRecipient<PlaybackLibSettingsNavigationRequestedMessage>
{
    private readonly IAppUIThreadProvider _appUiThreadProvider;
    private readonly IGlobalToastProvider _globalToastProvider;
    private readonly IPasswordVaultProvider _passwordVaultProvider;
    private readonly IFilePickerProvider _filePickerProvider;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<PlaybackSettingsControlViewModel> _logger;

    public IGsmtcService GsmtcService { get; }

    [ObservableProperty] public partial AppSettings AppSettings { get; set; }

    [ObservableProperty] public partial MediaSourceProviderInfo? SelectedMediaSourceProvider { get; set; }

    public ObservableCollection<PlaybackLibSettingsNavMenuItem> NavItems = [];

    [ObservableProperty] public partial PlaybackLibSettingsNavMenuItem? SelectedNavItem { get; set; }

    [ObservableProperty] public partial bool IsLxMusicServerTesting { get; set; } = false;

    [ObservableProperty] public partial int SelectedTargetLanguageIndex { get; set; }

    [ObservableProperty] public partial bool IsConfigPanelOpen { get; set; }

    public PlaybackSettingsControlViewModel(
    ISettingsService settingsService, IGsmtcService gsmtcService, IAppUIThreadProvider appUiThreadProvider, IPasswordVaultProvider passwordVaultProvider,
    IGlobalToastProvider globalToastProvider, IFilePickerProvider filePickerProvider,
    ILocalizationService localizationService, ILogger<PlaybackSettingsControlViewModel> logger)
    {
        GsmtcService = gsmtcService;

        _logger = logger;
        _appUiThreadProvider = appUiThreadProvider;
        _localizationService = localizationService;
        _passwordVaultProvider = passwordVaultProvider;
        _globalToastProvider = globalToastProvider;
        _filePickerProvider = filePickerProvider;



        AppSettings = settingsService.AppSettings;


        SelectedTargetLanguageIndex = LanguageHelper.SupportedTranslationTargetLanguages.ToList().FindIndex(x =>
            x.LanguageCode == AppSettings.TranslationSettings.SelectedTargetLanguageCode);


        SelectedMediaSourceProvider = AppSettings.MediaSourceProvidersInfo.FirstOrDefault();

        NavItems.Add(new PlaybackLibSettingsNavMenuItem
        {
            Label = _localizationService.GetLocalizedString("SettingsPageConfigPlaybackSource/Text"),
            Section = PlaybackLibSettingsSection.General
        });
        NavItems.Add(new PlaybackLibSettingsNavMenuItem
        {
            Label = _localizationService.GetLocalizedString("SettingsPageLyricsProcessing/Text"),
            Section = PlaybackLibSettingsSection.LyricsProcessing
        });
        NavItems.Add(new PlaybackLibSettingsNavMenuItem
        {
            Label = _localizationService.GetLocalizedString("SettingsPageRealtimeStatus/Text"),
            Section = PlaybackLibSettingsSection.RealtimeStatus
        });
        SelectedNavItem = NavItems[0];
    }

    protected override void OnActivated()
    {
        base.OnActivated();

        AppSettings.MediaSourceProvidersInfo.CollectionChanged += MediaSourceProvidersInfo_CollectionChanged;

        // 重新激活时同步一次外部状态，避免控件隐藏期间状态变旧
    }

    protected override void OnDeactivated()
    {
        AppSettings.MediaSourceProvidersInfo.CollectionChanged -= MediaSourceProvidersInfo_CollectionChanged;

        base.OnDeactivated();
    }

    partial void OnSelectedTargetLanguageIndexChanged(int value)
    {
        AppSettings.TranslationSettings.SelectedTargetLanguageCode =
            LanguageHelper.SupportedTranslationTargetLanguages[value].LanguageCode;
    }

    partial void OnSelectedNavItemChanged(PlaybackLibSettingsNavMenuItem? value)
    {
        WeakReferenceMessenger.Default.Send(
            new SettingsSectionChangedMessage(this, SettingsSection.PlaybackLib, SelectedNavItem?.Section));
    }

    partial void OnSelectedMediaSourceProviderChanged(MediaSourceProviderInfo? value)
    {
        WeakReferenceMessenger.Default.Send(new SettingsSectionChangedMessage(
            this, SettingsSection.PlaybackLib, SelectedNavItem?.Section, SelectedMediaSourceProvider?.Provider));
    }

    private void MediaSourceProvidersInfo_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SelectedMediaSourceProvider = AppSettings.MediaSourceProvidersInfo.FirstOrDefault();
    }

    [RelayCommand]
    private async Task StopTrackAsync()
    {
        // 该方法应仅在针对内置播放器的 InfoBar 打开时调用
        await GsmtcService.StopAsync();
    }

    [RelayCommand]
    private async Task ImportMemoryReaderConfigAsync()
    {
        string[] fileTypeFilter = [".json"];
        var (_, filePath) = await _filePickerProvider.PickSingleFileAsync(fileTypeFilter, WindowType.SettingsWindow);
        if (filePath != null)
        {
            var json = await File.ReadAllTextAsync(filePath);
            SelectedMediaSourceProvider?.MemoryReaderConfig = JsonSerializer.Deserialize(json,
                SourceGenerationContext.Default.MemoryReaderConfig);
            _globalToastProvider.Show("ImportSettingsSuccess", null, MessageSeverity.Success);
        }
    }

    [RelayCommand]
    private void LxMusicServerTest()
    {
        IsLxMusicServerTesting = true;
        _ = Task.Run(async () =>
        {
            var testResult =
                await NetHelper.CheckConnectivityAsync($"{AppSettings.GeneralSettings.LXMusicServer}/status");
            _appUiThreadProvider.Execute(() =>
            {
                if (testResult)
                    _globalToastProvider.Show("SettingsPageServerTestSuccessInfo", null, MessageSeverity.Success);
                else
                    _globalToastProvider.Show("SettingsPageServerTestFailedInfo", null, MessageSeverity.Error);

                IsLxMusicServerTesting = false;
            });
        });
    }

    [RelayCommand]
    private void CloseConfigPanel()
    {
        SelectedMediaSourceProvider = null;
        IsConfigPanelOpen = false;
    }

    [RelayCommand]
    private void ShowConfigPanel(MediaSourceProviderInfo? info)
    {
        SelectedMediaSourceProvider = info;
        IsConfigPanelOpen = info != null;
        SelectedNavItem = NavItems[0];
    }

    public void Receive(PlaybackLibSettingsNavigationRequestedMessage message)
    {
        var target = message.TargetSessionId != null
            ? AppSettings.MediaSourceProvidersInfo.FirstOrDefault(w => w.Provider == message.TargetSessionId)
            : null;

        ShowConfigPanel(target);
        SelectedNavItem = NavItems.FirstOrDefault(x => x.Section == message.TargetSection);
    }
}