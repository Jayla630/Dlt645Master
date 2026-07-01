using System.IO.Ports;
using Dlt645Master.Core.Protocol;
using Dlt645Master.Core.Transport;

namespace Dlt645Master.Transport.Serial;

/// <summary>
/// 基于 System.IO.Ports 的真实串口传输实现。采用「同步 + 超时」风格，不引入 async；
/// 多线程调度（后台读循环等）留给上层服务封装。
/// </summary>
public sealed class SerialPortTransport : ITransport
{
    private const byte WakeupByte = 0xFE;
    private const int PollIntervalMilliseconds = 10;

    private readonly SerialPortSettings _settings;
    private readonly SerialPort _serialPort;
    private readonly Dlt645FrameScanner _scanner = new();

    public SerialPortTransport(SerialPortSettings settings)
    {
        _settings = settings;
        _serialPort = new SerialPort(settings.PortName, settings.BaudRate, settings.Parity, settings.DataBits, settings.StopBits)
        {
            ReadTimeout = (int)settings.ReadTimeout.TotalMilliseconds,
        };
    }

    public bool IsOpen => _serialPort.IsOpen;

    public void Open()
    {
        _scanner.Reset();
        _serialPort.Open();
    }

    public void Close()
    {
        if (_serialPort.IsOpen)
        {
            _serialPort.Close();
        }
    }

    public void Send(byte[] frame)
    {
        if (_settings.WakeupByteCount > 0)
        {
            byte[] wakeup = new byte[_settings.WakeupByteCount];
            Array.Fill(wakeup, WakeupByte);
            _serialPort.Write(wakeup, 0, wakeup.Length);
        }

        _serialPort.Write(frame, 0, frame.Length);
    }

    public byte[]? ReceiveFrame(TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        byte[] readBuffer = new byte[256];

        while (true)
        {
            if (_scanner.TryReadFrame(out byte[] frame))
            {
                return frame;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return null;
            }

            int bytesToRead = _serialPort.BytesToRead;
            if (bytesToRead > 0)
            {
                int count = _serialPort.Read(readBuffer, 0, Math.Min(bytesToRead, readBuffer.Length));
                _scanner.Append(readBuffer.AsSpan(0, count));
            }
            else
            {
                Thread.Sleep(PollIntervalMilliseconds);
            }
        }
    }

    public void Dispose()
    {
        _serialPort.Dispose();
    }
}
