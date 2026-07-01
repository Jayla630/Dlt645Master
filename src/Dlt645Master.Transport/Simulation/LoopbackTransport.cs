using Dlt645Master.Core.Transport;

namespace Dlt645Master.Transport.Simulation;

/// <summary>内存回环传输：把 IMeterSlave 包成 ITransport，无需真实串口即可全链路闭环。</summary>
public sealed class LoopbackTransport : ITransport
{
    private readonly IMeterSlave _slave;
    private readonly TimeSpan? _responseDelay;
    private byte[]? _pendingResponse;

    public LoopbackTransport(IMeterSlave slave, TimeSpan? responseDelay = null)
    {
        _slave = slave;
        _responseDelay = responseDelay;
    }

    public bool IsOpen { get; private set; }

    public void Open() => IsOpen = true;

    public void Close() => IsOpen = false;

    /// <summary>把请求交给仿真从站处理；从站不应答（返回 null）时不缓存任何应答。</summary>
    public void Send(byte[] frame) => _pendingResponse = _slave.HandleRequest(frame);

    public byte[]? ReceiveFrame(TimeSpan timeout)
    {
        if (_pendingResponse is null)
        {
            return null;
        }

        if (_responseDelay is { } delay)
        {
            if (delay > timeout)
            {
                // 应答尚未到达即已超时：模拟真实链路中的超时无应答。
                return null;
            }

            Thread.Sleep(delay);
        }

        byte[] response = _pendingResponse;
        _pendingResponse = null;
        return response;
    }

    public void Dispose()
    {
        Close();
    }
}
