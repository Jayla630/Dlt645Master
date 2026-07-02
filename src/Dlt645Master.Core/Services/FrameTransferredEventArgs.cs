namespace Dlt645Master.Core.Services;

/// <summary>报文监视事件：一帧原始字节 + 收发方向 + 时间戳，供界面报文监视窗口订阅。</summary>
public sealed class FrameTransferredEventArgs : EventArgs
{
    public required FrameDirection Direction { get; init; }

    /// <summary>完整原始帧字节（防御性拷贝，订阅方可放心持有）。</summary>
    public required byte[] Frame { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}
