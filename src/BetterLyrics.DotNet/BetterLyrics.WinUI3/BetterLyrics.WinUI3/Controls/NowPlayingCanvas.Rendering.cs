using BetterLyrics.Core.Enums;
using BetterLyrics.Core.Models.Settings;
using BetterLyrics.WinUI3.Providers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Threading;

namespace BetterLyrics.WinUI3.Controls;

public sealed partial class NowPlayingCanvas
{
    private DispatcherQueueTimer? _settleTimer;
    private bool _isWindowVisible = true;
    private volatile bool _canRender;

    internal void SetWindowVisibility(bool visible)
    {
        _isWindowVisible = visible;
        RefreshRendering();
    }

    private void ObserveRenderSettings(LyricsWindowStatus? status, bool subscribe)
    {
        if (status == null) return;

        foreach (var settings in new INotifyPropertyChanged[]
                 {
                     status, status.LyricsBackgroundSettings, status.LyricsEffectSettings,
                     status.LyricsStyleSettings, status.AlbumArtLayoutSettings
                 })
        {
            if (subscribe)
                settings.PropertyChanged += OnRenderSettingChanged;
            else
                settings.PropertyChanged -= OnRenderSettingChanged;
        }
    }

    private void OnRenderSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess)
            RefreshRendering();
        else
            DispatcherQueue.TryEnqueue(RefreshRendering);
    }

    private void RefreshRendering()
    {
        if (Canvas == null || _settleTimer == null) return;

        var status = _lyricsWindowStatus;
        var canRender = _isWindowVisible && _monitorProvider.IsDisplayOn &&
                        status is { WindowState: LyricsWindowState.Opened, IsMinimized: false };
        var wasVisible = _canRender;
        _canRender = canRender;

        var isPlaying = _gsmtcService.CurrentIsPlaying;
        var background = status?.LyricsBackgroundSettings;
        SetSpectrumCapture(canRender && isPlaying && background != null &&
                           (background.IsSpectrumOverlayEnabled ||
                            background.IsCoverOverlayEnabled && background.IsCoverOverlayBrethingEffectEnabled ||
                            background.IsFluidOverlayEnabled && background.IsFluidOverlayBrethingEffectEnabled ||
                            status!.LyricsEffectSettings.IsLyricsBrethingEffectEnabled));

        if (!canRender)
        {
            _settleTimer.Stop();
            Canvas.Paused = true;
            if (wasVisible) _hdrPresenter?.Reset();
            return;
        }

        if (!wasVisible) _songPosition = _gsmtcService.CurrentPosition;
        RequestFrame();
    }

    private void RequestFrame()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(RequestFrame);
            return;
        }

        if (!_canRender || Canvas == null || _settleTimer == null) return;

        if (!_gsmtcService.CurrentIsPlaying)
        {
            _settleTimer.Stop();
            _settleTimer.Start();
        }

        if (Canvas.Paused) Canvas.Paused = false;
    }

    private void OnRenderSettled(DispatcherQueueTimer sender, object args)
    {
        if (Canvas != null && !_gsmtcService.CurrentIsPlaying)
            Canvas.Paused = true;
    }

    private void SetSpectrumCapture(bool capture)
    {
        if (!capture)
        {
            // Release the audio device watcher and FFT buffers too.
            Interlocked.Exchange(ref _spectrumAnalyzer, null)?.Dispose();
        }
        else if (_spectrumAnalyzer == null && _lyricsWindowStatus is { } status)
        {
            var background = status.LyricsBackgroundSettings;
            var spectrum = new SpectrumAnalyzerProvider
            {
                BarCount = background.SpectrumCount,
                Sensitivity = background.SpectrumSensitivity,
                DelayMs = background.SpectrumDelayMs
            };
            spectrum.StartCapture();
            Volatile.Write(ref _spectrumAnalyzer, spectrum);
        }
    }
}
