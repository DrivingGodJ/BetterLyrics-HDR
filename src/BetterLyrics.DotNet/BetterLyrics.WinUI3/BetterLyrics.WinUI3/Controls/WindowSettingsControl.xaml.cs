using BetterLyrics.Core.Models.Settings;
using BetterLyrics.Core.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace BetterLyrics.WinUI3.Controls;

public sealed partial class WindowSettingsControl : BaseUserControl
{
    public WindowSettingsControlViewModel ViewModel => (WindowSettingsControlViewModel)DataContext;

    public static readonly DependencyProperty LyricsWindowStatusProperty =
        DependencyProperty.Register(nameof(LyricsWindowStatus), typeof(LyricsWindowStatus),
            typeof(WindowSettingsControl), new PropertyMetadata(default, OnLyricsWindowStatusChanged));

    public LyricsWindowStatus LyricsWindowStatus
    {
        get => (LyricsWindowStatus)GetValue(LyricsWindowStatusProperty);
        set => SetValue(LyricsWindowStatusProperty, value);
    }

    public WindowSettingsControl()
    {
        InitializeComponent();
        DataContext = Ioc.Default.GetRequiredService<WindowSettingsControlViewModel>();
    }

    protected override void BindGeneratedControls()
    {
    }

    protected override void UnbindGeneratedControls()
    {
    }

    protected override void OnLoaded()
    {
        ViewModel.Attach();
    }

    protected override void OnUnloaded()
    {
        ViewModel?.Detach();
    }

    private static void OnLyricsWindowStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not WindowSettingsControl control) return;

        control.ViewModel.SetLyricsWindowStatus(control.LyricsWindowStatus);
    }

    private void RefreshMonitorButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RefreshMonitors();
    }
}
