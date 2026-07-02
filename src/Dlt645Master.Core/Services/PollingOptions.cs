namespace Dlt645Master.Core.Services;

/// <summary>轮询配置。地址与数据标识均为显示序（高位在前），与 <see cref="Protocol.IMeterProtocol"/> 口径一致。</summary>
public sealed class PollingOptions
{
    /// <summary>目标电表地址（6 字节，显示序）。</summary>
    public required byte[] MeterAddress { get; init; }

    /// <summary>本轮要循环读取的数据项 DI 清单（每项 4 字节，显示序）。</summary>
    public required IReadOnlyList<byte[]> DataIds { get; init; }

    /// <summary>两轮轮询之间的间隔（一轮 = 把 DataIds 全部读一遍）。</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>单次请求的应答超时（传给 ITransport.ReceiveFrame）。</summary>
    public TimeSpan ResponseTimeout { get; init; } = TimeSpan.FromMilliseconds(800);

    /// <summary>同一轮内相邻两条请求之间的帧间静默（RS485 半双工换向余量）。</summary>
    public TimeSpan InterFrameDelay { get; init; } = TimeSpan.FromMilliseconds(50);
}
