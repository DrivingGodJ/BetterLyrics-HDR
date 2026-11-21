using BetterLyrics.Core.Models.Settings;
using BetterLyrics.Core.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BetterLyrics.WinUI3.Controls;

public sealed partial class LyricsWindowManagerControl : BaseUserControl
{
    public LyricsWindowManagerControlViewModel ViewModel => (LyricsWindowManagerControlViewModel)DataContext;

    public LyricsWindowManagerControl()
    {
        DataContext = Ioc.Default.GetRequiredService<LyricsWindowManagerControlViewModel>();
        InitializeComponent();
    }

    protected override void BindGeneratedControls() => Bindings?.Update();

    protected override void UnbindGeneratedControls() => Bindings?.StopTracking();

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
