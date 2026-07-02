using Dlt645Master.App.Services;
using Dlt645Master.Core.Services;

namespace Dlt645Master.App.ViewModels;

/// <summary>报文监视条目：不可变。收发方向 + 时间戳 + 空格分隔大写十六进制报文。</summary>
public sealed class FrameLogEntry
{
    public FrameLogEntry(FrameDirection direction, byte[] frame, DateTimeOffset timestamp)
    {
        Direction = direction;
        Timestamp = timestamp;
        HexText = HexFormat.Spaced(frame);
    }

    public DateTimeOffset Timestamp { get; }

    public FrameDirection Direction { get; }

    /// <summary>方向的中文短标签，供报文监视列表直接展示。</summary>
    public string DirectionText => Direction == FrameDirection.Tx ? "发送" : "接收";

    /// <summary>报文十六进制文本，如 <c>68 AA AA AA AA AA AA 68 ...</c>。</summary>
    public string HexText { get; }
}
