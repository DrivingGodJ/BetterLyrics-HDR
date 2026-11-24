// https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance

using BetterLyrics.Core.Enums;
using BetterLyrics.Core.Helpers;
using BetterLyrics.Core.Implementations.Services;
using BetterLyrics.Core.Implementations.Services.FileSystemService;
using BetterLyrics.Core.Implementations.Services.GsmtcService;
using BetterLyrics.Core.Implementations.Services.LyricsSearchService;
using BetterLyrics.Core.Implementations.Services.PluginService;
using BetterLyrics.Core.Interfaces.Providers;
using BetterLyrics.Core.Interfaces.Services;
using BetterLyrics.Core.Models;
using BetterLyrics.Core.ViewModels;
using BetterLyrics.WinUI3.Logging;
using BetterLyrics.WinUI3.Providers;
using BetterLyrics.WinUI3.Services;
using BetterLyrics.WinUI3.UITest;
using BetterLyrics.WinUI3.Views;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Serilog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using WinRT;

namespace BetterLyrics.WinUI3;

public class Program
{
    private static readonly TaskCompletionSource<DispatcherQueue> ActivationDispatcher =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static ILogger<Program>? _logger;

    private static ServiceProvider? _serviceProvider;

    private static IntPtr redirectEventHandle = IntPtr.Zero;

    [STAThread]
    private static int Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();
        UiTestSession.Initialize(args);
        var isRedirect = DecideRedirection();

        if (!isRedirect)
        {
            Application.Start(p =>
            {
                var dispatcher = DispatcherQueue.GetForCurrentThread();
                var context = new DispatcherQueueSynchronizationContext(dispatcher);
                SynchronizationContext.SetSynchronizationContext(context);

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                PathHelper.EnsureDirectories();

                ConfigureServices();

                _logger = Ioc.Default.GetRequiredService<ILogger<Program>>();

                _logger.LogInformation(
                    "BetterLyrics starting. Version: {Version}; log level: {LogLevel}; process id: {ProcessId}.",
                    MetadataHelper.AppVersion, AppLogging.MinimumLevel, Environment.ProcessId);

                var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                _ = new App(() =>
                {
                    HandleActivation(activationArgs, true);
                    ActivationDispatcher.TrySetResult(dispatcher);
                });
            });

            // 正常关闭（非 ExitApp 路径）时释放容器；DisposeServices 可重复调用
            DisposeServices();
        }

