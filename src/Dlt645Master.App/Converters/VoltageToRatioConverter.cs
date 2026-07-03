using System.Globalization;
using System.Windows.Data;
using Dlt645Master.App.ViewModels;

namespace Dlt645Master.App.Converters;

/// <summary>
/// 电压值 → 对 250V 上限（<see cref="MainWindowViewModel.VoltageAlarmLimit"/>）的占比 [0, 1]。
/// 电压卡底部进度条的填充比例（Rectangle 的 ScaleTransform.ScaleX）用它驱动；空值按 0 处理。
/// </summary>
public sealed class VoltageToRatioConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not decimal voltage)
        {
            return 0d;
        }

        double ratio = (double)voltage / MainWindowViewModel.VoltageAlarmLimit;
        return Math.Clamp(ratio, 0d, 1d);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
