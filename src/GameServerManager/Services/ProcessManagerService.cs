using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using GameServerManager.Models;

namespace GameServerManager.Services;

/// <summary>
/// 汎用プロセス起動・停止・再起動サービス
/// 仕様書 §3.1 の全形式に対応:
/// exe, bat, cmd, ps1, jar, py, sh, docker, custom
/// </summary>
public class ProcessManagerService
{
    /// <summary>
    /// サーバーID → 管理対象プロセス のマッピング
    /// </summary>
    private readonly ConcurrentDictionary<string, ManagedProcess> _managedProcesses = new();

    /// <summary>
    /// プロセス起動イベント (serverId, pid)
    /// </summary>
    public event Action<string, int>? ProcessStarted;

    /// <summary>
    /// プロセス停止イベント (serverId, exitCode)
    /// </summary>
    public event Action<string, int?>? ProcessStopped;

    /// <summary>
    /// サーバーのプロセスを起動する
    /// </summary>
    /// <param name="server">サーバー設定</param>
    /// <returns>(成功フラグ, PID)</returns>
    public (bool Success, int Pid) StartServer(ServerConfig server)
    {
        // 既に稼働中の場合は起動しない
        if (IsProcessRunning(server.Id))
        {
            var existing = _managedProcesses[server.Id];
            return (true, existing.Pid);
        }

        try
        {
            var startInfo = BuildProcessStartInfo(server);
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            process.Start();

            var managed = new ManagedProcess
            {
                ServerId = server.Id,
                Process = process,
                Pid = process.Id,
                StartedAt = DateTime.Now,
                ServerType = server.Type
            };

            _managedProcesses[server.Id] = managed;

            ProcessStarted?.Invoke(server.Id, process.Id);

            return (true, process.Id);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessManager] Failed to start server '{server.Name}': {ex.Message}");
            return (false, -1);
        }
    }

    /// <summary>
    /// サーバーのプロセスを停止する (Graceful → Kill)
    /// </summary>
    /// <param name="serverId">サーバーID</param>
    /// <returns>停止成功フラグ</returns>
    public async Task<bool> StopServerAsync(string serverId)
    {
        if (!_managedProcesses.TryGetValue(serverId, out var managed))
            return false;

        if (!managed.IsAlive)
        {
            CleanupProcess(serverId, managed);
            return true;
        }

        try
        {
            var process = managed.Process!;

            // Docker の場合は docker-compose down で停止
            if (managed.ServerType == "docker")
            {
                return await StopDockerAsync(managed);
            }

            // Step 1: Graceful shutdown (CloseMainWindow)
            if (process.CloseMainWindow())
            {
                // 3秒待機
                if (await WaitForExitAsync(process, TimeSpan.FromSeconds(3)))
                {
                    CleanupProcess(serverId, managed);
                    return true;
                }
            }

            // Step 2: Force kill
            process.Kill(entireProcessTree: true);
            await WaitForExitAsync(process, TimeSpan.FromSeconds(2));

            CleanupProcess(serverId, managed);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessManager] Failed to stop server '{serverId}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// サーバーのプロセスを再起動する
    /// </summary>
    public async Task<(bool Success, int Pid)> RestartServerAsync(ServerConfig server)
    {
        await StopServerAsync(server.Id);

        // 少し待ってから再起動
        await Task.Delay(500);

        return StartServer(server);
    }

    /// <summary>
    /// 指定サーバーのプロセスが稼働中か確認
    /// </summary>
    public bool IsProcessRunning(string serverId)
    {
        if (!_managedProcesses.TryGetValue(serverId, out var managed))
            return false;

        return managed.IsAlive;
    }

    /// <summary>
    /// 管理対象プロセスを取得
    /// </summary>
    public ManagedProcess? GetManagedProcess(string serverId)
    {
        _managedProcesses.TryGetValue(serverId, out var managed);
        return managed;
    }

    /// <summary>
    /// 全管理対象プロセスのIDリストを取得
    /// </summary>
    public IReadOnlyCollection<string> GetAllManagedServerIds()
    {
        return _managedProcesses.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// アプリ終了時に全プロセスの管理を解放する (プロセス自体は停止しない)
    /// </summary>
    public void ReleaseAll()
    {
        foreach (var kvp in _managedProcesses)
        {
            try
            {
                kvp.Value.Process?.Dispose();
            }
            catch { }
        }
        _managedProcesses.Clear();
    }

    /// <summary>
    /// サーバータイプに応じた ProcessStartInfo を構築
    /// </summary>
    private ProcessStartInfo BuildProcessStartInfo(ServerConfig server)
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };

        // 作業ディレクトリの設定
        if (!string.IsNullOrWhiteSpace(server.WorkingDirectory) && Directory.Exists(server.WorkingDirectory))
        {
            startInfo.WorkingDirectory = server.WorkingDirectory;
        }
        else if (!string.IsNullOrWhiteSpace(server.ExecutablePath))
        {
            var dir = Path.GetDirectoryName(server.ExecutablePath);
            if (dir != null && Directory.Exists(dir))
            {
                startInfo.WorkingDirectory = dir;
            }
        }

        switch (server.Type.ToLowerInvariant())
        {
            case "exe":
            case "bat":
            case "cmd":
                // ダイレクト実行
                startInfo.FileName = server.ExecutablePath;
                startInfo.Arguments = server.Arguments;
                if (server.Type is "bat" or "cmd")
                {
                    // bat/cmd は cmd.exe 経由で実行
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = $"/c \"{server.ExecutablePath}\" {server.Arguments}";
                }
                break;

            case "ps1":
                // PowerShell: ExecutionPolicy Bypass 自動付与
                startInfo.FileName = "powershell.exe";
                startInfo.Arguments = $"-ExecutionPolicy Bypass -File \"{server.ExecutablePath}\" {server.Arguments}";
                break;

            case "jar":
                // Java: ExecutablePath にはJavaパスを指定、Arguments に -Xmx 等と -jar <jarpath> を含む
                startInfo.FileName = server.ExecutablePath;
                startInfo.Arguments = server.Arguments;
                break;

            case "py":
                // Python: venv対応 (ExecutablePath にpythonパスを指定)
                startInfo.FileName = server.ExecutablePath;
                startInfo.Arguments = server.Arguments;
                break;

            case "sh":
                // Shell: bash.exe 経由
                startInfo.FileName = "bash.exe";
                startInfo.Arguments = $"\"{server.ExecutablePath}\" {server.Arguments}";
                break;

            case "docker":
                // Docker Compose
                startInfo.FileName = "docker-compose";
                startInfo.Arguments = $"-f \"{server.ExecutablePath}\" up -d {server.Arguments}";
                break;

            case "custom":
                // カスタムコマンド: ExecutablePath をそのままコマンドとして実行
                // コマンドの最初の部分をファイル名、残りを引数として分割
                var parts = server.ExecutablePath.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                startInfo.FileName = parts[0];
                startInfo.Arguments = parts.Length > 1
                    ? $"{parts[1]} {server.Arguments}"
                    : server.Arguments;
                break;

            default:
                startInfo.FileName = server.ExecutablePath;
                startInfo.Arguments = server.Arguments;
                break;
        }

        return startInfo;
    }

    /// <summary>
    /// Docker Compose のプロセスを停止
    /// </summary>
    private async Task<bool> StopDockerAsync(ManagedProcess managed)
    {
        try
        {
            var stopProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker-compose",
                    Arguments = $"-f \"{managed.Process?.StartInfo.Arguments}\" down",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            stopProcess.Start();
            await WaitForExitAsync(stopProcess, TimeSpan.FromSeconds(30));

            CleanupProcess(managed.ServerId, managed);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// プロセスの終了を非同期で待機
    /// </summary>
    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// プロセス終了後のクリーンアップ
    /// </summary>
    private void CleanupProcess(string serverId, ManagedProcess managed)
    {
        try
        {
            if (managed.Process != null && managed.Process.HasExited)
            {
                managed.ExitCode = managed.Process.ExitCode;
            }
        }
        catch { }

        ProcessStopped?.Invoke(serverId, managed.ExitCode);
    }
}
