using System.Globalization;
using System.Text;

namespace Dlt645Master.App.Services;

/// <summary>
/// 读数记录 → CSV 文本的纯函数格式化（不碰文件系统，便于测试）。
/// 数值一律不变文化序列化（小数点固定为点号），字段按 RFC 4180 转义，行尾固定 CRLF。
/// </summary>
public static class ReadingCsvFormatter
{
    /// <summary>CSV 表头行。</summary>
    public const string Header = "时间戳,数据项,DI,数值,单位,状态,备注";

    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    /// <summary>把全部记录格式化为完整 CSV 文本（含表头，行尾 CRLF）。</summary>
    public static string Format(IEnumerable<ReadingRecord> records)
    {
        var builder = new StringBuilder();
        builder.Append(Header).Append("\r\n");
        foreach (ReadingRecord record in records)
        {
            builder.Append(FormatRecord(record)).Append("\r\n");
        }

        return builder.ToString();
    }

    /// <summary>格式化单条记录为一行 CSV（不含行尾）。</summary>
    public static string FormatRecord(ReadingRecord record)
    {
        string[] fields =
        [
            record.Timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture),
            record.ItemName,
            record.DataIdText,
            record.Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            record.Unit,
            record.IsSuccess ? "成功" : "失败",
            record.ErrorMessage,
        ];

        return string.Join(',', fields.Select(EscapeField));
    }

    /// <summary>
    /// 按 CSV 规范转义字段：含逗号/引号/换行时整体加引号并把内部引号翻倍，否则原样返回。
    /// 数据项名是中文一般不触发，但错误信息等自由文本必须防住。
    /// </summary>
    public static string EscapeField(string field)
    {
        if (field.AsSpan().IndexOfAny(',', '"') < 0 && !field.Contains('\r') && !field.Contains('\n'))
        {
            return field;
        }

        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
