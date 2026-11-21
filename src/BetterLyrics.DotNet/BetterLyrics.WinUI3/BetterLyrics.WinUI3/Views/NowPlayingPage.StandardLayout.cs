using BetterLyrics.Core.Helpers.Lyrics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;

namespace BetterLyrics.WinUI3.Views;

public sealed partial class NowPlayingPage
{
    private StandardWindowLayout _standardLayout;
    private DropShadow? _titleShadow;
    private DropShadow? _artistShadow;

    private bool UsesStandardLayout => !IsBuiltInOnly &&
        _lyricsWindowStatus is { IsPinToTaskbar: false, IsWallpaper: false, IsWorkArea: false };

    private void OnAlbumArtSourceChanged(DependencyObject sender, DependencyProperty property)
    {
        if (UsesStandardLayout) ApplyStandardLayout();
    }

    private void ApplyStandardLayout()
    {
        if (_lyricsWindowStatus == null || RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0) return;

        var width = RootGrid.ActualWidth;
        var height = RootGrid.ActualHeight;
        var aspect = AlbumArtImageSwitcher.Source is BitmapSource { PixelWidth: > 0, PixelHeight: > 0 } image
            ? (double)image.PixelWidth / image.PixelHeight : 1.0;
        var art = _lyricsWindowStatus.AlbumArtAreaEffectSettings;
        var layout = StandardWindowLayout.Calculate(width, height, aspect,
            art.FadeOut, art.FadeOutStartPointX, art.FadeOutEndPointX);
        _standardLayout = layout;

        // The cover extends to its natural width. The mask removes the excess without rescaling the image.
        AlbumArtContainer.HorizontalAlignment = HorizontalAlignment.Left;
        AlbumArtContainer.VerticalAlignment = VerticalAlignment.Top;
        AlbumArtContainer.Width = layout.CoverWidth;
        AlbumArtContainer.Height = height;
        AlbumArtContainer.Visibility = Visibility.Visible;
        AlbumArtMaskContainer.Margin = new Thickness(0);
        AlbumArtImageSwitcher.Margin = new Thickness(0);
        AlbumArtImageSwitcher.Stretch = Stretch.UniformToFill;
        AlbumArtGradientBrush.StartPoint = layout.IsFeatherLimited
            ? new Point(layout.FadeStart, 0) : new Point(art.FadeOutStartPointX, art.FadeOutStartPointY);
        AlbumArtGradientBrush.EndPoint = layout.IsFeatherLimited
            ? new Point(layout.FadeEnd, 0) : new Point(art.FadeOutEndPointX, art.FadeOutEndPointY);
        AlbumArtGradientBrushEnd.Color = art.FadeOut || layout.IsFeatherLimited
            ? Microsoft.UI.Colors.Transparent : Microsoft.UI.Colors.White;
        NowPlayingCanvas.AlbumArtRect = new Rect(0, 0, layout.IsFeatherLimited ? layout.VisibleCoverWidth : layout.CoverWidth, height);

        LyricsContainer.Margin = new Thickness(layout.LyricsLeft, 0, layout.LyricsRightInset, 0);
        LyricsContainer.Visibility = Visibility.Visible;
        NowPlayingCanvas.LyricsStartX = layout.LyricsLeft;
        NowPlayingCanvas.LyricsStartY = 0;
        NowPlayingCanvas.LyricsWidth = width - layout.LyricsLeft - layout.LyricsRightInset;
        NowPlayingCanvas.LyricsHeight = height;
        NowPlayingCanvas.LyricsOpacity = 1;

        var artistRowHeight = Math.Ceiling(layout.ArtistFontSize * 1.45);
        var titleRowHeight = Math.Ceiling(layout.TitleFontSize * 1.45);
        PlaceSongInfo(SongTitleContainer, layout, titleRowHeight, layout.SongInfoBottom + artistRowHeight);
        PlaceSongInfo(SongArtistContainer, layout, artistRowHeight, layout.SongInfoBottom);
        TitleTextBlock.FontSize = layout.TitleFontSize;
        ArtistsTextBlock.FontSize = layout.ArtistFontSize;
        TitleAutoScrollHoverEffectView.ScrollingPixelsPreSecond = (int)layout.TitleFontSize;
        ArtistsAutoScrollHoverEffectView.ScrollingPixelsPreSecond = (int)layout.ArtistFontSize;
        UpdateSongInfoShadows();

        PlaybackControlContainer.VerticalAlignment = VerticalAlignment.Bottom;
        PlaybackControlContainer.Margin = new Thickness(12, 0, 12, 0);
        PlaybackControlContainer.Visibility = Visibility.Visible;
    }

    private static void PlaceSongInfo(Grid container, StandardWindowLayout layout, double height, double bottom)
    {
        container.HorizontalAlignment = HorizontalAlignment.Left;
        container.VerticalAlignment = VerticalAlignment.Bottom;
        container.Width = layout.SongInfoWidth;
        container.Height = height;
        container.Margin = new Thickness(layout.SongInfoLeft, 0, 0, bottom);
        container.Visibility = Visibility.Visible;
    }

    private void UpdateSongInfoShadows()
    {
        UpdateTextShadow(TitleTextBlock, TitleShadowHost, ref _titleShadow);
        UpdateTextShadow(ArtistsTextBlock, ArtistShadowHost, ref _artistShadow);
    }

    private static void UpdateTextShadow(TextBlock text, Grid host, ref DropShadow? shadow)
    {
        if (!text.IsLoaded || text.Inlines.Count == 0 || text.Foreground is not SolidColorBrush foreground) return;

        if (shadow == null)
        {
            var alphaMask = text.GetAlphaMask();
            if (alphaMask == null) return;

            var compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;
            shadow = compositor.CreateDropShadow();
            shadow.Mask = alphaMask;
            var visual = compositor.CreateSpriteVisual();
            visual.RelativeSizeAdjustment = Vector2.One;
            visual.Shadow = shadow;
            ElementCompositionPreview.SetElementChildVisual(host, visual);
        }

        Color color = foreground.Color;
        var lightText = color.R * 0.2126 + color.G * 0.7152 + color.B * 0.0722 >= 140;
        shadow.Color = lightText ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White;
        shadow.Opacity = lightText ? 0.88f : 0.72f;
        shadow.Offset = new Vector3(0, lightText ? 2 : 0, 0);
        shadow.BlurRadius = (float)Math.Clamp(text.FontSize * 0.24, 3, 10);
    }

    private void ClearSongInfoShadows()
    {
        if (_titleShadow != null)
        {
            ElementCompositionPreview.SetElementChildVisual(TitleShadowHost, null);
            _titleShadow.Dispose();
            _titleShadow = null;
        }

        if (_artistShadow != null)
        {
            ElementCompositionPreview.SetElementChildVisual(ArtistShadowHost, null);
            _artistShadow.Dispose();
            _artistShadow = null;
        }
    }
}
