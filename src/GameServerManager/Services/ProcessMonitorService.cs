using System.Diagnostics;
using GameServerManager.ViewModels;
using Microsoft.Extensions.Hosting;

namespace GameServerManager.Services;

/// <summary>
/// バックグラウンドプロセス死活監視サービス (IHostedService)
/// config の polling_interval_seconds 間隔でプロセスの生存を確認し、
/// クラッシュ（意図しない終了）を検知してイベントを発火する
/// </summary>
public class ProcessMonitorService : IHostedService, IDisposable
{
    private readonly ProcessManagerService _processManager;
    private readonly ResourceMonitorService _resourceMonitor;
    private readonly ConfigService _configService;
    private Timer? _timer;

    /// <summary>
    /// ステータス変更通知 (serverId, newStatus)
    /// </summary>
    public event Action<string, ServerStatus>? StatusChanged;

    /// <summary>
    /// クラッシュ検知通知 (serverId, exitCode)
    /// </summary>
    public event Action<string, int?>? ServerCrashed;

    /// <summary>
    /// リソース使用量更新通知 (serverId, cpuPercent, memoryMb)
    /// </summary>
    public event Action<string, double, double>? ResourceUpdated;

    /// <summary>
    /// 前回のステータスを保持（クラッシュ検知用）
    /// </summary>
    private readonly Dictionary<string, ServerStatus> _previousStatuses = new();

    public ProcessMonitorService(
        ProcessManagerService processManager,
        ResourceMonitorService resourceMonitor,
        ConfigService configService)
    {
        _processManager = processManager;
        _resourceMonitor = resourceMonitor;
        _configService = configService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var config = _configService.LoadConfig();
        int intervalMs = config.AppSettings.PollingIntervalSeconds * 1000;

        // 最小1秒、最大30秒
        intervalMs = Math.Clamp(intervalMs, 1000, 30000);

        _timer = new Timer(
            callback: _ => PollProcesses(),
            state: null,
            dueTime: TimeSpan.FromSeconds(2), // 起動後2秒で最初のポーリング
            period: TimeSpan.FromMilliseconds(intervalMs));

        Debug.WriteLine($"[ProcessMonitor] Started with {intervalMs}ms interval");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        Debug.WriteLine("[ProcessMonitor] Stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// ポーリング間隔を更新（設定変更時に呼ぶ）
    /// </summary>
    public void UpdatePollingInterval(int seconds)
    {
        int intervalMs = Math.Clamp(seconds * 1000, 1000, 30000);
        _timer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(intervalMs));
        Debug.WriteLine($"[ProcessMonitor] Interval updated to {intervalMs}ms");
    }

    /// <summary>
    /// 全管理プロセスの死活チェックとリソース更新
    /// </summary>
    private void PollProcesses()
    {
        try
        {
            var serverIds = _processManager.GetAllManagedServerIds();

            foreach (var serverId in serverIds)
            {
                var managed = _processManager.GetManagedProcess(serverId);
                if (managed == null) continue;

                bool isAlive = managed.IsAlive;
                var currentStatus = isAlive ? ServerStatus.Running : ServerStatus.Stopped;

                // クラッシュ検知: 前回 Running → 今回 Stopped
                if (_previousStatuses.TryGetValue(serverId, out var previousStatus))
                {
                    if (previousStatus == ServerStatus.Running && currentStatus == ServerStatus.Stopped)
                    {
                        // 終了コードの取得
                        int? exitCode = null;
                        try
                        {
                            if (managed.Process != null && managed.Process.HasExited)
                            {
                                exitCode = managed.Process.ExitCode;
                            }
                        }
                        catch { }

                        managed.ExitCode = exitCode;

                        // クラッシュイベント発火 (意図しない終了の場合のみ)
                        if (!managed.IsIntentionalShutdown)
                        {
                            ServerCrashed?.Invoke(serverId, exitCode);
                        }
                        currentStatus = ServerStatus.Error;
                        
                        // リソース追跡停止
                        _resourceMonitor.StopTracking(serverId);
                    }
                }

                // ステータス更新通知
                if (!_previousStatuses.TryGetValue(serverId, out var prev) || prev != currentStatus)
                {
                    StatusChanged?.Invoke(serverId, currentStatus);
                }

                _previousStatuses[serverId] = currentStatus;

                // リソース使用量の更新（稼働中のみ）
                if (isAlive)
                {
                    var (cpu, memory) = _resourceMonitor.GetResourceUsage(serverId, managed.Pid);
                    ResourceUpdated?.Invoke(serverId, cpu, memory);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessMonitor] Error during polling: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
