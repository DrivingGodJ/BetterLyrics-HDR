using BetterLyrics.Core.Models.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace BetterLyrics.WinUI3.Controls;

public sealed partial class LyricsStyleSettingsControl : UserControl
{
    public static readonly DependencyProperty LyricsWindowStatusProperty =
        DependencyProperty.Register(nameof(LyricsWindowStatus), typeof(LyricsWindowStatus),
            typeof(LyricsStyleSettingsControl), new PropertyMetadata(null));

    public LyricsWindowStatus LyricsWindowStatus
    {
        get => (LyricsWindowStatus)GetValue(LyricsWindowStatusProperty);
        set => SetValue(LyricsWindowStatusProperty, value);
    }

    public LyricsStyleSettingsControl()
    {
        InitializeComponent();
    }

    private void LyricsLayerOrderListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        LyricsWindowStatus.LyricsStyleSettings.LyricsLayerOrder.Refresh();
    }
}