namespace Dlt645Master.Core.Services;

/// <summary>统计变更事件：每次记账后触发一次，携带变更后的不可变快照，供状态栏订阅。</summary>
public sealed class StatisticsChangedEventArgs : EventArgs
{
    public required CommStatistics Snapshot { get; init; }
}
