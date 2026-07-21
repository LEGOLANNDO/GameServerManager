using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using GameServerManager.Services;
using GameServerManager.ViewModels;
using GameServerManager.Views;
using GameServerManager.Views.Pages;
using Wpf.Ui;

namespace GameServerManager;

/// <summary>
/// App.xaml.cs - DI コンテナ設定、テーマ初期化、起動処理
/// </summary>
public partial class App : Application
{
    private static IHost? _host;

    /// <summary>
    /// DI サービスプロバイダーへのアクセス
    /// </summary>
    public static IServiceProvider Services => _host!.Services;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // === Services ===
                services.AddSingleton<CryptoService>();
                services.AddSingleton<ConfigService>();

                // === Process Management (Phase 2) ===
                services.AddSingleton<ProcessManagerService>();
                services.AddSingleton<ResourceMonitorService>();
                services.AddSingleton<ProcessMonitorService>();
                services.AddHostedService(sp => sp.GetRequiredService<ProcessMonitorService>());

                // === Discord & Notifications (Phase 3) ===
                services.AddSingleton<DiscordWebhookService>();

                // === Navigation (WPF-UI 4.x) ===
                services.AddSingleton<INavigationService, NavigationService>();

                // === ViewModels ===
                services.AddSingleton<MainWindowViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<SettingsViewModel>();

                // === Views ===
                services.AddSingleton<MainWindow>();
                services.AddTransient<DashboardPage>();
                services.AddTransient<SettingsPage>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host!.StartAsync();

        // MainWindow を表示
        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // ウィンドウ表示後にテーマを適用 (テーマブラシの確実な反映のため)
        var configService = Services.GetRequiredService<ConfigService>();
        var config = configService.LoadConfig();
        SettingsViewModel.ApplyTheme(config.AppSettings.Theme);

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            // 管理プロセスの参照を解放（プロセス自体は停止しない）
            var processManager = Services.GetRequiredService<ProcessManagerService>();
            processManager.ReleaseAll();

            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    /// <summary>
    /// 未処理例外のキャッチ（クラッシュ防止）
    /// </summary>
    private void App_OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // ログ出力（将来的にファイルロガーに置き換え）
        System.Diagnostics.Debug.WriteLine($"[UNHANDLED EXCEPTION] {e.Exception}");

        MessageBox.Show(
            $"予期しないエラーが発生しました。\n\n{e.Exception.Message}",
            "Game Server Manager - エラー",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
