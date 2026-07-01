using System.IO.Ports;

namespace Dlt645Master.Transport.Serial;

/// <summary>串口连接参数（DL/T645-2007 常用默认值：2400 波特、8 数据位、偶校验、1 停止位）。</summary>
public sealed class SerialPortSettings
{
    public string PortName { get; init; } = "COM1";

    public int BaudRate { get; init; } = 2400;

    public Parity Parity { get; init; } = Parity.Even;

    public int DataBits { get; init; } = 8;

    public StopBits StopBits { get; init; } = StopBits.One;

    /// <summary>单次读操作的底层超时。</summary>
    public TimeSpan ReadTimeout { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>发送前预置的 FE 唤醒字节个数（默认 0，不影响黄金向量）。</summary>
    public int WakeupByteCount { get; init; } = 0;
}
