using BetterLyrics.Core.Enums;
using BetterLyrics.Core.Helpers;
using BetterLyrics.Core.Interfaces.Providers;
using BetterLyrics.Core.Interfaces.Services;
using BetterLyrics.Core.Models.Settings;
using BetterLyrics.WinUI3.UITest;
using BetterLyrics.WinUI3.Views;
using CommunityToolkit.Mvvm.DependencyInjection;
using LiteDB;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using WinUIEx;
using UnhandledExceptionEventArgs = Microsoft.UI.Xaml.UnhandledExceptionEventArgs;

namespace BetterLyrics.WinUI3;

public partial class App : Application
{
    private readonly ILogger<App> _logger;
    private readonly Action _handleInitialActivation;
    private Window? m_window;

    public App(Action handleInitialActivation)
    {
        _handleInitialActivation = handleInitialActivation;
        InitializeComponent();

        ATL.Settings.NullAbsentValues = true;
        BsonMapper.Global.EmptyStringToNull = false;

        _logger = Ioc.Default.GetRequiredService<ILogger<App>>();

        _logger.LogInformation("App started");

        try
        {
            var xamlAssemblyPath = typeof(Microsoft.UI.Xaml.Application).Assembly.Location;
            _logger.LogInformation("WinUI runtime: {FileVersion} ({Path})",
                System.Diagnostics.FileVersionInfo.GetVersionInfo(xamlAssemblyPath).FileVersion,
                xamlAssemblyPath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to log the WinUI runtime version.");
        }

        // 注册全局异常捕获
        UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    public new static App Current => (App)Application.Current;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            // 应用增强动效/全局字体设置项
            var settingsService = Ioc.Default.GetRequiredService<ISettingsService>();
            _logger.LogDebug("Applying global appearance settings. Animations: {Animations}; font: {FontFamily}.",
                settingsService.AppSettings.GeneralSettings.EnhanceControlInteractiveAnimations,
                settingsService.AppSettings.GeneralSettings.GlobalFontFamily);
            UpdateGlobalStyles(settingsService.AppSettings.GeneralSettings.EnhanceControlInteractiveAnimations);
            UpdateGlobalFontFamily(settingsService.AppSettings.GeneralSettings.GlobalFontFamily);

            var windowManagerProvider = Ioc.Default.GetRequiredService<IWindowManagerProvider>();
            var appUiThreadProvider = Ioc.Default.GetRequiredService<IAppUIThreadProvider>();

            // 初始化 IAppUIThreadProvider
            m_window = windowManagerProvider.OpenOrShowWindow<SystemTrayWindow>();
            appUiThreadProvider.Initialize(m_window.DispatcherQueue);

            var globalToastProvider = Ioc.Default.GetRequiredService<IGlobalToastProvider>();
            globalToastProvider.Initialize();

            // 初始化 IMonitorBindingService：把旧版保存的 \\.\DISPLAYn 迁移为稳定的显示器 device path
            _ = Ioc.Default.GetRequiredService<IMonitorBindingService>();

            _ = LaunchAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "OnLaunched: ");
            System.Windows.Forms.MessageBox.Show(ex.Message, "OnLaunched: Fatal error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }

    private async Task LaunchAsync()
    {
        try
        {
            await InitAppServicesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LaunchAsync: ");
            System.Windows.Forms.MessageBox.Show(ex.Message, "LaunchAsync: Fatal error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            Environment.Exit(1);
        }
        finally
        {
            HandleNormalLaunch();
        }
    }

    private void HandleNormalLaunch()
    {
        try
        {
            if (UiTestSession.IsEnabled)
            {
                _ = WindowGcTestRunner.RunAsync();
                return;
            }

            _handleInitialActivation();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleNormalLaunch: ");
            System.Windows.Forms.MessageBox.Show(ex.Message, "HandleNormalLaunch: Fatal error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }

    private static async Task InitAppServicesAsync()
    {
        Ioc.Default.GetRequiredService<ILyricsWindowVisibilityService>().Initialize();

        var settingsService = Ioc.Default.GetRequiredService<ISettingsService>();

        // 迁移逻辑
        var migrationService = Ioc.Default.GetRequiredService<IDatabaseMigrationService>();
        await migrationService.MigrateAllAsync();

        var songSearchMapService = Ioc.Default.GetRequiredService<ISongSearchMapService>();
        var obsoleteSongSearchMap = settingsService.AppSettings.MappedSongSearchQueries;
        if (obsoleteSongSearchMap.Count > 0)
        {
            foreach (var item in obsoleteSongSearchMap)
            {
                await songSearchMapService.SaveMappingAsync(item);
            }

            obsoleteSongSearchMap.Clear();
        }

        Ioc.Default.GetRequiredService<ILogger<App>>().LogInformation("Application services initialized.");
    }

    private static void UpdateGlobalStyles(bool useCustom)
    {
        var mergedDicts = Application.Current.Resources.MergedDictionaries;

        var fluentDict = mergedDicts.FirstOrDefault(d =>
            d.Source != null && d.Source.OriginalString.Contains("FluentStyles.xaml"));
        var defaultDict = mergedDicts.FirstOrDefault(d =>
            d.Source != null && d.Source.OriginalString.Contains("DefaultStyles.xaml"));

        if (useCustom)
        {
            if (fluentDict == null)
                mergedDicts.Add(new ResourceDictionary
                { Source = new Uri("ms-appx:///Themes/FluentStyles.xaml") });

            if (defaultDict != null) mergedDicts.Remove(defaultDict);
        }
        else
        {
            if (defaultDict == null)
                mergedDicts.Add(new ResourceDictionary
                { Source = new Uri("ms-appx:///Themes/DefaultStyles.xaml") });

            if (fluentDict != null) mergedDicts.Remove(fluentDict);
        }
    }

    private static void UpdateGlobalFontFamily(string fontFamily)
    {
        FontFamily targetFontFamily = FontFamily.XamlAutoFontFamily;
        if (!string.IsNullOrEmpty(fontFamily))
        {
            targetFontFamily = new FontFamily(fontFamily);
        }
        Application.Current.Resources["ContentControlThemeFontFamily"] = targetFontFamily;
        Application.Current.Resources["MTCMediaFontFamily"] = targetFontFamily;
        Application.Current.Resources["PivotHeaderItemFontFamily"] = targetFontFamily;
        Application.Current.Resources["PivotTitleFontFamily"] = targetFontFamily;
        Application.Current.Resources["KeyTipFontFamily"] = targetFontFamily;
    }

    private void App_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "App_UnhandledException");
        e.Handled = true;
    }

    private void CurrentDomain_FirstChanceException(object? sender,
        FirstChanceExceptionEventArgs e)
    {
        // FirstChance 异常非常多（比如内部 try-catch 也会触发），通常建议只在 Debug 模式记录，或者过滤特定类型
        // _logger.LogError(e.Exception, "CurrentDomain_FirstChanceException"); 
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        _logger.LogError(e.ExceptionObject.ToString(), "CurrentDomain_UnhandledException");
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "TaskScheduler_UnobservedTaskException");
    }
}
