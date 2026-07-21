using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameServerManager.Models;
using GameServerManager.Services;
using Wpf.Ui.Appearance;

namespace GameServerManager.ViewModels;

/// <summary>
/// 設定ページ ViewModel: テーマ、Webhook、サーバー管理
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly DiscordWebhookService _discordService;
    private AppConfig _config;

    [ObservableProperty]
    private string _currentTheme = "system";

    [ObservableProperty]
    private string _globalWebhookUrl = string.Empty;

    [ObservableProperty]
    private int _pollingInterval = 3;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _webhookTestResult = string.Empty;

    [ObservableProperty]
    private bool _isTestingWebhook;

    // サーバー追加/編集用フィールド
    [ObservableProperty]
    private string _newServerName = string.Empty;

    [ObservableProperty]
    private string _newServerType = "exe";

    [ObservableProperty]
    private string _newServerPath = string.Empty;

    [ObservableProperty]
    private string _newServerArguments = string.Empty;

    [ObservableProperty]
    private string _newServerWorkingDir = string.Empty;

    [ObservableProperty]
    private string _newServerWebhook = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ServerConfig> _servers = new();

    [ObservableProperty]
    private ServerConfig? _selectedServer;

    public List<string> AvailableThemes { get; } = new() { "system", "dark", "light" };
    public List<string> AvailableServerTypes { get; } = new() { "exe", "bat", "cmd", "ps1", "jar", "py", "sh", "docker", "custom" };

    public SettingsViewModel(ConfigService configService, DiscordWebhookService discordService)
    {
        _configService = configService;
        _discordService = discordService;
        _config = _configService.LoadConfig();
        LoadSettings();
    }

    private void LoadSettings()
    {
        CurrentTheme = _config.AppSettings.Theme;
        GlobalWebhookUrl = _configService.DecryptWebhookUrl(_config.AppSettings.EncryptedGlobalWebhook);
        PollingInterval = _config.AppSettings.PollingIntervalSeconds;

        Servers.Clear();
        foreach (var server in _config.Servers)
        {
            Servers.Add(server);
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _config.AppSettings.Theme = CurrentTheme;
        _config.AppSettings.EncryptedGlobalWebhook = _configService.EncryptWebhookUrl(GlobalWebhookUrl);
        _config.AppSettings.PollingIntervalSeconds = PollingInterval;
        _config.Servers = new List<ServerConfig>(Servers);

        _configService.SaveConfig(_config);

        // テーマを適用
        ApplyTheme(CurrentTheme);

        StatusMessage = "✅ 設定を保存しました";
    }

    /// <summary>
    /// Webhook テスト送信 (§4.2)
    /// </summary>
    [RelayCommand]
    private async Task TestWebhookAsync()
    {
        if (string.IsNullOrWhiteSpace(GlobalWebhookUrl))
        {
            WebhookTestResult = "⚠️ Webhook URLを入力してください";
            return;
        }

        IsTestingWebhook = true;
        WebhookTestResult = "送信中...";

        try
        {
            var success = await _discordService.SendTestNotificationAsync(GlobalWebhookUrl);
            WebhookTestResult = success
                ? "✅ テスト通知を送信しました！Discordを確認してください"
                : "❌ 送信に失敗しました。URLが正しいか確認してください";
        }
        catch (Exception)
        {
            WebhookTestResult = "❌ 送信エラーが発生しました。ネットワーク接続を確認してください";
        }
        finally
        {
            IsTestingWebhook = false;
        }
    }

    [RelayCommand]
    private void AddServer()
    {
        if (string.IsNullOrWhiteSpace(NewServerName))
        {
            StatusMessage = "⚠️ サーバー名を入力してください";
            return;
        }

        var newServer = new ServerConfig
        {
            Id = $"srv_{Guid.NewGuid().ToString("N")[..8]}",
            Name = NewServerName,
            Type = NewServerType,
            ExecutablePath = NewServerPath,
            Arguments = NewServerArguments,
            WorkingDirectory = NewServerWorkingDir,
            EncryptedWebhook = _configService.EncryptWebhookUrl(NewServerWebhook)
        };

        Servers.Add(newServer);

        // フィールドをクリア
        NewServerName = string.Empty;
        NewServerType = "exe";
        NewServerPath = string.Empty;
        NewServerArguments = string.Empty;
        NewServerWorkingDir = string.Empty;
        NewServerWebhook = string.Empty;

        StatusMessage = $"✅ サーバー '{newServer.Name}' を追加しました";
    }

    [RelayCommand]
    private void RemoveServer(ServerConfig? server)
    {
        if (server == null) return;
        Servers.Remove(server);
        StatusMessage = $"🗑️ サーバー '{server.Name}' を削除しました";
    }

    public static void ApplyTheme(string theme)
    {
        ApplicationTheme appTheme;
        if (theme == "dark")
            appTheme = ApplicationTheme.Dark;
        else if (theme == "light")
            appTheme = ApplicationTheme.Light;
        else
        {
            var sysTheme = ApplicationThemeManager.GetSystemTheme();
            appTheme = sysTheme == SystemTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
        }

        ApplicationThemeManager.Apply(appTheme);
    }
}
