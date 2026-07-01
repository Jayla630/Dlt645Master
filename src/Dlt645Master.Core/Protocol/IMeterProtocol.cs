using Dlt645Master.Core.Models;

namespace Dlt645Master.Core.Protocol;

/// <summary>
/// 纯粹、与传输方式无关的电表协议编解码器：输入 byte[]，输出 byte[] / 结果对象。
/// 该接口背后不应存在任何串口或 UI 依赖。
/// 新增协议只需实现本接口，并在 <see cref="MeterProtocolRegistry"/> 中注册一行——不使用基于反射的插件加载。
/// </summary>
public interface IMeterProtocol
{
    /// <summary>为给定的 6 字节地址与 4 字节数据标识构造一条“读数据”请求帧。</summary>
    byte[] BuildReadRequest(byte[] address, byte[] dataId);

    /// <summary>尝试解析一条应答帧。对畸形输入不会抛异常，失败信息通过返回值体现。</summary>
    MeterReadResult TryParseResponse(byte[] frame);
}
