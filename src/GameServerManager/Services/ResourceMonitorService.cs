using System.Diagnostics;
using System.Collections.Concurrent;

namespace GameServerManager.Services;

/// <summary>
/// CPU使用率・メモリ使用量の取得サービス
/// PIDベースでプロセスのリソース使用状況をリアルタイム計測
/// </summary>
public class ResourceMonitorService
{
    /// <summary>
    /// CPU計測用: 前回の計測時刻とプロセッサ時間を保持
    /// </summary>
    private readonly ConcurrentDictionary<string, (DateTime Timestamp, TimeSpan ProcessorTime)> _cpuSnapshots = new();

    /// <summary>
    /// 指定サーバーのリソース使用量を取得
    /// </summary>
    /// <param name="serverId">サーバーID (追跡キー)</param>
    /// <param name="pid">プロセスID</param>
    /// <returns>(CPU使用率%, メモリ使用量MB)</returns>
    public (double CpuPercent, double MemoryMb) GetResourceUsage(string serverId, int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            
            double memoryMb = process.WorkingSet64 / (1024.0 * 1024.0);
            double cpuPercent = CalculateCpuUsage(serverId, process);

            return (cpuPercent, memoryMb);
        }
        catch (ArgumentException)
        {
            // プロセスが存在しない
            _cpuSnapshots.TryRemove(serverId, out _);
            return (0, 0);
        }
        catch (InvalidOperationException)
        {
            // プロセスが終了済み
            _cpuSnapshots.TryRemove(serverId, out _);
            return (0, 0);
        }
    }

    /// <summary>
    /// CPU使用率の追跡を開始（初回スナップショット取得）
    /// </summary>
    public void StartTracking(string serverId, int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            _cpuSnapshots[serverId] = (DateTime.UtcNow, process.TotalProcessorTime);
        }
        catch
        {
            // プロセスが見つからない場合は無視
        }
    }

    /// <summary>
    /// CPU使用率の追跡を停止
    /// </summary>
    public void StopTracking(string serverId)
    {
        _cpuSnapshots.TryRemove(serverId, out _);
    }

    /// <summary>
    /// CPU使用率を差分計測で算出
    /// TotalProcessorTime の差分 ÷ 経過時間 ÷ 論理プロセッサ数 × 100
    /// </summary>
    private double CalculateCpuUsage(string serverId, Process process)
    {
        var currentTime = DateTime.UtcNow;
        var currentCpuTime = process.TotalProcessorTime;

        if (!_cpuSnapshots.TryGetValue(serverId, out var snapshot))
        {
            // 初回: スナップショットを保存して 0% を返す
            _cpuSnapshots[serverId] = (currentTime, currentCpuTime);
            return 0;
        }

        var timeDiff = (currentTime - snapshot.Timestamp).TotalMilliseconds;
        if (timeDiff < 100) // 計測間隔が短すぎる場合
        {
            return 0;
        }

        var cpuDiff = (currentCpuTime - snapshot.ProcessorTime).TotalMilliseconds;
        int processorCount = Environment.ProcessorCount;

        // スナップショットを更新
        _cpuSnapshots[serverId] = (currentTime, currentCpuTime);

        // CPU使用率 = CPU時間差分 / (経過時間 × プロセッサ数) × 100
        double cpuPercent = (cpuDiff / (timeDiff * processorCount)) * 100.0;

        // 0〜100 の範囲にクランプ
        return Math.Clamp(cpuPercent, 0, 100);
    }
}
