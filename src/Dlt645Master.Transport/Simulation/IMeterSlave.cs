namespace Dlt645Master.Transport.Simulation;

/// <summary>仿真从站：处理主站请求帧，返回应答帧。</summary>
public interface IMeterSlave
{
    /// <summary>
    /// 处理一帧主站请求，返回应答帧字节。
    /// 广播 / 地址不匹配 / 校验失败 / 不予应答时返回 null（模拟从站不回话）。
    /// </summary>
    byte[]? HandleRequest(byte[] requestFrame);
}
