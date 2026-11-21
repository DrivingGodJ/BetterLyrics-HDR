using CommunityToolkit.Mvvm.Input;

namespace BetterLyrics.Core.Interfaces.Services;

public interface INavigationService
{
    IRelayCommand OpenSettingsWindowCommand { get; }
    IRelayCommand OpenLyricsWindowSwitchWindowCommand { get; }
    IRelayCommand OpenLyricsSearchWindowCommand { get; }
    IRelayCommand OpenStatsDashboardWindowCommand { get; }
}