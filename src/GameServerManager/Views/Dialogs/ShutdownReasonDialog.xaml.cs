using GameServerManager.Models;
using Wpf.Ui.Controls;

namespace GameServerManager.Views.Dialogs;

/// <summary>
/// Windows Server風 停止/再起動 理由入力ダイアログ (§3.3)
/// ContentDialog ベースでモーダル表示
/// </summary>
public partial class ShutdownReasonDialog : ContentDialog
{
    /// <summary>
    /// ダイアログの結果として取得する ShutdownReason
    /// </summary>
    public ShutdownReason? Result { get; private set; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="operationType">"停止" または "再起動"</param>
    /// <param name="serverName">対象サーバー名</param>
    public ShutdownReasonDialog(string operationType, string serverName)
    {
        InitializeComponent();
        OperationDescription.Text = $"サーバー「{serverName}」を{operationType}します。\n理由を入力してください。";
        Title = $"{operationType}の確認";
    }

    /// <summary>
    /// ダイアログを表示し、結果を返す
    /// </summary>
    /// <returns>ユーザーが「実行」を押した場合 ShutdownReason、キャンセル時は null</returns>
    public async Task<ShutdownReason?> ShowAndGetResultAsync()
    {
        var dialogResult = await ShowAsync();

        if (dialogResult == ContentDialogResult.Primary)
        {
            Result = new ShutdownReason
            {
                Classification = PlannedRadio.IsChecked == true
                    ? ShutdownClassification.Planned
                    : ShutdownClassification.Unplanned,
                Reason = ReasonTextBox.Text?.Trim() ?? string.Empty
            };
            return Result;
        }

        return null;
    }
}