        return 0;
    }

    private static bool DecideRedirection()
    {
        var isRedirect = false;
        var args = AppInstance.GetCurrent().GetActivatedEventArgs();
        var kind = args.Kind;

        // Run the UI test host as an independent instance so it does not get
        // redirected into a user instance that cannot accept test arguments.
        var instanceKey = UiTestSession.IsEnabled
            ? $"BetterLyrics.UiTest.{Environment.ProcessId}"
            : "BetterLyricsLite";

        var keyInstance = AppInstance.FindOrRegisterForKey(instanceKey);

        if (keyInstance.IsCurrent)
        {
            keyInstance.Activated += OnActivated;
        }
        else
        {
            isRedirect = true;
            if (kind != ExtendedActivationKind.StartupTask)
                RedirectActivationTo(args, keyInstance);
        }

        return isRedirect;
    }

    private static async void OnActivated(object? sender, AppActivationArguments args)
    {
        // A manual launch can arrive while the login instance is still initializing.
        var dispatcher = await ActivationDispatcher.Task;
        dispatcher.TryEnqueue(() => HandleActivation(args));
    }

    private static void HandleActivation(AppActivationArguments args, bool init = false)
    {
        var kind = args.Kind;
        _logger?.LogDebug("Activation received. Kind: {Kind}; initial: {IsInitial}.", kind, init);

        if (kind == ExtendedActivationKind.StartupTask)
        {
            _logger?.LogInformation("Started at Windows sign-in in the notification area.");
            return;
        }

        if (kind == ExtendedActivationKind.Protocol)
        {
            HandleProtocolActivation(args);
            return;
        }

        var windows = Ioc.Default.GetRequiredService<IWindowManagerProvider>();
        var profiles = Ioc.Default.GetRequiredService<ISettingsService>().AppSettings.WindowBoundsRecords;
        var profile = init ? null : windows.GetWindows<NowPlayingWindow>().FirstOrDefault()?.LyricsWindowStatus;
        profile ??= profiles.FirstOrDefault(candidate => candidate.IsDefault) ?? profiles.FirstOrDefault();
        if (profile != null)
        {
            windows.OpenOrShowWindow<NowPlayingWindow>(profile);
        }
    }

    private static void HandleProtocolActivation(AppActivationArguments args)
    {
        var windowManagerProvider = Ioc.Default.GetRequiredService<IWindowManagerProvider>();

        if (args.Data is IProtocolActivatedEventArgs protocolArgs)
        {
            _logger?.LogDebug("Protocol activation received: {Uri}.", protocolArgs.Uri);

            if (protocolArgs.Uri.Host == "settings")
            {
                var segments = protocolArgs.Uri.Segments.Select(s => s.Trim('/')).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                if (segments.Length > 0 && Enum.TryParse<SettingsSection>(segments[0], true, out var section) &&
                    section is SettingsSection.PlaybackLib or SettingsSection.LyricsWindowMgr)
                {
                    windowManagerProvider.OpenOrShowWindow<SettingsWindow>();
                    var settingsPageViewModel = Ioc.Default.GetRequiredService<SettingsPageViewModel>();

                    string? subsectionString = segments.Length > 1 ? segments[1] : null;
                    string? param = segments.Length > 2 ? segments[2] : null;

                    // Support both Tag/Param and Param/Tag formats
                    if (segments.Length > 2 && !TryParseSettingsSubsection(section, subsectionString, out _) && TryParseSettingsSubsection(section, param, out _))
                    {
                        (subsectionString, param) = (param, subsectionString);
                    }

                    if (!string.IsNullOrEmpty(subsectionString) && TryParseSettingsSubsection(section, subsectionString, out var subsection))
                    {
                        var searchItem = new SettingSearchItem
                        {
                            Section = section,
                            Subsection = subsection,
                            TargetParameter = param
                        };

                        // Parse guid if possible
                        if (!string.IsNullOrEmpty(param) && Guid.TryParse(param, out var guidParam))
                        {
                            searchItem.TargetParameter = guidParam;
                        }

                        settingsPageViewModel.NavigateToSettingSearchItem(searchItem);
                    }
                    else
                    {
                        settingsPageViewModel.NavigateToSection(section);
                    }
                }
            }
            else if (protocolArgs.Uri.Host == "stats")
            {
                windowManagerProvider.OpenOrShowWindow<StatsDashboardWindow>();
            }
        }
    }

    private static void ConfigureServices()
    {
        AppLogging.Initialize();

        Log.Information("Logging initialized. File: {LogFile}; minimum level: {MinimumLevel}.", PathHelper.LogFilePattern, AppLogging.MinimumLevel);

        var services = new ServiceCollection()
            // HTTP Client
            .AddHttpClient()
            .ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
                }))

            // 数据库服务和迁移
            .AddSingleton<IDatabaseService, DatabaseService>()
            .AddSingleton<IDatabaseMigrationService, DatabaseMigrationService>()
            .AddSingleton<IPlayHistoryService, PlayHistoryService>()
            .AddSingleton<IFileSystemService, FileSystemService>()
            .AddSingleton<ISongSearchMapService, SongSearchMapService>()
            .AddSingleton<IPlaybackStateService, PlaybackStateService>()

            // 日志
            .AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddSerilog();
            })

            // Services
            .AddSingleton<ISettingsService, SettingsService>()
            .AddSingleton<ISmtcService, SmtcService>()
            .AddSingleton<IGsmtcService, GsmtcService>()
            .AddSingleton<IBuiltInGsmtcService, BuiltInGsmtcService>()
            .AddSingleton<IAlbumArtSearchService, AlbumArtSearchService>()
            .AddSingleton<ILyricsSearchService, LyricsSearchService>()
            .AddSingleton<ILastFmService, LastFmService>()
            .AddSingleton<ILocalizationService, LocalizationService>()
            .AddSingleton<ILyricsCacheService, LyricsCacheService>()
            .AddSingleton<IPluginService, PluginService>()
            .AddSingleton<INavigationService, NavigationService>()
            .AddSingleton<IAppLifecycleService, AppLifecycleService>()
            .AddSingleton<IAudioMixerService, AudioMixerService>()
            .AddSingleton<IGlobalShortcutService, GlobalShortcutService>()
            .AddSingleton<IMonitorBindingService, MonitorBindingService>()
            .AddSingleton<IWallpaperEngineService, WallpaperEngineService>()
            .AddSingleton<ILyricsWindowVisibilityService, LyricsWindowVisibilityService>()

            // Providers
            .AddSingleton<IPasswordVaultProvider, PasswordVaultProvider>()
            .AddSingleton<IPlatformProvider, PlatformProvider>()
            .AddSingleton<IStringConverterProvider, StringConverterProvider>()
            .AddSingleton<ISystemUIProvider, SystemUIProvider>()
            .AddSingleton<IUniversalMemoryReaderProvider, UniversalMemoryReaderProvider>()
            .AddSingleton<IAppUIThreadProvider, AppUIThreadProvider>()
            .AddSingleton<IGlobalToastProvider, GlobalToastProvider>()
            .AddSingleton<IWindowManagerProvider, WindowManagerProvider>()
            .AddSingleton<IAddMediaSourceDialogProvider, MediaSourceDialogProvider>()
            .AddSingleton<ILastFmDialogProvider, LastFmDialogProvider>()
            .AddSingleton<IAssetReaderProvider, AssetReaderProvider>()
            .AddSingleton<IMediaManagerProvider, MediaManagerProvider>()
            .AddSingleton<ILauncherProvider, LauncherProvider>()
            .AddSingleton<IFilePickerProvider, FilePickerProvider>()
            .AddSingleton<IProgramProvider, ProgramProvider>()
            .AddSingleton<IMonitorProvider, MonitorProvider>()
            .AddSingleton<ITaskbarThumbnailProvider, TaskbarThumbnailProvider>()
            .AddSingleton<IGlobalShortcutProvider, GlobalShortcutProvider>()

            // ViewModels
            // 应用级单例：需要被视图之外的代码解析，或在窗口重建后保留状态
            .AddSingleton<SettingsPageViewModel>()
            .AddSingleton<SystemTrayViewModel>()

            // 视图级实例：每个 Window/Page/Control 生命周期内独立，随视图创建与释放
            .AddTransient<PlaybackSettingsControlViewModel>()
            .AddTransient<LyricsWindowManagerControlViewModel>()
            .AddTransient<WindowSettingsControlViewModel>()
            .AddTransient<LyricsWindowSwitchControlViewModel>()
            .AddTransient<LyricsWindowSwitchWindowViewModel>()
            .AddTransient<StatsDashboardControlViewModel>()
            .AddTransient<NowPlayingPageViewModel>()
            .AddTransient<NowPlayingBarViewModel>();

        _serviceProvider = services.BuildServiceProvider();
        Ioc.Default.ConfigureServices(_serviceProvider);

        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        AppLogging.ApplyLogLevel(settingsService.AppSettings.AdvancedSettings.WriteDebugLogs);
    }

    /// <summary>
    /// 释放 Ioc 容器及其持有的单例资源（数据库、文件监视器、全局快捷键、托盘等）。
    /// </summary>
    public static void DisposeServices()
    {
        var serviceProvider = _serviceProvider;
        _serviceProvider = null;

        serviceProvider?.Dispose();
        AppLogging.Shutdown();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateEvent(
        IntPtr lpEventAttributes, bool bManualReset,
        bool bInitialState, string lpName);

    [DllImport("kernel32.dll")]
    private static extern bool SetEvent(IntPtr hEvent);

    [DllImport("ole32.dll")]
    private static extern uint CoWaitForMultipleObjects(
        uint dwFlags, uint dwMilliseconds, ulong nHandles,
        IntPtr[] pHandles, out uint dwIndex);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    // Do the redirection on another thread, and use a non-blocking
    // wait method to wait for the redirection to complete.
    public static void RedirectActivationTo(AppActivationArguments args, AppInstance keyInstance)
    {
        redirectEventHandle = CreateEvent(IntPtr.Zero, true, false, null);
        _ = Task.Run(() =>
        {
            keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
            SetEvent(redirectEventHandle);
        });

        uint CWMO_DEFAULT = 0;
        var INFINITE = 0xFFFFFFFF;
        _ = CoWaitForMultipleObjects(
            CWMO_DEFAULT, INFINITE, 1,
            [redirectEventHandle], out var handleIndex);

        // Bring the window to the foreground
        var process = Process.GetProcessById((int)keyInstance.ProcessId);
        SetForegroundWindow(process.MainWindowHandle);
    }

    private static bool TryParseSettingsSubsection(SettingsSection section, string? value, out Enum? parsedSubsection)
    {
        parsedSubsection = section switch
        {
            SettingsSection.PlaybackLib when Enum.TryParse<PlaybackLibSettingsSection>(value, true, out var playback) &&
                                             Enum.IsDefined(playback) && playback != PlaybackLibSettingsSection.Integration => playback,
            SettingsSection.LyricsWindowMgr when Enum.TryParse<LyricsWindowManagerSettingsSection>(value, true, out var window) &&
                                                Enum.IsDefined(window) => window,
            _ => null
        };
        return parsedSubsection != null;
    }
}
