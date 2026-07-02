namespace Dlt645Master.Core.Services;

/// <summary>
/// 收发统计快照（不可变）。任何时刻通过 <see cref="IMeterPollingService.Statistics"/> 取到的都是一份内部一致的快照。
/// 记账口径：
/// - 超时（ReceiveFrame 返回 null）只计入 TimeoutCount，不计入 ErrorCount；
/// - 收到帧但解析失败（校验和错 / 异常应答 D1H / 未知 DI）计入 ErrorCount，同时也计入 RxFrameCount（确实收到了帧）；
/// - 成功解析计入 RxFrameCount 并刷新 LastRoundTripMs。
/// </summary>
public sealed class CommStatistics
{
    public long TxFrameCount { get; init; }

    public long RxFrameCount { get; init; }

    public long TimeoutCount { get; init; }

    public long ErrorCount { get; init; }

    /// <summary>最近一次成功往返耗时（毫秒）；尚无成功记录时为 0。</summary>
    public double LastRoundTripMs { get; init; }
}
