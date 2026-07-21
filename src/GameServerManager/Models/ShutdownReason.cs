namespace GameServerManager.Models;

/// <summary>
/// 停止/再起動時の理由情報 (§3.3 Windows Server風ダイアログ)
/// </summary>
public class ShutdownReason
{
    /// <summary>
    /// 分類: 計画的 / 非計画的
    /// </summary>
    public ShutdownClassification Classification { get; set; } = ShutdownClassification.Planned;

    /// <summary>
    /// 理由テキスト (自由記述)
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 分類の表示テキスト
    /// </summary>
    public string ClassificationText => Classification switch
    {
        ShutdownClassification.Planned => "計画的",
        ShutdownClassification.Unplanned => "非計画的",
        _ => "不明"
    };
}

/// <summary>
/// 停止/再起動の分類
/// </summary>
public enum ShutdownClassification
{
    /// <summary>計画的 (Planned)</summary>
    Planned,

    /// <summary>非計画的 (Unplanned)</summary>
    Unplanned
}
