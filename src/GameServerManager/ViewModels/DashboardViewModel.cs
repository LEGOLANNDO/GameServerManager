using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameServerManager.Models;
using GameServerManager.Services;
using GameServerManager.Views.Dialogs;

namespace GameServerManager.ViewModels;

/// <summary>
/// ダッシュボード: サーバー一覧・ステータス表示・プロセス制御
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly ProcessManagerService _processManager;
    private readonly ProcessMonitorService _processMonitor;
    private readonly ResourceMonitorService _resourceMonitor;
    private readonly DiscordWebhookService _discordService;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _scheduleTimer;

    [ObservableProperty]
    private ObservableCollection<ServerDisplayItem> _servers = new();

    public DashboardViewModel(
        ConfigService configService,
        ProcessManagerService processManager,
        ProcessMonitorService processMonitor,
        ResourceMonitorService resourceMonitor,
        DiscordWebhookService discordService)
    {
        _configService = configService;
        _processManager = processManager;
        _processMonitor = processMonitor;
        _resourceMonitor = resourceMonitor;
        _discordService = discordService;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _scheduleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _scheduleTimer.Tick += OnScheduleTimerTick;
        _scheduleTimer.Start();

        // 監視イベントの購読
        _processMonitor.StatusChanged += OnStatusChanged;
        _processMonitor.ServerCrashed += OnServerCrashed;
        _processMonitor.ResourceUpdated += OnResourceUpdated;

        LoadServers();
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadServers();
    }

    /// <summary>
    /// サーバーを起動 + Discord起動通知
    /// </summary>
    [RelayCommand]
    private async Task StartServerAsync(ServerDisplayItem? item)
    {
        if (item == null) return;

        var serverConfig = FindServerConfig(item.Id);
        if (serverConfig == null) return;

        var (success, pid) = _processManager.StartServer(serverConfig);
        if (success)
        {
            item.Status = ServerStatus.Running;
            item.StartedAt = DateTime.Now;
            _resourceMonitor.StartTracking(item.Id, pid);

            // Discord 起動通知 (非同期、UIをブロックしない)
            var webhookUrl = _discordService.ResolveWebhookUrl(serverConfig.EncryptedWebhook);
            if (!string.IsNullOrEmpty(webhookUrl))
            {
                _ = _discordService.SendStartNotificationAsync(item.Name, webhookUrl);
            }
        }
    }

    /// <summary>
    /// サーバーを停止 (理由入力ダイアログ付き) + Discord停止通知
    /// </summary>
    [RelayCommand]
    private async Task StopServerAsync(ServerDisplayItem? item)
    {
        if (item == null) return;

        // 理由入力ダイアログを表示
        var dialog = new ShutdownReasonDialog("停止", item.Name);
        var reason = await dialog.ShowAndGetResultAsync();

        // キャンセルされた場合は何もしない
        if (reason == null) return;

        item.Status = ServerStatus.Stopped;
        var serverConfig = FindServerConfig(item.Id);
        var success = await _processManager.StopServerAsync(item.Id);
        if (success)
        {
            _resourceMonitor.StopTracking(item.Id);
            item.CpuUsage = 0;
            item.MemoryUsageMb = 0;
            item.StartedAt = null;

            // Discord 停止通知
            if (serverConfig != null)
            {
                var webhookUrl = _discordService.ResolveWebhookUrl(serverConfig.EncryptedWebhook);
                if (!string.IsNullOrEmpty(webhookUrl))
                {
                    _ = _discordService.SendStopNotificationAsync(
                        item.Name, reason.ClassificationText, reason.Reason, webhookUrl);
                }
            }
        }
    }

    /// <summary>
    /// サーバーを再起動 (理由入力ダイアログ付き) + Discord再起動通知
    /// </summary>
    [RelayCommand]
    private async Task RestartServerAsync(ServerDisplayItem? item)
    {
        if (item == null) return;

        var serverConfig = FindServerConfig(item.Id);
        if (serverConfig == null) return;

        // 理由入力ダイアログを表示
        var dialog = new ShutdownReasonDialog("再起動", item.Name);
        var reason = await dialog.ShowAndGetResultAsync();

        // キャンセルされた場合は何もしない
        if (reason == null) return;

        item.Status = ServerStatus.Stopped;
        var (success, pid) = await _processManager.RestartServerAsync(serverConfig);
        if (success)
        {
            item.Status = ServerStatus.Running;
            item.StartedAt = DateTime.Now;
            _resourceMonitor.StartTracking(item.Id, pid);

            // Discord 再起動通知
            var webhookUrl = _discordService.ResolveWebhookUrl(serverConfig.EncryptedWebhook);
            if (!string.IsNullOrEmpty(webhookUrl))
            {
                _ = _discordService.SendRestartNotificationAsync(
                    item.Name, reason.ClassificationText, reason.Reason, webhookUrl);
                
                // 再起動後に「起動」通知も送信する
                _ = _discordService.SendStartNotificationAsync(item.Name, webhookUrl);
            }
        }
    }

    /// <summary>
    /// スケジュール設定ダイアログを開き、設定を保存する
    /// </summary>
    [RelayCommand]
    private async Task SetScheduleAsync(ServerDisplayItem? item)
    {
        if (item == null) return;

        var dialog = new GameServerManager.Views.Dialogs.ScheduleDialog(item.Name, item.ScheduledTime, item.ScheduledAction);
        var result = await dialog.ShowAndGetResultAsync();

        if (result != null)
        {
            if (result.IsCleared)
            {
                item.ScheduledTime = null;
                item.ScheduledAction = string.Empty;
            }
            else
            {
                item.ScheduledTime = result.ScheduledTime;
                item.ScheduledAction = result.ScheduledAction;
            }
        }
    }

    /// <summary>
    /// 10秒ごとのタイマーでスケジュールを監視し、実行する
    /// </summary>
    private async void OnScheduleTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var toExecute = Servers.Where(s => s.ScheduledTime.HasValue && s.ScheduledTime.Value <= now).ToList();

        foreach (var item in toExecute)
        {
            var action = item.ScheduledAction;
            
            // スケジュールをクリア
            item.ScheduledTime = null;
            item.ScheduledAction = string.Empty;

            if (action == "Restart")
            {
                // UI上では再起動ボタン押下と同じ扱いにするため、
                // 再起動理由ダイアログが出るのを防ぐには直接 ProcessManager を叩くか
                // 理由を「スケジュール」として処理を分ける必要がある。
                // 簡易的に直接再起動を実行する。
                var serverConfig = FindServerConfig(item.Id);
                if (serverConfig != null)
                {
                    item.Status = ServerStatus.Stopped;
                    var (success, pid) = await _processManager.RestartServerAsync(serverConfig);
                    if (success)
                    {
                        item.Status = ServerStatus.Running;
                        item.StartedAt = DateTime.Now;
                        _resourceMonitor.StartTracking(item.Id, pid);

                        var webhookUrl = _discordService.ResolveWebhookUrl(serverConfig.EncryptedWebhook);
                        if (!string.IsNullOrEmpty(webhookUrl))
                        {
                            await _discordService.SendRestartNotificationAsync(item.Name, "スケジュール", "予約された再起動", webhookUrl);
                            await _discordService.SendStartNotificationAsync(item.Name, webhookUrl);
                        }
                    }
                }
            }
            else if (action == "Stop")
            {
                var serverConfig = FindServerConfig(item.Id);
                if (serverConfig != null)
                {
                    var success = await _processManager.StopServerAsync(item.Id);
                    if (success)
                    {
                        item.Status = ServerStatus.Stopped;
                        item.CpuUsage = 0;
                        item.MemoryUsageMb = 0;
                        item.StartedAt = null;

                        var webhookUrl = _discordService.ResolveWebhookUrl(serverConfig.EncryptedWebhook);
                        if (!string.IsNullOrEmpty(webhookUrl))
                        {
                            await _discordService.SendStopNotificationAsync(item.Name, "スケジュール", "予約された停止", webhookUrl);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// サーバー一覧をConfigから読み込み、現在のプロセス状態を反映
    /// </summary>
    private void LoadServers()
    {
        var config = _configService.LoadConfig();
        Servers.Clear();

        // 稼働中のプロセスを検知して復旧する
        var recovered = _processManager.AttachRunningProcesses(config.Servers);
        foreach (var r in recovered)
        {
            _resourceMonitor.StartTracking(r.ServerId, r.Pid);
        }
        foreach (var server in config.Servers)
        {
            var isRunning = _processManager.IsProcessRunning(server.Id);
            var managed = _processManager.GetManagedProcess(server.Id);

            var displayItem = new ServerDisplayItem
            {
                Id = server.Id,
                Name = server.Name,
                Type = server.Type,
                ExecutablePath = server.ExecutablePath,
                Arguments = server.Arguments,
                WorkingDirectory = server.WorkingDirectory,
                Status = isRunning ? ServerStatus.Running : ServerStatus.Stopped,
                StartedAt = isRunning && managed != null ? managed.StartedAt : null,
                CpuUsage = 0,
                MemoryUsageMb = 0
            };

            // 稼働中ならリソース情報を取得
            if (isRunning && managed != null)
            {
                var (cpu, mem) = _resourceMonitor.GetResourceUsage(server.Id, managed.Pid);
                displayItem.CpuUsage = cpu;
                displayItem.MemoryUsageMb = mem;
            }

            Servers.Add(displayItem);
        }
    }

    /// <summary>
    /// ServerConfig をIDで検索
    /// </summary>
    private ServerConfig? FindServerConfig(string serverId)
    {
        var config = _configService.LoadConfig();
        return config.Servers.Find(s => s.Id == serverId);
    }

    // === 監視イベントハンドラ (UIスレッドにディスパッチ) ===

    private void OnStatusChanged(string serverId, ServerStatus status)
    {
        _dispatcher.BeginInvoke(() =>
        {
            var item = FindDisplayItem(serverId);
            if (item != null)
            {
                item.Status = status;
                if (status == ServerStatus.Stopped)
                {
                    item.CpuUsage = 0;
                    item.MemoryUsageMb = 0;
                    item.StartedAt = null;
                }
            }
        });
    }

    private void OnServerCrashed(string serverId, int? exitCode)
    {
        _dispatcher.BeginInvoke(() =>
        {
            var item = FindDisplayItem(serverId);
            if (item != null)
            {
                item.Status = ServerStatus.Error;
                item.CpuUsage = 0;
                item.MemoryUsageMb = 0;

                // Discord クラッシュ通知
                var serverConfig = FindServerConfig(serverId);
                if (serverConfig != null)
                {
                    var webhookUrl = _discordService.ResolveWebhookUrl(serverConfig.EncryptedWebhook);
                    if (!string.IsNullOrEmpty(webhookUrl))
                    {
                        _ = _discordService.SendCrashNotificationAsync(
                            item.Name, exitCode, webhookUrl);
                    }
                }
            }
        });
    }

    private void OnResourceUpdated(string serverId, double cpu, double memory)
    {
        _dispatcher.BeginInvoke(() =>
        {
            var item = FindDisplayItem(serverId);
            if (item != null)
            {
                item.CpuUsage = cpu;
                item.MemoryUsageMb = memory;
            }
        });
    }

    private ServerDisplayItem? FindDisplayItem(string serverId)
    {
        return Servers.FirstOrDefault(s => s.Id == serverId);
    }
}

/// <summary>
/// UI表示用のサーバー情報
/// </summary>
public partial class ServerDisplayItem : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _type = string.Empty;

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private ServerStatus _status = ServerStatus.Stopped;

    [ObservableProperty]
    private double _cpuUsage;

    [ObservableProperty]
    private double _memoryUsageMb;

    [ObservableProperty]
    private DateTime? _startedAt;

    [ObservableProperty]
    private DateTime? _scheduledTime;

    [ObservableProperty]
    private string _scheduledAction = string.Empty; // "Restart" or "Stop"

    public string ScheduleText
    {
        get
        {
            if (ScheduledTime == null) return string.Empty;
            var actionText = ScheduledAction == "Restart" ? "再起動" : "停止";
            return $"📅 {ScheduledTime.Value:MM/dd HH:mm} に{actionText}されます";
        }
    }

    /// <summary>
    /// ステータスに応じた色コード
    /// </summary>
    public string StatusColor => Status switch
    {
        ServerStatus.Running => "#2ECC71",
        ServerStatus.Error => "#E74C3C",
        _ => "#95A5A6"
    };

    /// <summary>
    /// ステータスの表示テキスト
    /// </summary>
    public string StatusText => Status switch
    {
        ServerStatus.Running => "稼働中",
        ServerStatus.Error => "異常",
        _ => "停止中"
    };

    /// <summary>
    /// 稼働中かどうか (ボタン制御用)
    /// </summary>
    public bool IsRunning => Status == ServerStatus.Running;

    /// <summary>
    /// 停止中かどうか (ボタン制御用)
    /// </summary>
    public bool IsStopped => Status != ServerStatus.Running;

    /// <summary>
    /// 稼働時間の表示テキスト
    /// </summary>
    public string UptimeText
    {
        get
        {
            if (StartedAt == null || Status != ServerStatus.Running)
                return "--";
            var elapsed = DateTime.Now - StartedAt.Value;
            if (elapsed.TotalDays >= 1)
                return $"{(int)elapsed.TotalDays}日 {elapsed.Hours}時間";
            if (elapsed.TotalHours >= 1)
                return $"{(int)elapsed.TotalHours}時間 {elapsed.Minutes}分";
            return $"{(int)elapsed.TotalMinutes}分";
        }
    }

    partial void OnStatusChanged(ServerStatus value)
    {
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(UptimeText));
    }

    partial void OnStartedAtChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(UptimeText));
    }

    partial void OnScheduledTimeChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(ScheduleText));
    }

    partial void OnScheduledActionChanged(string value)
    {
        OnPropertyChanged(nameof(ScheduleText));
    }
}

public enum ServerStatus
{
    Stopped,
    Running,
    Error
}
