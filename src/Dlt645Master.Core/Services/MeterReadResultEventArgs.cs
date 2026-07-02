using Dlt645Master.Core.Models;

namespace Dlt645Master.Core.Services;

/// <summary>读取结果事件：直接携带一次一问一答的 <see cref="MeterReadResult"/>。</summary>
public sealed class MeterReadResultEventArgs : EventArgs
{
    public required MeterReadResult Result { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}
