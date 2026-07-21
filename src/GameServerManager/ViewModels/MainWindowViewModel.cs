using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace GameServerManager.ViewModels;

/// <summary>
/// MainWindow のナビゲーション管理 ViewModel
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _applicationTitle = "Game Server Manager";

    [ObservableProperty]
    private ObservableCollection<object> _navigationItems = new();

    [ObservableProperty]
    private ObservableCollection<object> _navigationFooter = new();
}
