using System.Globalization;
using System.Windows.Data;

namespace Dlt645Master.App.Converters;

/// <summary>
/// 可用宽度 → 自适应列数：columns = max(1, floor(width / 最小卡宽))。
/// 卡片墙的 UniformGrid 用它吃满可用宽度、消灭右侧空白带；最小卡宽经 ConverterParameter 传入（默认 240）。
/// </summary>
public sealed class WidthToColumnsConverter : IValueConverter
{
    private const double DefaultMinItemWidth = 240;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double width = value is double actual && !double.IsNaN(actual) ? actual : 0;
        double minItemWidth = parameter is not null
            && double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            && parsed > 0
            ? parsed
            : DefaultMinItemWidth;

        return Math.Max(1, (int)(width / minItemWidth));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
