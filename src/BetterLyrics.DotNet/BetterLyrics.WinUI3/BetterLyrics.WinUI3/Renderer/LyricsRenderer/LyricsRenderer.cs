using BetterLyrics.Core.Enums;
using BetterLyrics.Core.Models.Settings;
using BetterLyrics.WinUI3.Extensions;
using BetterLyrics.WinUI3.Models.Lyrics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;

namespace BetterLyrics.WinUI3.Renderer.LyricsRenderer;

public partial class LyricsRenderer : EffectRendererBase, IDisposable
{
    private const byte HoverHighlightAlpha = 16;
    private const byte PressHighlightAlpha = 32;
    private const double HoverHighlightScale = 1.10;
    private const double PressHighlightScale = 1.05;

    private readonly EdgeFadeMaskRenderer _edgeFadeMaskRenderer = new();
    private readonly GapIndicatorRenderer _gapIndicatorRenderer = new();

    // 每行独立的高亮动画状态，保证从一行划到另一行时旧高亮淡出、新高亮淡入
    private readonly Dictionary<int, LineHighlightAnimation> _lineHighlightAnimations = new();

    private IList<RenderLyricsLine>? _lineHighlightAnimationLines;

    public int MouseHoverLineIndex { get; set; } = -1;
    public bool IsMousePressing { get; set; } = false;

    public int StartVisibleLineIndex { get; set; } = 0;
    public int EndVisibleLineIndex { get; set; } = 0;

    public double UserScrollOffset { get; set; } = 0;

    public double LyricsX { get; set; }
    public double LyricsY { get; set; }

    public double LyricsWidth { get; set; }
    public double LyricsHeight { get; set; }

    public double LyricsOpacity { get; set; } = 1;
    public double PlayingLineTopOffsetFactor { get; set; }

    public double CurrentProgressMs { get; set; }

    public LyricsWindowStatus? LyricsWindowStatus { get; set; }

    public IList<RenderLyricsLine>? RenderLyricsLines { get; set; }

    /// <summary>
    /// 当前所在的无歌词间隙，为 null 时不显示呼吸圆点
    /// </summary>
    public GapIndicatorState? GapIndicator { get; set; }

    public void Dispose()
    {
        _edgeFadeMaskRenderer.Dispose();
    }

    public void Draw(ICanvasAnimatedControl control, CanvasDrawingSession ds, float hdrHighlightGain = 1)
    {
        if (LyricsOpacity == 0) return;

        if (LyricsWindowStatus == null) return;
        DrawLyricsWithEdgeFadeHandled(control, ds, hdrHighlightGain);
    }

    private void DrawLyricsWithEdgeFadeHandled(ICanvasAnimatedControl control, CanvasDrawingSession ds,
        float hdrHighlightGain)
    {
        if (LyricsWindowStatus == null) return;
        if (_edgeFadeMaskRenderer.Brush != null &&
            (!LyricsWindowStatus.LyricsStyleSettings.AutoWrap ||
             LyricsWindowStatus.LyricsEffectSettings.IsLyricsEdgeFeatheringEffectEnabled))
            using (ds.CreateLayer(_edgeFadeMaskRenderer.Brush))
            {
                DrawLyrics(control, ds, hdrHighlightGain);
            }
        else
            DrawLyrics(control, ds, hdrHighlightGain);
    }

