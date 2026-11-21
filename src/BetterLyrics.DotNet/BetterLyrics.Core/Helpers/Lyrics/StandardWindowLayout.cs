namespace BetterLyrics.Core.Helpers.Lyrics;

public readonly record struct StandardWindowLayout(
    double CoverWidth, double VisibleCoverWidth, double FadeStart, double FadeEnd, bool IsFeatherLimited,
    double LyricsLeft, double LyricsRightInset, double SongInfoLeft, double SongInfoWidth,
    double SongInfoBottom, double TitleFontSize, double ArtistFontSize)
{
    public static StandardWindowLayout Calculate(double width, double height, double coverAspect,
        bool featherEnabled, double fadeStartX, double fadeEndX)
    {
        var coverWidth = height * coverAspect;
        var inset = Math.Min(16, width * 0.04);
        var lyricsLimit = width / 2 - inset;
        var limitFeather = coverWidth > lyricsLimit;
        var feather = featherEnabled || limitFeather;
        var naturalEnd = coverWidth * Math.Clamp(fadeEndX, 0.01, 1);
        var visibleCover = feather ? Math.Min(naturalEnd, lyricsLimit) : coverWidth;
        var fadeStart = coverWidth * Math.Clamp(fadeStartX, 0, 1);
        if (limitFeather)
        {
            // Leave enough space for a soft edge even when the cover is much wider than the window.
            var minimumFade = Math.Min(48, visibleCover * 0.25);
            fadeStart = Math.Clamp(fadeStart, 0, visibleCover - minimumFade);
        }

        var lyricsLeft = Math.Min(visibleCover + inset, lyricsLimit);
        var infoArea = Math.Min(coverWidth, lyricsLeft);
        var infoLeft = Math.Min(Math.Clamp(height * 0.045, 10, 36), infoArea * 0.1);
        var infoWidth = Math.Min(440, Math.Max(0, infoArea - 2 * infoLeft));
        var titleSize = Math.Clamp(Math.Min(height * 0.064, infoWidth * 0.15), 10, 44);
        var artistSize = Math.Clamp(titleSize * 0.59, 9, 25);
        var infoBottom = Math.Min(height * 0.27, Math.Clamp(height * 0.136, 34, 96));

        return new StandardWindowLayout(coverWidth, visibleCover, fadeStart / coverWidth,
            visibleCover / coverWidth, limitFeather, lyricsLeft, inset, infoLeft, infoWidth,
            infoBottom, titleSize, artistSize);
    }
}
