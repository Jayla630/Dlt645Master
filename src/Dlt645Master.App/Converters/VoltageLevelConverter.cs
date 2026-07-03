using System.Globalization;
using System.Windows.Data;
using Dlt645Master.App.ViewModels;

namespace Dlt645Master.App.Converters;

/// <summary>
/// 电压值 → 进度条着色档位字符串：Normal（正常）/ High（≥ 上限 96%，接近上限转黄）/ Over（越过 250V 上限转红）。
/// XAML 触发器按档位映射主题令牌画刷（颜色本身仍集中在 DarkTheme.xaml，转换器不携带色值）。
/// </summary>
public sealed class VoltageLevelConverter : IValueConverter
{
    /// <summary>「接近上限」阈值：上限的 96%（250V 时即 240V）。</summary>
    public const double HighRatio = 0.96;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not decimal voltage)
        {
            return "Normal";
        }

        double volts = (double)voltage;
        return volts > MainWindowViewModel.VoltageAlarmLimit ? "Over"
            : volts >= MainWindowViewModel.VoltageAlarmLimit * HighRatio ? "High"
            : "Normal";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