    private void DrawLyrics(ICanvasAnimatedControl control, CanvasDrawingSession ds, float hdrHighlightGain)
    {
        if (RenderLyricsLines == null || LyricsWindowStatus == null) return;

        var effectSettings = LyricsWindowStatus.LyricsEffectSettings;
        var styleSettings = LyricsWindowStatus.LyricsStyleSettings;
        var isBreathingEnabled = effectSettings.IsLyricsBrethingEffectEnabled;

        var isVertical = styleSettings.LyricsLayoutOrientation == LyricsLayoutOrientation.Vertical;

        LyricsLineRendererBase lineRenderer = isVertical
            ? new VerticalLyricsLineRenderer()
            : new HorizontalLyricsLineRenderer();

        lineRenderer.LyricsWidth = LyricsWidth;
        lineRenderer.LyricsHeight = LyricsHeight;
        lineRenderer.LyricsWindowStatus = LyricsWindowStatus;
        lineRenderer.HdrHighlightGain = hdrHighlightGain;

        // 提前计算全局基准坐标
        var baseScrollX = isVertical
            ? UserScrollOffset + LyricsX + LyricsWidth * (1 - PlayingLineTopOffsetFactor)
            : LyricsX;

        var baseScrollY = isVertical
            ? LyricsY
            : UserScrollOffset + LyricsY + LyricsHeight * PlayingLineTopOffsetFactor;

        // 开始渲染循环
        for (var i = StartVisibleLineIndex; i <= EndVisibleLineIndex; i++)
        {
            if (i < 0 || i >= RenderLyricsLines.Count) continue;
            var line = RenderLyricsLines[i];

            // 空行只用于占位无歌词间隙，由呼吸圆点代替绘制
            if (line == null || line.IsGapLine) continue;
            if (line.PrimaryTextLayout == null || line.PrimaryTextLayout.LayoutBounds.Width <= 0) continue;

            var isPlaying = line.GetIsPlaying(CurrentProgressMs);

            // 更新重用对象的动态属性
            lineRenderer.Line = line;
            lineRenderer.IsPlaying = isPlaying;
            lineRenderer.CurrentProgressMs = CurrentProgressMs;

            // 计算当前行的基础位移
            var currentXOffset = baseScrollX;
            var currentYOffset = baseScrollY;

            if (isVertical)
                currentXOffset += line.OffsetTransition.Value;
            else
                currentYOffset += line.OffsetTransition.Value;

            // 呼吸效果按每行的权重应用：行开始 / 结束播放时权重平滑过渡，避免缩放突然跳变
            ApplyBreathingTransform(ds, line.CenterPosition, isBreathingEnabled, line.BreathingWeightTransition.Value);

            // 应用缩放矩阵
            ds.Transform *= Matrix3x2.CreateScale((float)line.ScaleTransition.Value, line.CenterPosition);

            // 应用最终位移矩阵
            ds.Transform *= Matrix3x2.CreateTranslation((float)currentXOffset, (float)currentYOffset);

            line.EnsureCaches(control, 0);
            if (line.CachedStroke == null || line.CachedFill == null) continue;
            if (line.UnplayedFillTint == null || line.UnplayedStrokeTint == null ||
                line.UnplayedComposite == null) continue;

            line.UnplayedFillTint.Color = ColorExtensions.FromAppColor(line.UnplayedFillColorTransition.Value);
            line.UnplayedStrokeTint.Color = ColorExtensions.FromAppColor(line.UnplayedStrokeColorTransition.Value);

            // 真正的绘制调用
            lineRenderer.Draw(control, ds);

            // 鼠标悬停的高亮背景框：悬浮只淡入淡出，缩放仅由按下 / 释放驱动
            if (_lineHighlightAnimations.TryGetValue(i, out var highlightAnimation))
            {
                var hoverAmount = highlightAnimation.Hover.Value;
                if (hoverAmount > 0.001)
                {
                    var pressAmount = highlightAnimation.Press.Value;

                    var opacity = Math.Clamp(
                        (HoverHighlightAlpha + (PressHighlightAlpha - HoverHighlightAlpha) * pressAmount) *
                        hoverAmount, 0, 255);
                    var scale = HoverHighlightScale -
                                (HoverHighlightScale - PressHighlightScale) * pressAmount;

                    // 这里的 TopLeft 和 BottomRight 无论横竖排都是绝对包围盒，所以可以直接 Scale
                    ds.FillRoundedRectangle(
                        new Rect(line.TopLeftPosition.ToPoint(), line.BottomRightPosition.ToPoint()).Scale(scale),
                        8, 8,
                        Color.FromArgb((byte)opacity, 255, 255, 255));
                }
            }

#if DEBUG && false
                ds.DrawRectangle(new Rect(line.TopLeftPosition.ToPoint(), line.BottomRightPosition.ToPoint()), Colors.Cyan);

                // Debug 线也需要区分横竖排，如果是竖排，辅助线应该是垂直的
                if (isVertical)
                    ds.DrawLine(new Vector2(line.CenterPosition.X, line.TopLeftPosition.Y), new Vector2(line.CenterPosition.X, line.BottomRightPosition.Y), Colors.Cyan);
                else
                    ds.DrawLine(new Vector2(line.TopLeftPosition.X, line.CenterPosition.Y), new Vector2(line.BottomRightPosition.X, line.CenterPosition.Y), Colors.Cyan);

                ds.DrawText($"({line.CenterPosition.X}, {line.CenterPosition.Y})", line.CenterPosition, Colors.Cyan);
#endif

            ds.Transform = Matrix3x2.Identity;
        }

        DrawGapIndicator(ds, isVertical, baseScrollX, baseScrollY, styleSettings);
    }

