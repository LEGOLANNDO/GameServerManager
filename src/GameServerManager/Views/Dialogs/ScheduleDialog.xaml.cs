using Wpf.Ui.Controls;

namespace GameServerManager.Views.Dialogs;

public class ScheduleDialogResult
{
    public bool IsCleared { get; set; }
    public DateTime? ScheduledTime { get; set; }
    public string ScheduledAction { get; set; } = string.Empty;
}

/// <summary>
/// スケジュール設定ダイアログ
/// </summary>
public partial class ScheduleDialog : ContentDialog
{
    public ScheduleDialogResult? Result { get; private set; }

    public ScheduleDialog(string serverName, DateTime? existingTime, string existingAction)
    {
        InitializeComponent();
        
        DescriptionText.Text = $"サーバー「{serverName}」の自動実行スケジュールを設定します。";

        // 時間と分のコンボボックスを初期化
        for (int i = 0; i < 24; i++) HourComboBox.Items.Add(i.ToString("D2"));
        for (int i = 0; i < 60; i += 5) MinuteComboBox.Items.Add(i.ToString("D2"));

        if (existingTime.HasValue)
        {
            ScheduleDatePicker.SelectedDate = existingTime.Value.Date;
            HourComboBox.SelectedItem = existingTime.Value.Hour.ToString("D2");
            MinuteComboBox.SelectedItem = existingTime.Value.Minute.ToString("D2");
            if (existingAction == "Stop") StopRadio.IsChecked = true;
            else RestartRadio.IsChecked = true;
        }
        else
        {
            var now = DateTime.Now.AddMinutes(5); // デフォルトは5分後
            ScheduleDatePicker.SelectedDate = now.Date;
            HourComboBox.SelectedItem = now.Hour.ToString("D2");
            var min = (now.Minute / 5) * 5; // 5分刻みに丸める
            MinuteComboBox.SelectedItem = min.ToString("D2");
        }

        // メインウィンドウの DialogHost を設定
        if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
        {
            DialogHost = mainWindow.RootContentDialog;
        }
    }

    public async Task<ScheduleDialogResult?> ShowAndGetResultAsync()
    {
        var dialogResult = await ShowAsync();

        if (dialogResult == ContentDialogResult.Primary)
        {
            // スケジュール設定
            if (ScheduleDatePicker.SelectedDate.HasValue && 
                HourComboBox.SelectedItem is string hourStr &&
                MinuteComboBox.SelectedItem is string minuteStr &&
                int.TryParse(hourStr, out int hour) &&
                int.TryParse(minuteStr, out int minute))
            {
                var date = ScheduleDatePicker.SelectedDate.Value;
                var time = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0);

                Result = new ScheduleDialogResult
                {
                    IsCleared = false,
                    ScheduledTime = time,
                    ScheduledAction = RestartRadio.IsChecked == true ? "Restart" : "Stop"
                };
                return Result;
            }
        }
        else if (dialogResult == ContentDialogResult.Secondary)
        {
            // スケジュール解除
            Result = new ScheduleDialogResult
            {
                IsCleared = true
            };
            return Result;
        }

        return null;
    }
}
