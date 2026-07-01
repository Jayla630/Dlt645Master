namespace Dlt645Master.Transport.Simulation;

/// <summary>仿真数据源：按数据标识提供电表当前值。</summary>
public interface IMeterDataSource
{
    /// <summary>按数据标识（4 字节，显示序/高位在前）取值；无此数据标识返回 false。</summary>
    bool TryGetValue(byte[] dataId, out decimal value);
}
