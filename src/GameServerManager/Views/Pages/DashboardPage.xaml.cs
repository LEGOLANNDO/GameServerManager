using System.Windows.Controls;
using GameServerManager.ViewModels;

namespace GameServerManager.Views.Pages;

/// <summary>
/// ダッシュボードページ - サーバー一覧とステータス表示
/// </summary>
public partial class DashboardPage : Page
{
    public DashboardPage(DashboardViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
