using BetterLyrics.Core.Interfaces.Providers;
using BetterLyrics.Core.Interfaces.Services;
using BetterLyrics.Core.Interfaces.ViewModels;
using BetterLyrics.Core.Models;
using BetterLyrics.Core.Models.Domain;
using BetterLyrics.Core.Models.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace BetterLyrics.Core.ViewModels;

/// <summary>
/// Transient ViewModel
/// </summary>
public partial class WindowSettingsControlViewModel : ObservableObject, ITransientViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IMonitorProvider _monitorProvider;
    private readonly IAppUIThreadProvider _appUiThreadProvider;

    private bool _isAttached;
    private LyricsWindowStatus? _boundStatus;
    private bool _isStatusSubscribed;

    public WindowSettingsControlViewModel(ISettingsService settingsService,
        IMonitorProvider monitorProvider, IAppUIThreadProvider appUiThreadProvider)
    {
        _settingsService = settingsService;
        _monitorProvider = monitorProvider;
        _appUiThreadProvider = appUiThreadProvider;

        ReloadMonitors(force: false);
    }

    [ObservableProperty] public partial LyricsWindowStatus? LyricsWindowStatus { get; set; }

    [ObservableProperty] public partial bool IsManaged { get; set; }

    public ObservableCollection<MonitorInfo> Monitors { get; } = [];

    public void Attach()
    {
        if (_isAttached) return;

        _monitorProvider.MonitorsChanged += MonitorProvider_MonitorsChanged;
        SubscribeStatus();
        _isAttached = true;
    }

    public void Detach()
    {
        if (!_isAttached) return;

        _monitorProvider.MonitorsChanged -= MonitorProvider_MonitorsChanged;
        UnsubscribeStatus();
        _isAttached = false;
    }

    /// <summary>
    /// 绑定窗口配置并监听显示器位置变化。
    /// </summary>
    public void SetLyricsWindowStatus(LyricsWindowStatus? status)
    {
        UnsubscribeStatus();

        LyricsWindowStatus = status;
        _boundStatus = status;
        SubscribeStatus();

        IsManaged = status != null && _settingsService.AppSettings.WindowBoundsRecords.Contains(status);
    }

    private void SubscribeStatus()
    {
        if (_isStatusSubscribed || _boundStatus is null) return;

        _boundStatus.PropertyChanged += BoundStatus_PropertyChanged;
        _isStatusSubscribed = true;
    }

    private void UnsubscribeStatus()
    {
        if (!_isStatusSubscribed || _boundStatus is null) return;

        _boundStatus.PropertyChanged -= BoundStatus_PropertyChanged;
        _isStatusSubscribed = false;
    }

    private void BoundStatus_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LyricsWindowStatus.MonitorDeviceName)) return;

        var status = _boundStatus;
        if (status is null) return;

        // The combo box stores the GDI name because it is what the Win32 window APIs use.
        // Keep the persisted identity in sync with the selected monitor.
        var monitor = _monitorProvider.ResolveMonitor(null, null, status.MonitorDeviceName);
        if (monitor is null) return;

        if (!string.IsNullOrEmpty(monitor.DevicePath)) status.MonitorDevicePath = monitor.DevicePath;
        if (!string.IsNullOrEmpty(monitor.EdidId)) status.MonitorEdid = monitor.EdidId;
        status.MonitorBounds = _monitorProvider.GetMonitorRectFromDeviceName(monitor.GdiDeviceName);
    }

    private void MonitorProvider_MonitorsChanged(object? sender, EventArgs e)
    {
        // 等待系统显示拓扑更新完成
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            _appUiThreadProvider.Execute(() => ReloadMonitors(force: true));
        });
    }

    public void RefreshMonitors() => ReloadMonitors(force: true);

    private void ReloadMonitors(bool force)
    {
        var monitors = force ? _monitorProvider.RefreshMonitors() : _monitorProvider.GetMonitors();

        // Avoid clearing the collection when nothing changed: clearing it makes the ComboBox
        // drop its selection even though the underlying binding is still valid.
        if (Monitors.Count == monitors.Count)
        {
            var isSame = true;
            for (var i = 0; i < monitors.Count; i++)
            {
                if (!string.Equals(Monitors[i].DevicePath, monitors[i].DevicePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    isSame = false;
                    break;
                }
            }

            if (isSame) return;
        }

        Monitors.Clear();
        foreach (var monitor in monitors)
        {
            Monitors.Add(monitor);
        }

        // Do not rewrite the binding here. A monitor can be temporarily unavailable (for
        // example when the user switches to "PC screen only"), and silently rebinding the
        // window to the first monitor would destroy the user's configuration.
    }
}
