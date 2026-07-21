using System.Windows.Controls;
using GameServerManager.ViewModels;

namespace GameServerManager.Views.Pages;

/// <summary>
/// 設定ページ - テーマ、Webhook、サーバー管理
/// </summary>
public partial class SettingsPage : Page
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
