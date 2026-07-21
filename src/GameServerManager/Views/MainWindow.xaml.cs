using System.ComponentModel;
using System.Windows;
using GameServerManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace GameServerManager.Views;

/// <summary>
/// MainWindow - FluentWindow ベースのメインウィンドウ
/// タスクトレイ常駐とナビゲーション管理を担当
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _viewModel;
    private bool _isExplicitExit = false;

    public MainWindow(
        MainWindowViewModel viewModel,
        IServiceProvider serviceProvider)
    {
        _viewModel = viewModel;

        DataContext = _viewModel;
        InitializeComponent();

        // NavigationView にサービスプロバイダーを設定（ページのDI解決用）
        NavigationView.SetServiceProvider(serviceProvider);

        // 初期ページに遷移
        Loaded += (_, _) =>
        {
            NavigationView.Navigate(typeof(Pages.DashboardPage));
        };
    }

    /// <summary>
    /// ウィンドウ閉じる → タスクトレイに格納（明示的終了以外）
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            e.Cancel = true;
            this.Hide();
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// トレイアイコンダブルクリック → ウィンドウ復帰
    /// </summary>
    private void TrayIcon_OnLeftDoubleClick(object sender, RoutedEventArgs e)
    {
        ShowAndActivateWindow();
    }

    /// <summary>
    /// トレイメニュー「表示」クリック
    /// </summary>
    private void ShowWindow_Click(object sender, RoutedEventArgs e)
    {
        ShowAndActivateWindow();
    }

    /// <summary>
    /// トレイメニュー「終了」クリック → アプリケーション終了
    /// </summary>
    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        _isExplicitExit = true;
        TrayIcon.Dispose();
        Application.Current.Shutdown();
    }

    /// <summary>
    /// ウィンドウを表示してアクティブにする
    /// </summary>
    private void ShowAndActivateWindow()
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
        this.Focus();
    }
}
