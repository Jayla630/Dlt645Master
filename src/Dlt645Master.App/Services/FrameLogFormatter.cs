using System.Globalization;
using System.Text;
using Dlt645Master.App.ViewModels;

namespace Dlt645Master.App.Services;

/// <summary>
/// 报文监视条目 → 纯文本的格式化（不碰文件系统，便于测试）。
/// 每条两行与界面显示一致：第一行「时间戳 方向」、第二行十六进制报文，条目间空行。
/// </summary>
public static class FrameLogFormatter
{
    /// <summary>把报文条目序列格式化为导出文本（行尾 CRLF，末尾带换行）。</summary>
    public static string Format(IEnumerable<FrameLogEntry> entries)
    {
        var builder = new StringBuilder();
        bool first = true;
        foreach (FrameLogEntry entry in entries)
        {
            if (!first)
            {
                builder.Append("\r\n");
            }

            builder
                .Append(entry.Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(entry.DirectionText)
                .Append("\r\n")
                .Append(entry.HexText)
                .Append("\r\n");
            first = false;
        }

        return builder.ToString();
    }
}
