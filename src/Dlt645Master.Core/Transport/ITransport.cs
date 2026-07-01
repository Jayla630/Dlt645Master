namespace Dlt645Master.Core.Transport;

/// <summary>
/// 传输层抽象：隔离真实串口与仿真从站，负责收发 / 超时 / 组帧。
/// 报文监视（TX/RX 计数等）不在这层做，只保证 Send / ReceiveFrame 进出的是完整原始帧字节。
/// </summary>
public interface ITransport : IDisposable
{
    bool IsOpen { get; }

    void Open();

    void Close();

    /// <summary>发送一帧原始字节（完整 DL/T645 帧，由上层构造好）。</summary>
    void Send(byte[] frame);

    /// <summary>
    /// 在 timeout 内尝试读取一个完整 DL/T645 帧。
    /// 读到则返回帧字节；超时未收齐返回 null（不抛超时异常，与「失败通过返回值体现」的风格一致）。
    /// </summary>
    byte[]? ReceiveFrame(TimeSpan timeout);
}