    /// <summary>
    /// 绘制超长无歌词间隙中的呼吸圆点
    /// </summary>
    private void DrawGapIndicator(CanvasDrawingSession ds, bool isVertical, double baseScrollX, double baseScrollY,
        LyricsStyleSettings styleSettings)
    {
        if (GapIndicator is not { } indicator) return;
        if (LyricsWindowStatus == null || RenderLyricsLines == null) return;
        if (indicator.ReferenceLineIndex < 0 || indicator.ReferenceLineIndex >= RenderLyricsLines.Count) return;

        var referenceLine = RenderLyricsLines[indicator.ReferenceLineIndex];
        if (referenceLine == null) return;

        // 行绘制可能因为缓存缺失提前 continue 而残留变换
        ds.Transform = Matrix3x2.Identity;

        // 圆点跟随参考行一起滚动，间奏开始时它会与歌词一起滑到当前行位置
        var flowPosition = referenceLine.OffsetTransition.Value + indicator.FlowOffset;

        var alignment = styleSettings.UseInternalLyricsAlignment
            ? referenceLine.HorizontalAlignmentType ?? styleSettings.LyricsAlignmentType
            : styleSettings.LyricsAlignmentType;

        if (alignment == TextAlignmentType.LeftRight)
            alignment = indicator.ReferenceLineIndex % 2 == 0
                ? TextAlignmentType.Left
                : TextAlignmentType.Right;

        // 竖排时圆点上下堆叠，因此宽高相同
        var groupSize = GapIndicatorRenderer.MeasureWidth(indicator.LineHeight);

        float centerX;
        float centerY;

        if (isVertical)
        {
            centerX = (float)(baseScrollX + flowPosition);
            centerY = (float)(alignment switch
            {
                TextAlignmentType.Left => LyricsY + groupSize / 2,
                TextAlignmentType.Right => LyricsY + LyricsHeight - groupSize / 2,
                _ => LyricsY + LyricsHeight / 2
            });
        }
        else
        {
            centerX = (float)(alignment switch
            {
                TextAlignmentType.Left => LyricsX + groupSize / 2,
                TextAlignmentType.Right => LyricsX + LyricsWidth - groupSize / 2,
                _ => LyricsX + LyricsWidth / 2
            });
            centerY = (float)(baseScrollY + flowPosition);
        }

        _gapIndicatorRenderer.Draw(
            ds,
            LyricsWindowStatus,
            new Vector2(centerX, centerY),
            isVertical,
            CurrentProgressMs,
            indicator);
    }

    private void UpdateEdgeFadeMask(ICanvasAnimatedControl sender)
    {
        if (LyricsWindowStatus == null) return;

        var isVertical = LyricsWindowStatus.LyricsStyleSettings.LyricsLayoutOrientation ==
                         LyricsLayoutOrientation.Vertical;

        var autoWrapFadeWidth = LyricsWindowStatus.LyricsStyleSettings.AutoWrap ? 0 : 16;
        var lyricsEdgeFadeWidth =
            LyricsWindowStatus.LyricsEffectSettings.IsLyricsEdgeFeatheringEffectEnabled ? 16 : 0;

        if (isVertical)
            _edgeFadeMaskRenderer.Update(sender, new Rect(LyricsX, LyricsY - 16, LyricsWidth, LyricsHeight + 32),
                lyricsEdgeFadeWidth, autoWrapFadeWidth, lyricsEdgeFadeWidth, autoWrapFadeWidth);
        else
            _edgeFadeMaskRenderer.Update(sender, new Rect(LyricsX - 16, LyricsY, LyricsWidth + 32, LyricsHeight),
                autoWrapFadeWidth, lyricsEdgeFadeWidth, autoWrapFadeWidth, lyricsEdgeFadeWidth);
    }

    private void UpdateLineHighlightAnimations(TimeSpan elapsedTime)
    {
        // 歌词数据切换后，旧的行索引不再有效，直接清空动画状态
        if (!ReferenceEquals(_lineHighlightAnimationLines, RenderLyricsLines))
        {
            _lineHighlightAnimationLines = RenderLyricsLines;
            _lineHighlightAnimations.Clear();
        }

        var hoveredLineIndex = MouseHoverLineIndex;
        var isPressing = IsMousePressing && hoveredLineIndex >= 0;

        if (hoveredLineIndex >= 0 && !_lineHighlightAnimations.ContainsKey(hoveredLineIndex))
        {
            _lineHighlightAnimations[hoveredLineIndex] = new LineHighlightAnimation(hoveredLineIndex);
        }

        List<int>? finishedLineIndices = null;

        foreach (var (lineIndex, animation) in _lineHighlightAnimations)
        {
            var isHovered = lineIndex == hoveredLineIndex;

            animation.SetHovered(isHovered);
            animation.SetPressed(isHovered && isPressing);

            animation.Hover.Update(elapsedTime);
            animation.Press.Update(elapsedTime);

            // 已淡出的行不再需要保留动画状态
            if (!isHovered && !animation.Hover.IsTransitioning && animation.Hover.Value <= 0.001)
                (finishedLineIndices ??= []).Add(lineIndex);
        }

        if (finishedLineIndices == null) return;

        foreach (var lineIndex in finishedLineIndices) _lineHighlightAnimations.Remove(lineIndex);
    }

    public void Update(ICanvasAnimatedControl sender, TimeSpan elapsedTime, float bassEnergy, int breathingIntensity)
    {
        UpdateLineHighlightAnimations(elapsedTime);
        UpdateBreathing(bassEnergy, breathingIntensity);
        UpdateEdgeFadeMask(sender);
    }
}
