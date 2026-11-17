using BetterLyrics.Core.Models.Domain;
using BetterLyrics.Core.Models.Lyrics;
using BetterLyrics.Core.Helpers;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using System;
using Windows.Foundation;

namespace BetterLyrics.WinUI3.Models.Lyrics;

public class RenderLyricsChar : BaseRenderLyricsChar
{
    private ColorMatrixEffect? _hdrGlowColor;
    private MorphologyEffect? _hdrGlowSpread;
    private GaussianBlurEffect? _hdrGlow;
    private float _hdrHighlightGain;

    public RenderLyricsChar(BaseLyrics lyricsChars, AppRect layoutRect) : base(lyricsChars, layoutRect)
    {
        Crop = new CropEffect { BorderMode = EffectBorderMode.Hard };
        Glow = new GaussianBlurEffect { Source = Crop, BorderMode = EffectBorderMode.Soft };
    }

    public CropEffect Crop { get; }
    public GaussianBlurEffect Glow { get; }

    public (ICanvasImage Fill, float Strength) DrawHdrGlow(CanvasDrawingSession ds, Rect sourceRect,
        Rect destinationRect, float gain)
    {
        if (_hdrGlowColor == null)
        {
            _hdrGlowColor = CreateHdrPeakEffect(Crop, gain);
            _hdrHighlightGain = gain;
        }
        else if (_hdrHighlightGain != gain)
        {
            _hdrHighlightGain = gain;
            _hdrGlowColor.ColorMatrix = GetHdrPeakMatrix(gain);
        }

        // Give the bright inner halo room outside the glyph before feathering its edge.
        _hdrGlowSpread ??= new MorphologyEffect
        {
            Source = _hdrGlowColor,
            Mode = MorphologyEffectMode.Dilate,
            Width = 5,
            Height = 5,
            BufferPrecision = CanvasBufferPrecision.Precision16Float
        };
        _hdrGlow ??= new GaussianBlurEffect
        {
            Source = _hdrGlowSpread,
            BorderMode = EffectBorderMode.Soft,
            BufferPrecision = CanvasBufferPrecision.Precision16Float
        };

        _hdrGlow.BlurAmount = Glow.BlurAmount;
        var strength = EasingHelper.SmoothStep(Math.Clamp(Glow.BlurAmount / 4, 0, 1));
        var passed = EasingHelper.SmoothStep(Math.Clamp((float)(ProgressPlayed - 0.65) / 0.35f, 0, 1));
        ds.DrawImage(_hdrGlow, destinationRect, sourceRect, strength * (0.82f - 0.59f * passed));
        return (_hdrGlowColor, strength);
    }

    internal static ColorMatrixEffect CreateHdrPeakEffect(ICanvasImage source, float gain) =>
        new()
        {
            Source = source,
            ClampOutput = false,
            BufferPrecision = CanvasBufferPrecision.Precision16Float,
            ColorMatrix = GetHdrPeakMatrix(gain)
        };

    private static Matrix5x4 GetHdrPeakMatrix(float gain) =>
        new() { M44 = 1, M51 = gain, M52 = gain, M53 = gain };

    public void DisposeEffetcts()
    {
        Glow?.Dispose();
        _hdrGlow?.Dispose();
        _hdrGlowSpread?.Dispose();
        _hdrGlowColor?.Dispose();
        Crop?.Dispose();
    }
}
