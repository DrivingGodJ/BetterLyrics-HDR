using Microsoft.Graphics.Canvas;
using System;
using System.Numerics;

namespace BetterLyrics.WinUI3.Renderer;

public abstract class EffectRendererBase
{
    protected float _currentScale = 1.0f;
    private float _targetScale = 1.0f;

    protected void UpdateBreathing(float bassEnergy, int intensity)
    {
        if (intensity <= 0)
        {
            _currentScale = 1.0f;
            return;
        }

        var maxScaleOffset = intensity / 100.0f;
        _targetScale = 1.0f + bassEnergy * maxScaleOffset;

        if (_targetScale > _currentScale)
            _currentScale += (_targetScale - _currentScale) * 0.2f;
        else
            _currentScale += (_targetScale - _currentScale) * 0.05f;
    }

    protected void ApplyBreathingTransform(CanvasDrawingSession ds, Vector2 center, bool isEnabled, double weight = 1.0)
    {
        if (!isEnabled || weight <= 0 || _currentScale <= 1.0f) return;

        // 通过权重在“无呼吸”与“当前呼吸强度”之间插值，使行进入 / 离开播放状态时过渡平滑
        var scale = 1.0f + (_currentScale - 1.0f) * (float)Math.Clamp(weight, 0.0, 1.0);
        if (scale > 1.0f) ds.Transform = Matrix3x2.CreateScale(scale, center);
    }

    protected static void ResetTransform(CanvasDrawingSession ds, bool isEnabled)
    {
        if (isEnabled) ds.Transform = Matrix3x2.Identity;
    }
}
