using BetterLyrics.Core.Interfaces.Providers;
using BetterLyrics.Core.Interfaces.Services;
using BetterLyrics.Core.Models.Settings;
using BetterLyrics.Core.ViewModels;
using BetterLyrics.WinUI3.Views;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.System;

namespace BetterLyrics.WinUI3.Controls;

public sealed partial class LyricsWindowManagerControl : BaseUserControl
{
    private readonly ILocalizationService _localization = Ioc.Default.GetRequiredService<ILocalizationService>();
    private readonly ILogger<LyricsWindowManagerControl> _logger =
        Ioc.Default.GetRequiredService<ILogger<LyricsWindowManagerControl>>();

    private SettingsWindow? _settingsWindow;
    private bool _updatingStartup;

    public LyricsWindowManagerControlViewModel ViewModel => (LyricsWindowManagerControlViewModel)DataContext;

    public LyricsWindowManagerControl()
    {
        DataContext = Ioc.Default.GetRequiredService<LyricsWindowManagerControlViewModel>();
        InitializeComponent();
        AutomationProperties.SetName(SilentStartupToggle, _localization.GetLocalizedString("SettingsPageSilentStartup/Header"));
    }

    protected override void BindGeneratedControls() => Bindings?.Update();

    protected override void UnbindGeneratedControls() => Bindings?.StopTracking();

    protected override void OnLoaded()
    {
        _settingsWindow = Ioc.Default.GetRequiredService<IWindowManagerProvider>().GetWindow<SettingsWindow>();
        if (_settingsWindow != null) _settingsWindow.Activated += SettingsWindow_Activated;
        _ = UpdateStartupTaskAsync();
    }

    protected override void OnUnloaded()
    {
        if (_settingsWindow != null) _settingsWindow.Activated -= SettingsWindow_Activated;
        _settingsWindow = null;
    }

    private void SettingsWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
            _ = UpdateStartupTaskAsync();
    }

    private async void SilentStartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch { IsEnabled: true } toggle)
            await UpdateStartupTaskAsync(toggle.IsOn);
    }

    private async Task UpdateStartupTaskAsync(bool? enable = null)
    {
        if (_updatingStartup) return;
        _updatingStartup = true;
        var wasEnabled = enable.HasValue ? !enable.Value : SilentStartupToggle.IsOn;
        SilentStartupToggle.IsEnabled = false;

        try
        {
            var task = await StartupTask.GetAsync(BetterLyrics.Core.Constants.App.AutoStartupTaskId);
            var state = task.State;
            wasEnabled = state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;

            if (enable is true && state == StartupTaskState.Disabled)
                state = await task.RequestEnableAsync();
            else if (enable is false && state == StartupTaskState.Enabled)
            {
                task.Disable();
                state = task.State;
            }

            SilentStartupToggle.IsOn = state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
            SilentStartupToggle.IsEnabled = state is StartupTaskState.Enabled or StartupTaskState.Disabled;
            StartupSettingsLink.Visibility = state == StartupTaskState.DisabledByUser ? Visibility.Visible : Visibility.Collapsed;
            SilentStartupCard.Description = _localization.GetLocalizedString(state switch
            {
                StartupTaskState.DisabledByUser => "SettingsPageSilentStartupDisabledByUser",
                StartupTaskState.DisabledByPolicy or StartupTaskState.EnabledByPolicy => "SettingsPageSilentStartupManaged",
                _ => "SettingsPageSilentStartup/Description"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read or update BetterLyrics startup settings.");
            SilentStartupToggle.IsOn = wasEnabled;
            StartupSettingsLink.Visibility = Visibility.Collapsed;
            SilentStartupCard.Description = _localization.GetLocalizedString("SettingsPageSilentStartupUnavailable");
        }
        finally
        {
            _updatingStartup = false;
        }
    }

    private async void StartupSettingsLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await Launcher.LaunchUriAsync(new Uri("ms-settings:startupapps"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not open the Windows startup apps settings.");
            SilentStartupCard.Description = _localization.GetLocalizedString("SettingsPageSilentStartupUnavailable");
        }
    }

    private void ConfigButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LyricsWindowStatus window })
            ViewModel.ShowConfigPanelCommand.Execute(window);
    }

    private void WindowStatusListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs e)
    {
        ViewModel.AppSettings.WindowBoundsRecords.Refresh();
    }
}
