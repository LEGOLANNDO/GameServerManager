using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GameServerManager.Helpers;

/// <summary>
/// コレクション数が 0 のときに Visible、1以上で Collapsed を返すコンバーター
/// サーバー未登録時の空状態メッセージ表示に使用
/// </summary>
public class InverseCountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// bool を Visibility に変換するコンバーター
/// true → Visible, false → Collapsed
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // parameter が "Inverse" の場合は反転
            if (parameter is string p && p == "Inverse")
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// bool を反転するコンバーター
/// true → false, false → true
/// テスト送信ボタンの IsEnabled 制御等に使用
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}
