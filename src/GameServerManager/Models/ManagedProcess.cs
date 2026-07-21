using System.Diagnostics;

namespace GameServerManager.Models;

/// <summary>
/// 管理対象プロセスの情報を保持するモデル
/// ProcessManagerService が起動したプロセスを追跡するために使用
/// </summary>
public class ManagedProcess
{
    /// <summary>
    /// サーバーID (ServerConfig.Id に対応)
    /// </summary>
    public string ServerId { get; set; } = string.Empty;

    /// <summary>
    /// System.Diagnostics.Process 参照
    /// </summary>
    public Process? Process { get; set; }

    /// <summary>
    /// プロセスID
    /// </summary>
    public int Pid { get; set; }

    /// <summary>
    /// 起動時刻
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// プロセスタイプ (exe/jar/ps1 等)
    /// </summary>
    public string ServerType { get; set; } = "exe";

    /// <summary>
    /// 最後に取得した終了コード (プロセス終了時のみ)
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// プロセスが生存しているか
    /// </summary>
    public bool IsAlive
    {
        get
        {
            try
            {
                return Process != null && !Process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }
}
